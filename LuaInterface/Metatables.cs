using System;
using System.Collections;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LuaInterface.LuaAPI;

namespace LuaInterface
{
	/// <summary>Functions used in the metatables of userdata representing CLR objects</summary>
	/// <remarks>
	/// Author: Fabio Mascarenhas
	/// Version: 1.0
	/// </remarks>
	class MetaFunctions
	{
		/// <summary>__index metafunction for CLR objects. Implemented in Lua. Chunk returns a single value.</summary>
		internal const string luaIndexFunction = @"
			return function(obj,name)
				local meta=getmetatable(obj)
				local cached=meta.cache[name]
				if cached then
					return cached
				else
					local value,isFunc = get_object_member(obj,name)
					if value==nil and type(isFunc)=='string' then error(isFunc,2) end
					if isFunc then
						meta.cache[name]=value
					end
					return value
				end
			end
		";

		private readonly ObjectTranslator translator;
		internal readonly lua.CFunction gcFunction, indexFunction, newindexFunction,
			baseIndexFunction, classIndexFunction, classNewindexFunction,
			execDelegateFunction, callConstructorFunction, toStringFunction;

		public MetaFunctions(ObjectTranslator translator)
		{
			this.translator = translator;
			gcFunction = this.collectObject;
			toStringFunction = this.toString;
			indexFunction = this.getMethod;
			newindexFunction = this.setFieldOrProperty;
			baseIndexFunction = this.getBaseMethod;
			callConstructorFunction = this.callConstructor;
			classIndexFunction = this.getClassMethod;
			classNewindexFunction = this.setClassFieldOrProperty;
			execDelegateFunction = this.runFunctionDelegate;
		}

		/// <summary>[-0, +0, v] Gets the first argument via <see cref="ObjectTranslator.getRawNetObject"/>, throwing a Lua error if it isn't one.</summary>
		object getNetObj(lua.State L)
		{
			Debug.Assert(L == translator.interpreter._L && luanet.infunction(L));
			var obj = translator.getRawNetObject(L, 1);
			if (obj == null)
				luaL.argerror(L, 1, lua.isnoneornil(L, 1) ? "value expected" : "CLR object expected");
			return obj;
		}
		/// <summary>[-0, +0, v] Gets the first argument as type <typeparamref name="T"/> via <see cref="ObjectTranslator.getRawNetObject"/>, throwing a Lua error if it isn't one. If it is the wrong CLR type, <paramref name="extramsg"/> is used for the error message.</summary>
		T getNetObj<T>(lua.State L, string extramsg) where T : class
		{
			var obj = getNetObj(L) as T;
			if (obj == null)
				luaL.argerror(L, 1, extramsg);
			return obj;
		}

		/// <summary>__call metafunction of CLR delegates, retrieves and calls the delegate.</summary>
		private int runFunctionDelegate(lua.State L)
		{
			Debug.Assert(L == translator.interpreter._L && luanet.infunction(L));
			var func = getNetObj<lua.CFunction>(L, "lua_CFunction expected");
			lua.remove(L, 1);
			return func(L);
		}
		/// <summary>__gc metafunction of CLR objects.</summary>
		private int collectObject(lua.State L)
		{
			Debug.Assert(L == translator.interpreter._L && luanet.infunction(L));
			int udata = luanet.rawnetobj(L, 1);
			if (udata != -1)
				translator.collectObject(udata);
			// else Debug.WriteLine("not found: " + udata);
			return 0;
		}
		/// <summary>__tostring metafunction of CLR objects.</summary>
		private int toString(lua.State L)
		{
			Debug.Assert(L == translator.interpreter._L && luanet.infunction(L));
			var obj = getNetObj(L);
			string s;
			try { s = obj.ToString(); }
			catch (Exception ex) { return translator.throwError(L, ex); }
			lua.pushstring(L, s ?? "");
			return 1;
		}


		/// <summary>
		/// Implementation of get_object_member. Called by the __index metafunction of CLR objects in case the method is not cached or it is a field/property/event.
		/// Receives the object and the member name as arguments and returns either the value of the member or a delegate to call it.
		/// If the member does not exist returns nil.
		/// </summary>
		private int getMethod(lua.State L)
		{
			Debug.Assert(L == translator.interpreter._L && luanet.infunction(L));
			object obj = getNetObj(L);

			object index = translator.getObject(L, 2);
			//Type indexType = index.GetType();

			string methodName = index as string;        // will be null if not a string arg
			Type objType = obj.GetType();

			// Handle the most common case, looking up the method by name.

			// CP: This will fail when using indexers and attempting to get a value with the same name as a property of the object,
			// ie: xmlelement['item'] <- item is a property of xmlelement
			try
			{
				// todo: investigate: getMember throws lua errors. all other call sites are also CFunctions and do not use try{}.
				// possible reasons: (1) the author didn't know that Lua errors pass through catch{}
				//                   (2) it is passing unusual input that might generate exceptions not seen in the other use cases
				//                   (3) it is a hasty fix for a bug
				if (methodName != null && isMemberPresent(objType, methodName))
					return getMember(L, objType, obj, methodName, BindingFlags.Instance);
			}
			catch { }
			bool failed = true;

			// Try to access by array if the type is right and index is an int (lua numbers always come across as double)
			if (objType.IsArray && index is double)
			{
				int intIndex = (int)((double)index);
				var aa = (Array) obj;
				if (intIndex >= aa.Length) {
					return pushError(L,"array index out of bounds: "+intIndex + " " + aa.Length);
				}
				object val = aa.GetValue(intIndex);
				translator.push (L,val);
				failed = false;
			}
			else
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
					try
					{
						translator.push(L, mInfo.Invoke(obj, new[]{index}));
						failed = false;
					}
					catch (TargetInvocationException e)
					{
						// Provide a more readable description for the common case of key not found
						if (e.InnerException is KeyNotFoundException)
							return pushError(L, "key '" + index + "' not found ");
						else
							return pushError(L, "exception indexing '" + index + "' " + e.InnerException.Message);
					}
				}
			}
			if (failed) {
				return pushError(L,"cannot find " + index);
			}
			lua.pushboolean(L, false);
			return 2;
		}
		/// <summary>Push an error to be read by __index</summary>
		static int pushError(lua.State L, string msg)
		{
			lua.pushnil(L);
			lua.pushstring(L,msg);
			return 2;
		}


		/// <summary>
		/// __index metafunction of base classes (the base field of Lua tables).
		/// Adds a prefix to the method name to call the base version of the method.
		/// </summary>
		private int getBaseMethod(lua.State L)
		{
			Debug.Assert(L == translator.interpreter._L && luanet.infunction(L));
			object obj = getNetObj(L);

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


		/// <summary>Does this method exist as either an instance or static?</summary>
		bool isMemberPresent(IReflect objType, string methodName)
		{
			Debug.Assert(objType != null && methodName != null);
			object cachedMember = checkMemberCache(objType, methodName);

			if (cachedMember != null)
				return true;

			MemberInfo[] members = objType.GetMember(methodName, BindingFlags.Static | BindingFlags.Instance | luanet.LuaBindingFlags);
			return (members.Length > 0);
		}

		/// <summary>
		/// Pushes the value of a member or a delegate to call it, depending on the type of the member.
		/// Works with static or instance members. Uses reflection to find members,
		/// and stores the reflected MemberInfo object in a cache (indexed by the type of the object and the name of the member).
		/// </summary>
		/// <exception cref="ArgumentNullException"><paramref name="objType"/> and <paramref name="methodName"/></exception>
		private int getMember(lua.State L, IReflect objType, object obj, string methodName, BindingFlags bindingType)
		{
			Debug.Assert(L == translator.interpreter._L);
			Debug.Assert(objType != null && methodName != null);
			bool implicitStatic = false;
			MemberInfo member = null;
			object cachedMember = checkMemberCache(objType, methodName);
			if (cachedMember != null)
			{
				var cachedMethod = cachedMember as lua.CFunction;
				if (cachedMethod != null)
				{
					translator.pushFunction(L, cachedMethod);
					lua.pushboolean(L, true);
					return 2;
				}
				member = (MemberInfo)cachedMember;
			}
			else
			{
				MemberInfo[] members = objType.GetMember(methodName, bindingType | luanet.LuaBindingFlags);
				if (members.Length > 0)
					member = members[0];
				else
				{
					// If we can't find any suitable instance members, try to find them as statics - but we only want to allow implicit static
					// lookups for fields/properties/events -kevinh
					members = objType.GetMember(methodName, bindingType | BindingFlags.Static | luanet.LuaBindingFlags);

					if (members.Length > 0)
					{
						member = members[0];
						implicitStatic = true;
					}
				}
			}
			if (member == null)
			{
				// kevinh - we want to throw an exception because merely returning 'nil' in this case
				// is not sufficient.  valid data members may return nil and therefore there must be some
				// way to know the member just doesn't exist.

				return luaL.error(L, "unknown member name " + methodName);
			}
			switch (member.MemberType)
			{
			case MemberTypes.Field:
				FieldInfo field = (FieldInfo) member;
				try { translator.push(L, field.GetValue(obj)); }
				catch { lua.pushnil(L); }
				break;

			case MemberTypes.Property:
				PropertyInfo property = (PropertyInfo) member;

				try { translator.push(L, property.GetValue(obj, null)); }
				catch (ArgumentException)
				{
					// If we can't find the getter in our class, recurse up to the base class and see if they can help.
					var type = objType as Type;
					if (type != null && (type = type.BaseType) != null)
						return getMember(L, type, obj, methodName, bindingType);
					else
						lua.pushnil(L);
				}
				catch (TargetInvocationException e) { return translator.throwError(L, e.InnerException); }
				break;

			case MemberTypes.Event:
				EventInfo eventInfo = (EventInfo) member;
				translator.push(L, new RegisterEventHandler(translator.pendingEvents, obj, eventInfo));
				break;

			case MemberTypes.NestedType:
				if (implicitStatic)
					return luaL.error(L, "can't pass instance to static field " + methodName);

				var nestedType = (Type) member;
				if (translator.FindType(nestedType))
					translator.pushType(L, nestedType);
				else
					lua.pushnil(L);
				break;

			default: // Member type must be 'method'
				if (implicitStatic)
					return luaL.error(L, "can't pass instance to static method " + methodName);

				var wrapper = new lua.CFunction((new LuaMethodWrapper(translator, objType, methodName, bindingType)).call);

				if (cachedMember == null) setMemberCache(objType, methodName, wrapper);
				translator.pushFunction(L, wrapper);
				lua.pushboolean(L, true);
				return 2;
			}
			
			if (cachedMember == null) setMemberCache(objType, methodName, member);
			// push false because we are NOT returning a function (see luaIndexFunction)
			lua.pushboolean(L, false);
			return 2;
		}

		private readonly Dictionary<IReflect, Dictionary<string, object>> memberCache = new Dictionary<IReflect, Dictionary<string, object>>();

		/// <summary>Checks if a MemberInfo object is cached, returning it or null.</summary><exception cref="ArgumentNullException">All arguments are required.</exception>
		private object checkMemberCache(IReflect objType, string memberName)
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
		private void setMemberCache(IReflect objType, string memberName, object member)
		{
			Debug.Assert(objType != null && memberName != null && member != null);
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
		private int setFieldOrProperty(lua.State L)
		{
			Debug.Assert(L == translator.interpreter._L && luanet.infunction(L));
			object target = getNetObj(L);

			Type type = target.GetType();

			// First try to look up the parameter as a property name
			string detailMessage;
			bool didMember = trySetMember(L, type, target, BindingFlags.Instance, out detailMessage);

			if (didMember)
				return 0;       // Must have found the property name

			// We didn't find a property name, now see if we can use a [] style this accessor to set array contents
			try
			{
				if (type.IsArray && lua.type(L, 2) == LUA.T.NUMBER)
				{
					int index = (int)lua.tonumber(L, 2);

					Array arr = (Array)target;
					object val = translator.getAsType(L, 3, arr.GetType().GetElementType());
					arr.SetValue(val, index);
				}
				else
				{
					// Try to see if we have a this[] accessor
					MethodInfo setter = type.GetMethod("set_Item");
					if (setter == null)
						return luaL.error(L, detailMessage); // Pass the original message from trySetMember because it is probably best

					ParameterInfo[] args = setter.GetParameters();
					Type valueType = args[1].ParameterType;

					// The new val ue the user specified
					object val = translator.getAsType(L, 3, valueType);

					Type indexType = args[0].ParameterType;
					object index = translator.getAsType(L, 2, indexType);

					object[] methodArgs = new object[2];

					// Just call the indexer - if out of bounds an exception will happen
					methodArgs[0] = index;
					methodArgs[1] = val;

					setter.Invoke(target, methodArgs);
				}
			}
			catch (SEHException)
			{
				// If we are seeing a C++ exception - this must actually be for Lua's private use.  Let it handle it
				throw;
			}
			catch (Exception e) { ThrowError(L, e); }
			return 0;
		}

		/// <summary>Tries to set a named property or field</summary>
		/// <returns>false if unable to find the named member, true for success</returns>
		private bool trySetMember(lua.State L, IReflect targetType, object target, BindingFlags bindingType, out string detailMessage)
		{
			Debug.Assert(L == translator.interpreter._L);
			Debug.Assert(targetType != null);
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
				if (members.Length > 0)
				{
					member = members[0];
					setMemberCache(targetType, fieldName, member);
				}
				else
				{
					detailMessage = "member '" + fieldName + "' does not exist";
					return false;
				}
			}

			object val;
			switch (member.MemberType)
			{
			case MemberTypes.Field:
				FieldInfo field = (FieldInfo)member;
				val = translator.getAsType(L, 3, field.FieldType);

				try { field.SetValue(target, val); }
				catch (Exception e) { ThrowError(L, e); }
				// We did a call
				return true;

			case MemberTypes.Property:
				PropertyInfo property = (PropertyInfo)member;
				val = translator.getAsType(L, 3, property.PropertyType);

				try { property.SetValue(target, val, null); }
				catch (Exception e) { ThrowError(L, e); }
				// We did a call
				return true;

			default:
				detailMessage = "'" + fieldName + "' is not a .net field or property";
				return false;
			}
		}


		/// <summary><para>[-0, +0, v] Convert a C# exception into a Lua error. Never returns.</para><para>We try to look into the exception to give the most meaningful description</para></summary>
		void ThrowError(lua.State L, Exception e)
		{
			Debug.Assert(L == translator.interpreter._L);
			Debug.Assert(e != null);

			// If we got inside a reflection show what really happened
			var te = e as TargetInvocationException;
			if (te != null)
				e = te.InnerException;

			translator.throwError(L, e);
		}

		/// <summary>__index metafunction of type references, works on static members.</summary>
		private int getClassMethod(lua.State L)
		{
			Debug.Assert(L == translator.interpreter._L && luanet.infunction(L));
			var klass = getNetObj<IReflect>(L, "type reference expected");

			switch (lua.type(L, 2))
			{
			case LUA.T.NUMBER:
				int size = (int)lua.tonumber(L, 2);
				translator.push(L, Array.CreateInstance(klass.UnderlyingSystemType, size));
				return 1;

			case LUA.T.STRING:
				string methodName = lua.tostring(L, 2);
				return getMember(L, klass, null, methodName, BindingFlags.FlattenHierarchy | BindingFlags.Static);

			default:
				return luaL.typerror(L, 2, "string or number");
			}
		}
		/// <summary>__newindex function of type references, works on static members.</summary>
		private int setClassFieldOrProperty(lua.State L)
		{
			Debug.Assert(L == translator.interpreter._L && luanet.infunction(L));
			IReflect target = getNetObj<IReflect>(L, "type reference expected");

			string detail;
			if (!trySetMember(L, target, null, BindingFlags.FlattenHierarchy | BindingFlags.Static, out detail))
				return luaL.error(L, detail);

			return 0;
		}
		/// <summary>
		/// __call metafunction of type references.
		/// Searches for and calls a constructor for the type.
		/// Returns nil if the constructor is not found or if the arguments are invalid.
		/// Throws an error if the constructor generates an exception.
		/// </summary>
		private int callConstructor(lua.State L)
		{
			Debug.Assert(L == translator.interpreter._L && luanet.infunction(L));
			var validConstructor = new MethodCache();
			var klass = getNetObj<IReflect>(L, "type reference expected");
			lua.remove(L, 1);
			var constructors = klass.UnderlyingSystemType.GetConstructors();

			foreach (ConstructorInfo constructor in constructors)
			if (matchParameters(L, constructor, ref validConstructor))
			{
				try { translator.push(L, constructor.Invoke(validConstructor.args)); }
				catch (TargetInvocationException e) { return translator.throwError(L, e.InnerException); }
				catch { lua.pushnil(L); }
				return 1;
			}
			return luaL.error(L, klass.UnderlyingSystemType+" constructor arguments do not match");
		}

		private static bool IsInteger(double x) {
			return Math.Ceiling(x) == x;
		}


		/// <summary>Note: this disposes <paramref name="luaParamValue"/> if it is a table.</summary>
		internal static Array TableToArray(object luaParamValue, Type paramArrayType)
		{
			Array paramArray;

			var table = luaParamValue as LuaTable;
			if (table == null)
			{
				paramArray = Array.CreateInstance(paramArrayType, 1);
				paramArray.SetValue(luaParamValue, 0);
			}
			else using (table)
			{
				paramArray = Array.CreateInstance(paramArrayType, table.Length);

				table.ForEachI((i, o) =>
				{
					if (paramArrayType == typeof(object) && o is double)
					{
						var d = (double) o;
						if (IsInteger(d))
							o = Convert.ToInt32(d);
					}
					paramArray.SetValue(Convert.ChangeType(o, paramArrayType), i - 1);
				});
			}

			return paramArray;
		}

		/// <summary>
		/// Matches a method against its arguments in the Lua stack.
		/// Returns if the match was successful.
		/// It it was also returns the information necessary to invoke the method.
		/// </summary>
		internal bool matchParameters(lua.State L, MethodBase method, ref MethodCache methodCache)
		{
			Debug.Assert(L == translator.interpreter._L);
			bool isMethod = true;
			int currentLuaParam = 1;
			int nLuaParams = lua.gettop(L);
			var paramList = new ArrayList();
			var outList = new List<int>();
			var argTypes = new List<MethodArgs>();
			foreach (ParameterInfo currentNetParam in method.GetParameters())
			{
				if (!currentNetParam.IsIn && currentNetParam.IsOut)  // Skips out params
				{
					outList.Add(paramList.Add(null));
					continue;
				}

				if (currentLuaParam > nLuaParams) // Adds optional parameters
				{
					if (currentNetParam.IsOptional)
					{
						paramList.Add(currentNetParam.DefaultValue);
						continue;
					}
					else
					{
						isMethod = false;
						break;
					}
				}

				// Type checking
				ExtractValue extractValue;
				try { extractValue = translator.typeChecker.checkType(L, currentLuaParam, currentNetParam.ParameterType); }
				catch
				{
					extractValue = null;
					Debug.WriteLine("Type wasn't correct");
				}
				if (extractValue != null)  
				{
					int index = paramList.Add(extractValue(L, currentLuaParam));

					argTypes.Add(new MethodArgs {index = index, extractValue = extractValue});

					if (currentNetParam.ParameterType.IsByRef)
						outList.Add(index);
					currentLuaParam++;
					continue;
				}

				// Type does not match, ignore if the parameter is optional
				if (_IsParamsArray(L, currentLuaParam, currentNetParam, out extractValue))
				{
					object luaParamValue = extractValue(L, currentLuaParam);
					Type paramArrayType = currentNetParam.ParameterType.GetElementType();

					Array paramArray = TableToArray(luaParamValue, paramArrayType);
					int index = paramList.Add(paramArray);

					argTypes.Add(new MethodArgs
					{
						index = index,
						extractValue = extractValue,
						isParamsArray = true,
						paramsArrayType = paramArrayType,
					});

					currentLuaParam++;
					continue;
				}

				if (currentNetParam.IsOptional)
				{
					paramList.Add(currentNetParam.DefaultValue);
					continue;
				}

				// No match
				isMethod = false;
				break;
			}
			if (currentLuaParam != nLuaParams + 1) // Number of parameters does not match
				isMethod = false;
			if (isMethod)
			{
				methodCache.args = paramList.ToArray();
				methodCache.cachedMethod = method;
				methodCache.outList = outList.ToArray();
				methodCache.argTypes = argTypes.ToArray();
			}
			return isMethod;
		}

		private bool _IsParamsArray(lua.State L, int index, ParameterInfo param, out ExtractValue extractValue)
		{
			Debug.Assert(L == translator.interpreter._L);
			extractValue = null;

			if (param.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0)
			switch (lua.type(L, index))
			{
			case LUA.T.NONE:
				Debug.WriteLine("_IsParamsArray: Could not retrieve lua type.");
				return false;

			case LUA.T.TABLE:
				extractValue = translator.typeChecker.getExtractor(typeof(LuaTable));
				if (extractValue != null)
					return true;
				break;

			default:
				Type elementType = param.ParameterType.GetElementType();
				if (elementType == null)
					break; // not an array; invalid ParamArrayAttribute
				try
				{
					extractValue = translator.typeChecker.checkType(L, index, elementType);
					if (extractValue != null)
						return true;
				}
				catch (Exception ex) { Debug.WriteLine(String.Format("_IsParamsArray: checkType({0}) threw exception: {1}", elementType.FullName, ex.ToString())); }
				break;
			}

			Debug.WriteLine("Type wasn't Params object.");
			return false;
		}
	}
}