using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using LuaInterface.LuaAPI;

namespace LuaInterface
{
	/// <summary>Functions used in the metatables of userdata representing CLR objects</summary>
	/// <remarks>Author: Fabio Mascarenhas</remarks>
	class MetaFunctions
	{
		readonly Lua interpreter;
		readonly ObjectTranslator translator;
		readonly lua.CFunction _index, _newindex,
			_baseIndex, _classIndex, _classNewindex,
			_classCall, _toString;

		public MetaFunctions(lua.State L, ObjectTranslator translator)
		{
			Debug.Assert(translator.interpreter.IsSameLua(L));
			this.translator = translator;
			this.interpreter = translator.interpreter;

			// used by BuildObjectMetatable
			_toString = this.toString;
			_index    = this.index;
			_newindex = this.newIndex;

			luaL.checkstack(L, 2, "new MetaFunctions");
			StackAssert.Start(L);

			// Creates the metatable for superclasses (the base field of registered tables)
			bool didnt_exist = luaclr.newrefmeta(L, "luaNet_searchbase", 3);
			Debug.Assert(didnt_exist);
			lua.pushcfunction(L,_toString                  ); lua.setfield(L,-2,"__tostring");
			lua.pushcfunction(L,_baseIndex = this.baseIndex); lua.setfield(L,-2,"__index");
			lua.pushcfunction(L,_newindex                  ); lua.setfield(L,-2,"__newindex");
			lua.pop(L,1);

			// Creates the metatable for type references
			didnt_exist = luaclr.newrefmeta(L, "luaNet_class", 4);
			Debug.Assert(didnt_exist);
			lua.pushcfunction(L,_toString                          ); lua.setfield(L,-2,"__tostring");
			lua.pushcfunction(L,_classIndex    = this.classIndex   ); lua.setfield(L,-2,"__index");
			lua.pushcfunction(L,_classNewindex = this.classNewIndex); lua.setfield(L,-2,"__newindex");
			lua.pushcfunction(L,_classCall     = this.classCall    ); lua.setfield(L,-2,"__call");
			lua.pop(L,1);

			StackAssert.End();
		}

		/// <summary>[-0, +0, m, requires checkstack(1)] Add CLR object instance metatable entries to a new ref metatable at the top of the stack.</summary>
		public void BuildObjectMetatable(lua.State L)
		{
			Debug.Assert(interpreter.IsSameLua(L) && luaclr.isrefmeta(L, -1));
			lua.newtable(L);                lua.setfield(L,-2,"cache");
			lua.pushcfunction(L,_index   ); lua.setfield(L,-2,"__index");
			lua.pushcfunction(L,_toString); lua.setfield(L,-2,"__tostring");
			lua.pushcfunction(L,_newindex); lua.setfield(L,-2,"__newindex");
		}

		/// <summary>__tostring metafunction of CLR objects.</summary>
		int toString(lua.State L)
		{
			Debug.Assert(interpreter.IsSameLua(L) && luanet.infunction(L));
			var obj = luaclr.checkref(L, 1);
			string s;
			var c = luanet.entercfunction(L, interpreter);
			try { s = obj.ToString(); }
			catch (LuaInternalException) { throw; }
			catch (Exception ex) { return translator.throwError(L, ex); }
			finally { c.Dispose(); }
			lua.pushstring(L, s ?? "");
			return 1;
		}

		/// <summary>__index metafunction of CLR objects. Checks metatable.cache for previously used functions before calling getMethod to look up the member.</summary>
		int index(lua.State L)
		{
			using (luanet.entercfunction(L, interpreter))
			{
				if (lua.gettop(L) != 2)
					return luaL.error(L, "__index requires 2 arguments");
				// __index(o,k)
				if (!luaL.getmetafield(L, 1, "cache"))
					return luaL.typerror(L, 1, "CLR object");
				lua.pushvalue(L, 2);
				lua.gettable(L, 3); // metatable(o).cache[k]
				if (!lua.isnil(L, -1))
					return 1;

				lua.settop(L, 3);
				if (getMethod(L) != 2)
					return luaL.error(L, "unexpected getMethod return values");

				if (lua.type(L, -1) == LUA.T.STRING)
					return luaL.error(L, lua.tostring(L, -1));
				bool is_function = lua.toboolean(L, -1);
				lua.pop(L, 1);

				Debug.Assert(lua.gettop(L) <= LUA.MINSTACK-2);
				if (is_function)
				{
					lua.pushvalue(L, 2);
					lua.pushvalue(L, -2);
					lua.settable(L, 3); // metatable(o).cache[k] = getMethod(L)
				}
				return 1;
			}
		}

		/// <summary>[-0, +2, e]
		/// Called by the __index metafunction of CLR objects in case the method is not cached or it is a field/property/event.
		/// Receives the object and the member name as arguments and returns either the value of the member or a delegate to call it.
		/// If the member does not exist returns nil.
		/// </summary>
		int getMethod(lua.State L)
		{
			Debug.Assert(interpreter.IsSameLua(L) && luanet.infunction(L));
			object obj = luaclr.checkref(L, 1);

			object index = translator.getObject(L, 2);
			//Type indexType = index.GetType();

			string methodName = index as string;        // will be null if not a string arg
			Type objType = obj.GetType();

			// Handle the most common case, looking up the method by name.

			// CP: This will fail when using indexers and attempting to get a value with the same name as a property of the object,
			// ie: xmlelement['item'] <- item is a property of xmlelement
			if (methodName != null && isMemberPresent(objType, methodName))
				return getMember(L, objType, obj, methodName, BindingFlags.Instance);
			bool failed = true;

			// Try to access by array if the type is right and index is an int (lua numbers always come across as double)
			if (objType.IsArray && index is double)
			{
				object val;
				try { val = ((Array) obj).GetValue((int)(double) index); }
				catch (Exception ex) { return translator.throwError(L, ex); }
				translator.push(L,val);
				failed = false;
			}
			else
			{
				try
				{
					// Try to use get_Item to index into this .net object
					//MethodInfo getter = objType.GetMethod("get_Item");
					// issue here is that there may be multiple indexers..
					foreach (MethodInfo mInfo in objType.GetMethods())
					if (mInfo.Name == "get_Item")
					{
						ParameterInfo[] actualParms = mInfo.GetParameters();
						if (actualParms.Length != 1) //check if the signature matches the input
							continue;
						// Get the index in a form acceptable to the getter
						index = translator.getAsType(L, 2, actualParms[0].ParameterType);
						// Just call the indexer - if out of bounds an exception will happen
						object o = mInfo.Invoke(obj, new[]{index});
						failed = false;
						translator.push(L, o);
						break;
					}
				}
				catch (TargetInvocationException ex) {
					return translator.throwError(L, luaclr.verifyex(ex.InnerException));
				}
				catch (LuaInternalException) { throw; }
				catch (Exception ex) {
					return luaL.error(L, "unable to index {0}: {1}", objType, ex.Message);
				}
			}
			if (failed) {
				return luaL.error(L,"cannot find " + index);
			}
			lua.pushboolean(L, false);
			return 2;
		}


		/// <summary>
		/// __index metafunction of base classes (the base field of Lua tables).
		/// Adds a prefix to the method name to call the base version of the method.
		/// </summary>
		int baseIndex(lua.State L)
		{
			using (luanet.entercfunction(L, interpreter))
			{
				object obj = luaclr.checkref(L, 1);

				string methodName = lua.tostring(L, 2);
				if (methodName == null)
				{
					lua.pushnil(L);
					lua.pushboolean(L, false);
					return 2;
				}
				getMember(L, obj.GetType(), obj, "__luaInterface_base_" + methodName, BindingFlags.Instance);
				lua.pop(L,1);
				if (lua.type(L, -1) == LUA.T.NIL)
				{
					lua.pop(L,1);
					return getMember(L, obj.GetType(), obj, methodName, BindingFlags.Instance);
				}
				lua.pushboolean(L, false);
				return 2;
			}
		}


		/// <summary>Does this method exist as either an instance or static?</summary><exception cref="ArgumentNullException">All arguments are required.</exception>
		bool isMemberPresent(IReflect objType, string methodName)
		{
			Debug.Assert(objType != null && methodName != null);
			object cachedMember = checkMemberCache(objType, methodName);

			if (cachedMember != null)
				return true;

			MemberInfo[] members = objType.GetMember(methodName, BindingFlags.Static | BindingFlags.Instance | luanet.LuaBindingFlags);
			return (members.Length != 0);
		}

		/// <summary>[-0, +2, e]
		/// Returns to Lua the value of a member or a delegate to call it, depending on the type of the member.
		/// Works with static or instance members. Uses reflection to find members,
		/// and stores the reflected MemberInfo object in a cache (indexed by <paramref name="objType"/> and <paramref name="methodName"/>).
		/// </summary>
		/// <exception cref="ArgumentNullException"><paramref name="objType"/> and <paramref name="methodName"/> are required</exception>
		int getMember(lua.State L, IReflect objType, object obj, string methodName, BindingFlags bindingType)
		{
			Debug.Assert(interpreter.IsSameLua(L));
			Debug.Assert(objType != null && methodName != null);

			Debug.Assert((obj == null) == (objType is ProxyType));
			Debug.Assert((obj == null) != (objType is Type));
			Debug.Assert((obj == null) == ((bindingType & BindingFlags.Static) == BindingFlags.Static));
			Debug.Assert((obj == null) != ((bindingType & BindingFlags.Instance) == BindingFlags.Instance));

			MemberInfo member = null;
			object cachedMember = checkMemberCache(objType, methodName);
			if (cachedMember != null)
			{
				var cachedMethod = cachedMember as lua.CFunction;
				if (cachedMethod != null)
				{
					luaclr.pushcfunction(L, cachedMethod);
					lua.pushboolean(L, true);
					return 2;
				}
				Debug.Assert(cachedMember is MemberInfo);
				member = (MemberInfo)cachedMember;
			}
			else
			{
				MemberInfo[] members = objType.GetMember(methodName, bindingType | luanet.LuaBindingFlags);
				if (members.Length != 0)
				{
					member = members[0];
					#if DEBUG
					if (!(members.Length == 1 || member is MethodBase)) // todo
						return luaL.error(L, "Overloads for members other than methods are not implemented.");
					#endif
				}
			}

			object value = null;

			switch (member == null ? MemberTypes.All : member.MemberType)
			{
			default: // not found or found a constructor
				// kevinh - we want to throw an error because merely returning 'nil' in this case
				// is not sufficient.  valid data members may return nil and therefore there must be some
				// way to know the member just doesn't exist.
				return luaL.error(L, string.Format("'{0}' does not contain a definition for '{1}'", objType.UnderlyingSystemType.FullName, methodName));

			case MemberTypes.Method:
				var wrapper = new lua.CFunction((new LuaMethodWrapper(translator, objType, methodName, bindingType)).call);

				if (cachedMember == null) setMemberCache(objType, methodName, wrapper);
				luaclr.pushcfunction(L, wrapper);
				lua.pushboolean(L, true);
				return 2;

			case MemberTypes.Field:
				try { value = ((FieldInfo) member).GetValue(obj); }
				catch { goto default; }
				translator.push(L, value);
				break;

			case MemberTypes.Property:
				// todo: support indexed properties
				try { value = ((PropertyInfo) member).GetValue(obj, null); }
				catch (TargetInvocationException ex) { return translator.throwError(L, luaclr.verifyex(ex.InnerException)); }
				catch { goto default; }
				translator.push(L, value);
				break;

			case MemberTypes.Event:
				value = new RegisterEventHandler(translator.pendingEvents, obj, (EventInfo) member);
				translator.push(L, value);
				break;

			case MemberTypes.NestedType:
				var nestedType = (Type) member;
				if (translator.FindType(nestedType)) // don't hand out class references unless loaded/whitelisted
					ObjectTranslator.pushType(L, nestedType);
				else
					lua.pushnil(L);
				break;
			}

			if (cachedMember == null) setMemberCache(objType, methodName, member);
			// push false because we are NOT returning a function (see luaIndexFunction)
			lua.pushboolean(L, false);
			return 2;
		}

		readonly Dictionary<IReflect, Dictionary<string, object>> memberCache = new Dictionary<IReflect, Dictionary<string, object>>();

		/// <summary>Checks if a MemberInfo object is cached, returning it or null.</summary><exception cref="ArgumentNullException">All arguments are required.</exception>
		object checkMemberCache(IReflect objType, string memberName)
		{
			Debug.Assert(objType != null && memberName != null);
			Dictionary<string, object> members;
			object member;
			if (memberCache.TryGetValue(objType, out members)
			&& members.TryGetValue(memberName, out member))
				return member;
			else
				return null;
		}
		/// <summary>Stores a MemberInfo object in the member cache.</summary><exception cref="ArgumentNullException"><paramref name="objType"/> and <paramref name="memberName"/> are required. Null <paramref name="member"/> won't throw an exception but shouldn't be null.</exception>
		void setMemberCache(IReflect objType, string memberName, object member)
		{
			Debug.Assert(objType != null && memberName != null && member != null);
			Debug.Assert(member is lua.CFunction || member is MemberInfo);
			var memberCache = this.memberCache;
			Dictionary<string, object> members;
			if (!memberCache.TryGetValue(objType, out members))
				memberCache[objType] = members = new Dictionary<string, object>();

			members[memberName] = member;
		}

		/// <summary>
		/// __newindex metafunction of CLR objects.
		/// Receives the object, the member name and the value to be stored as arguments.
		/// Throws and error if the assignment is invalid.
		/// </summary>
		int newIndex(lua.State L)
		{
			using (luanet.entercfunction(L, interpreter))
			{
				object target = luaclr.checkref(L, 1);
				Type type = target.GetType();

				// First try to look up the parameter as a property name
				string detailMessage;
				if (trySetMember(L, type, target, BindingFlags.Instance, out detailMessage))
					return 0;       // found and set the property

				// We didn't find a property name, now see if we can use a [] style this accessor to set array contents
				try
				{
					if (type.IsArray && lua.type(L, 2) == LUA.T.NUMBER)
					{
						int index = (int) lua.tonumber(L, 2);

						var arr = (Array) target;
						object val = translator.getAsType(L, 3, type.GetElementType());
						arr.SetValue(val, index);
					}
					else
					{
						// Try to see if we have a this[] accessor
						MethodInfo setter = type.GetMethod("set_Item");
						if (setter == null)
							return luaL.error(L, detailMessage); // Pass the original message from trySetMember because it is probably best

						ParameterInfo[] args = setter.GetParameters();
						if (args.Length != 2) // avoid throwing IndexOutOfRangeException for functions that take less than 2 arguments. that would be confusing in this context.
							return luaL.error(L, detailMessage);

						object index = translator.getAsType(L, 2, args[0].ParameterType);
						// The new value the user specified
						object val   = translator.getAsType(L, 3, args[1].ParameterType);

						// Just call the indexer - if out of bounds an exception will happen
						setter.Invoke(target, new object[] { index, val });
					}
					return 0;
				}
				catch (TargetInvocationException ex) { return translator.throwError(L, luaclr.verifyex(ex.InnerException)); }
				catch (LuaInternalException) { throw; }
				catch (Exception ex) { return luaL.error(L, "Index setter call failed: "+ex.Message); }
			}
		}

		/// <summary>[-0, +0, e]
		/// Tries to set a named property or field.
		/// Returns false if unable to find the named member, true for success
		/// </summary>
		/// <exception cref="ArgumentNullException"><paramref name="targetType"/> is required</exception>
		bool trySetMember(lua.State L, IReflect targetType, object target, BindingFlags bindingType, out string detailMessage)
		{
			Debug.Assert(interpreter.IsSameLua(L));
			Debug.Assert(targetType != null);
			Debug.Assert((target == null) == ((bindingType & BindingFlags.Static  ) == BindingFlags.Static));
			Debug.Assert((target == null) != ((bindingType & BindingFlags.Instance) == BindingFlags.Instance));
			detailMessage = null;   // No error yet

			// If not already a string just return - we don't want to call tostring - which has the side effect of
			// changing the lua typecode to string
			// Note: We don't use isstring because the standard lua C isstring considers either strings or numbers to
			// be true for isstring.
			if (lua.type(L, 2) != LUA.T.STRING)
			{
				detailMessage = "member names must be strings";
				return false;
			}

			// We only look up property names by string
			string fieldName = lua.tostring(L, 2);
			if (fieldName.Length == 0 || !(Char.IsLetter(fieldName[0]) || fieldName[0] == '_'))
			{
				detailMessage = "invalid member name";
				return false;
			}

			// Find our member via reflection or the cache
			MemberInfo member = checkMemberCache(targetType, fieldName) as MemberInfo;
			if (member == null)
			{
				MemberInfo[] members = targetType.GetMember(fieldName, bindingType | luanet.LuaBindingFlags);
				if (members.Length == 0)
				{
					detailMessage = "member '" + fieldName + "' does not exist";
					return false;
				}
				member = members[0];
				if (!(member is MethodBase))
					setMemberCache(targetType, fieldName, member);
			}

			object val;
			switch (member.MemberType)
			{
			case MemberTypes.Field:
				var field = (FieldInfo) member;
				val = translator.getAsType(L, 3, field.FieldType);

				try
				{
					field.SetValue(target, val);
					return true;
				}
				catch (Exception ex)
				{
					detailMessage = "Field write failed: "+ex.Message;
					return false;
				}

			case MemberTypes.Property:
				PropertyInfo property = (PropertyInfo)member;
				val = translator.getAsType(L, 3, property.PropertyType);

				try
				{
					property.SetValue(target, val, null);
					return true;
				}
				catch (TargetInvocationException ex)
				{
					translator.throwError(L, luaclr.verifyex(ex.InnerException));
					return false; // never returns
				}
				catch (Exception ex)
				{
					detailMessage = "Property setter call failed: "+ex.Message;
					return false;
				}

			default:
				detailMessage = "'" + fieldName + "' is not a CLR field or property";
				return false;
			}
		}


		static ProxyType _checktyperef(lua.State L)
		{
			return luaclr.checkref<ProxyType>(L, 1, "type reference expected, got {1}");
		}

		/// <summary>__index metafunction of type references, works on static members.</summary>
		int classIndex(lua.State L)
		{
			using (luanet.entercfunction(L, interpreter))
			{
				var klass = _checktyperef(L);

				switch (lua.type(L, 2))
				{
				case LUA.T.NUMBER:
					var size = lua.tonumber(L, 2);
					Array array;
					try { array = Array.CreateInstance(klass.UnderlyingSystemType, checked((int) size)); }
					catch (Exception ex) { return translator.throwError(L, ex); }
					translator.push(L, array);
					return 1;

				case LUA.T.STRING:
					string methodName = lua.tostring(L, 2);
					return getMember(L, klass, null, methodName, BindingFlags.FlattenHierarchy | BindingFlags.Static);

				default:
					return luaL.typerror(L, 2, "string or number");
				}
			}
		}
		/// <summary>__newindex function of type references, works on static members.</summary>
		int classNewIndex(lua.State L)
		{
			using (luanet.entercfunction(L, interpreter))
			{
				var target = _checktyperef(L);

				string detail;
				if (!trySetMember(L, target, null, BindingFlags.FlattenHierarchy | BindingFlags.Static, out detail))
					return luaL.error(L, detail);

				return 0;
			}
		}
		/// <summary>
		/// __call metafunction of type references.
		/// Searches for and calls a constructor for the type.
		/// Returns nil if the constructor is not found or if the arguments are invalid.
		/// Throws an error if the constructor generates an exception.
		/// </summary>
		int classCall(lua.State L)
		{
			using (luanet.entercfunction(L, interpreter))
			{
				var klass = _checktyperef(L);
				var validConstructor = new MethodCache();
				lua.remove(L, 1);
				var constructors = klass.UnderlyingSystemType.GetConstructors();

				foreach (ConstructorInfo constructor in constructors)
				if (translator.matchParameters(L, constructor, ref validConstructor))
				{
					object result;
					try { result = constructor.Invoke(validConstructor.args); }
					catch (TargetInvocationException ex) { return translator.throwError(L, luaclr.verifyex(ex.InnerException)); }
					catch (Exception ex) { return luaL.error(L, "Constructor call failed: "+ex.Message); }
					translator.push(L, result);
					return 1;
				}
				return luaL.error(L, klass.UnderlyingSystemType+" constructor arguments do not match");
			}
		}
	}
}