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
		/// <summary>__index metafunction for CLR objects. Implemented in Lua.</summary>
		internal const string luaIndexFunction =
	@"
		local function index(obj,name)
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
	return index";

		private readonly ObjectTranslator translator;
		private readonly Hashtable memberCache = new Hashtable();
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

		/// <summary>__call metafunction of CLR delegates, retrieves and calls the delegate.</summary>
		private int runFunctionDelegate(lua.State L)
		{
			Debug.Assert(L == translator.interpreter._L && luanet.infunction(L));
			var func = (lua.CFunction)translator.getRawNetObject(L, 1);
			lua.remove(L, 1);
			return func(L);
		}
		/// <summary>__gc metafunction of CLR objects.</summary>
		private int collectObject(lua.State L)
		{
			Debug.Assert(L == translator.interpreter._L && luanet.infunction(L));
			int udata = luanet.rawnetobj(L, 1);
			if (udata != -1)
			{
				translator.collectObject(udata);
			}
			// else Debug.WriteLine("not found: " + udata);
			return 0;
		}
		/// <summary>__tostring metafunction of CLR objects.</summary>
		private int toString(lua.State L)
		{
			Debug.Assert(L == translator.interpreter._L && luanet.infunction(L));
			object obj = translator.getRawNetObject(L, 1);
			if (obj == null) return luaL.error(L, "argument is not a CLR object");
			lua.pushstring(L, obj.ToString());
			return 1;
		}


		/// <summary>
		/// Debug tool to dump the lua stack
		/// </summary>
		/// FIXME, move somewhere else
		public static void dumpStack(ObjectTranslator translator, lua.State L)
		{
			Debug.Assert(L == translator.interpreter._L);
			int depth = lua.gettop(L);

			Debug.WriteLine("lua stack depth: " + depth);
			for (int i = 1; i <= depth; i++)
			{
				var type = lua.type(L, i);
				// we dump stacks when deep in calls, calling typename while the stack is in flux can fail sometimes, so manually check for key types
				string typestr = (type == LUA.T.TABLE) ? "table" : lua.typename(L, type);

				string strrep = lua.tostring(L, i);
				if (type == LUA.T.USERDATA)
				{
					object obj = translator.getRawNetObject(L, i);
					strrep = obj.ToString();
				}

				Debug.Print("{0}: ({1}) {2}", i, typestr, strrep);
			}
		}

		/// <summary>
		/// Called by the __index metafunction of CLR objects in case the method is not cached or it is a field/property/event.
		/// Receives the object and the member name as arguments and returns either the value of the member or a delegate to call it.
		/// If the member does not exist returns nil.
		/// </summary>
		private int getMethod(lua.State L)
		{
			Debug.Assert(L == translator.interpreter._L && luanet.infunction(L));
			object obj = translator.getRawNetObject(L, 1);
			if (obj == null)
				return luaL.error(L, "trying to index an invalid object reference");

			object index = translator.getObject(L, 2);
			Type indexType = index.GetType(); //* not used

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
					return getMember(L, objType, obj, methodName, BindingFlags.Instance | BindingFlags.IgnoreCase);
			}
			catch { }
			bool failed = true;

			// Try to access by array if the type is right and index is an int (lua numbers always come across as double)
			if (objType.IsArray && index is double)
			{
				int intIndex = (int)((double)index);
				var aa = (Array) obj;
				if (intIndex >= aa.Length) {
					return ObjectTranslator.pushError(L,"array index out of bounds: "+intIndex + " " + aa.Length);
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
							return ObjectTranslator.pushError(L, "key '" + index + "' not found ");
						else
							return ObjectTranslator.pushError(L, "exception indexing '" + index + "' " + e.InnerException.Message);
					}
				}
			}
			if (failed) {
				return ObjectTranslator.pushError(L,"cannot find " + index);
			}
			lua.pushboolean(L, false);
			return 2;
		}


		/// <summary>
		/// __index metafunction of base classes (the base field of Lua tables).
		/// Adds a prefix to the method name to call the base version of the method.
		/// </summary>
		private int getBaseMethod(lua.State L)
		{
			Debug.Assert(L == translator.interpreter._L && luanet.infunction(L));
			object obj = translator.getRawNetObject(L, 1);
			if (obj == null)
				return luaL.error(L, "trying to index an invalid object reference");

			string methodName = lua.tostring(L, 2);
			if (methodName == null)
			{
				lua.pushnil(L);
				lua.pushboolean(L, false);
				return 2;
			}
			getMember(L, obj.GetType(), obj, "__luaInterface_base_" + methodName, BindingFlags.Instance | BindingFlags.IgnoreCase);
			lua.pop(L,1);
			if (lua.type(L, -1) == LUA.T.NIL)
			{
				lua.pop(L,1);
				return getMember(L, obj.GetType(), obj, methodName, BindingFlags.Instance | BindingFlags.IgnoreCase);
			}
			lua.pushboolean(L, false);
			return 2;
		}


		/// <summary>Does this method exist as either an instance or static?</summary>
		bool isMemberPresent(IReflect objType, string methodName)
		{
			object cachedMember = checkMemberCache(memberCache, objType, methodName);

			if (cachedMember != null)
				return true;

			//CP: Removed NonPublic binding search
			MemberInfo[] members = objType.GetMember(methodName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase/* | BindingFlags.NonPublic*/);
			return (members.Length > 0);
		}

		/// <summary>
		/// Pushes the value of a member or a delegate to call it, depending on the type of the member.
		/// Works with static or instance members. Uses reflection to find members,
		/// and stores the reflected MemberInfo object in a cache (indexed by the type of the object and the name of the member).
		/// </summary>
		private int getMember(lua.State L, IReflect objType, object obj, string methodName, BindingFlags bindingType)
		{
			Debug.Assert(L == translator.interpreter._L);
			bool implicitStatic = false;
			MemberInfo member = null;
			object cachedMember = checkMemberCache(memberCache, objType, methodName);
			//object cachedMember=null;
			if (cachedMember is lua.CFunction)
			{
				translator.pushFunction(L, (lua.CFunction)cachedMember);
				lua.pushboolean(L, true);
				return 2;
			}
			else if (cachedMember != null)
			{
				member = (MemberInfo)cachedMember;
			}
			else
			{
				//CP: Removed NonPublic binding search
				MemberInfo[] members = objType.GetMember(methodName, bindingType | BindingFlags.Public | BindingFlags.IgnoreCase/*| BindingFlags.NonPublic*/);
				if (members.Length > 0)
					member = members[0];
				else
				{
					// If we can't find any suitable instance members, try to find them as statics - but we only want to allow implicit static
					// lookups for fields/properties/events -kevinh
					//CP: Removed NonPublic binding search and made case insensitive
					members = objType.GetMember(methodName, bindingType | BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase/*| BindingFlags.NonPublic*/);

					if (members.Length > 0)
					{
						member = members[0];
						implicitStatic = true;
					}
				}
			}
			if (member != null)
			{
				if (member.MemberType == MemberTypes.Field)
				{
					FieldInfo field = (FieldInfo)member;
					if (cachedMember == null) setMemberCache(memberCache, objType, methodName, member);
					try
					{
						translator.push(L, field.GetValue(obj));
					}
					catch
					{
						lua.pushnil(L);
					}
				}
				else if (member.MemberType == MemberTypes.Property)
				{
					PropertyInfo property = (PropertyInfo)member;
					if (cachedMember == null) setMemberCache(memberCache, objType, methodName, member);
					try
					{
						translator.push(L, property.GetValue(obj, null));
					}
					catch (ArgumentException)
					{
						// If we can't find the getter in our class, recurse up to the base class and see
						// if they can help.

						if (objType is Type && !(((Type)objType) == typeof(object)))
							return getMember(L, ((Type)objType).BaseType, obj, methodName, bindingType);
						else
							lua.pushnil(L);
					}
					catch (TargetInvocationException e) { return translator.throwError(L, e.InnerException); }
				}
				else if (member.MemberType == MemberTypes.Event)
				{
					EventInfo eventInfo = (EventInfo)member;
					if (cachedMember == null) setMemberCache(memberCache, objType, methodName, member);
					translator.push(L, new RegisterEventHandler(translator.pendingEvents, obj, eventInfo));
				}
				else if (!implicitStatic)
				{
					if (member.MemberType == MemberTypes.NestedType)
					{
						// kevinh - added support for finding nested types

						// cache us
						if (cachedMember == null) setMemberCache(memberCache, objType, methodName, member);

						// Find the name of our class
						string name = member.Name;
						Type dectype = member.DeclaringType;

						// Build a new long name and try to find the type by name
						string longname = dectype.FullName + "+" + name;
						Type nestedType = translator.FindType(longname);

						translator.pushType(L, nestedType);
					}
					else
					{
						// Member type must be 'method'
						var wrapper = new lua.CFunction((new LuaMethodWrapper(translator, objType, methodName, bindingType)).call);

						if (cachedMember == null) setMemberCache(memberCache, objType, methodName, wrapper);
						translator.pushFunction(L, wrapper);
						lua.pushboolean(L, true);
						return 2;
					}
				}
				else
				{
					// If we reach this point we found a static method, but can't use it in this context because the user passed in an instance
					return luaL.error(L, "can't pass instance to static method " + methodName);
				}
			}
			else
			{
				// kevinh - we want to throw an exception because merely returning 'nil' in this case
				// is not sufficient.  valid data members may return nil and therefore there must be some
				// way to know the member just doesn't exist.

				return luaL.error(L, "unknown member name " + methodName);
			}

			// push false because we are NOT returning a function (see luaIndexFunction)
			lua.pushboolean(L, false);
			return 2;
		}
		/// <summary>Checks if a MemberInfo object is cached, returning it or null.</summary>
		private static object checkMemberCache(Hashtable memberCache, IReflect objType, string memberName)
		{
			var members = (Hashtable)memberCache[objType];
			if (members != null)
				return members[memberName];
			else
				return null;
		}
		/// <summary>Stores a MemberInfo object in the member cache.</summary>
		private static void setMemberCache(Hashtable memberCache, IReflect objType, string memberName, object member)
		{
			var members = (Hashtable)memberCache[objType];
			if (members == null)
			{
				members = new Hashtable();
				memberCache[objType] = members;
			}
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
			object target = translator.getRawNetObject(L, 1);
			if (target == null)
				return luaL.error(L, "trying to index and invalid object reference");

			Type type = target.GetType();

			// First try to look up the parameter as a property name
			string detailMessage;
			bool didMember = trySetMember(L, type, target, BindingFlags.Instance | BindingFlags.IgnoreCase, out detailMessage);

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
			detailMessage = null;   // No error yet

			// If not already a string just return - we don't want to call tostring - which has the side effect of
			// changing the lua typecode to string
			// Note: We don't use isstring because the standard lua C isstring considers either strings or numbers to
			// be true for isstring.
			if (lua.type(L, 2) != LUA.T.STRING)
			{
				detailMessage = "property names must be strings";
				return false;
			}

			// We only look up property names by string
			string fieldName = lua.tostring(L, 2);
			if (fieldName == null || fieldName.Length < 1 || !(char.IsLetter(fieldName[0]) || fieldName[0] == '_'))
			{
				detailMessage = "invalid property name";
				return false;
			}

			// Find our member via reflection or the cache
			MemberInfo member = (MemberInfo)checkMemberCache(memberCache, targetType, fieldName);
			if (member == null)
			{
				//CP: Removed NonPublic binding search and made case insensitive
				MemberInfo[] members = targetType.GetMember(fieldName, bindingType | BindingFlags.Public | BindingFlags.IgnoreCase/*| BindingFlags.NonPublic*/);
				if (members.Length > 0)
				{
					member = members[0];
					setMemberCache(memberCache, targetType, fieldName, member);
				}
				else
				{
					detailMessage = "field or property '" + fieldName + "' does not exist";
					return false;
				}
			}

			if (member.MemberType == MemberTypes.Field)
			{
				FieldInfo field = (FieldInfo)member;
				object val = translator.getAsType(L, 3, field.FieldType);
				try
				{
					field.SetValue(target, val);
				}
				catch (Exception e) { ThrowError(L, e); }
				// We did a call
				return true;
			}
			else if (member.MemberType == MemberTypes.Property)
			{
				PropertyInfo property = (PropertyInfo)member;
				object val = translator.getAsType(L, 3, property.PropertyType);
				try
				{
					property.SetValue(target, val, null);
				}
				catch (Exception e) { ThrowError(L, e); }
				// We did a call
				return true;
			}

			detailMessage = "'" + fieldName + "' is not a .net field or property";
			return false;
		}


		/// <summary>Writes to fields or properties, either static or instance. Throws an error if the operation is invalid.</summary>
		private int setMember(lua.State L, IReflect targetType, object target, BindingFlags bindingType)
		{
			Debug.Assert(L == translator.interpreter._L);
			string detail;
			bool success = trySetMember(L, targetType, target, bindingType, out detail);

			if (!success)
				return luaL.error(L, detail);

			return 0;
		}

		/// <summary><para>Convert a C# exception into a Lua error. Never returns.</para><para>We try to look into the exception to give the most meaningful description</para></summary>
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
			IReflect klass;
			object obj = translator.getRawNetObject(L, 1);
			if (!(obj is IReflect))
				return luaL.error(L, "trying to index an invalid type reference");

			klass = (IReflect)obj;
			if (lua.isnumber(L, 2))
			{
				int size = (int)lua.tonumber(L, 2);
				translator.push(L, Array.CreateInstance(klass.UnderlyingSystemType, size));
				return 1;
			}
			else
			{
				string methodName = lua.tostring(L, 2);
				if (methodName == null)
				{
					lua.pushnil(L);
					return 1;
				} //CP: Ignore case
				else return getMember(L, klass, null, methodName, BindingFlags.FlattenHierarchy | BindingFlags.Static | BindingFlags.IgnoreCase);
			}
		}
		/// <summary>__newindex function of type references, works on static members.</summary>
		private int setClassFieldOrProperty(lua.State L)
		{
			Debug.Assert(L == translator.interpreter._L && luanet.infunction(L));
			IReflect target;
			object obj = translator.getRawNetObject(L, 1);
			if (!(obj is IReflect))
				return luaL.error(L, "trying to index an invalid type reference");

			target = (IReflect)obj;
			return setMember(L, target, null, BindingFlags.FlattenHierarchy | BindingFlags.Static | BindingFlags.IgnoreCase);
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
			IReflect klass;
			object obj = translator.getRawNetObject(L, 1);
			if (!(obj is IReflect))
				return luaL.error(L, "trying to call constructor on an invalid type reference");

			else klass = (IReflect)obj;
			lua.remove(L, 1);
			ConstructorInfo[] constructors = klass.UnderlyingSystemType.GetConstructors();
			foreach (ConstructorInfo constructor in constructors)
			{
				bool isConstructor = matchParameters(L, constructor, ref validConstructor);
				if (isConstructor)
				{
					try
					{
						translator.push(L, constructor.Invoke(validConstructor.args));
					}
					catch (TargetInvocationException e) { return translator.throwError(L, e.InnerException); }
					catch { lua.pushnil(L); }
					return 1;
				}
			}

			string constructorName = (constructors.Length == 0) ? "unknown" : constructors[0].Name;

			return luaL.error(L, String.Format("{0} does not contain constructor({1}) argument match",
				klass.UnderlyingSystemType,
				constructorName));
		}

		private static bool IsInteger(double x) {
			return Math.Ceiling(x) == x;
		}


		/// <summary>Note: this disposes <paramref name="luaParamValue"/> if it is a table.</summary>
		internal Array TableToArray(object luaParamValue, Type paramArrayType)
		{
			Array paramArray;

			var table = luaParamValue as LuaTable;
			if (table != null) using (table)  {
				Debug.Assert(table.Owner._L == translator.interpreter._L);
				paramArray = Array.CreateInstance(paramArrayType, table.Length);

				table.ForEachI((i, o) =>
				{
					if (paramArrayType == typeof(object) && o is double)
					{
						var d = (double) o;
						if (IsInteger(d))
							o = Convert.ToInt32(d);
					}
					paramArray.SetValue(Convert.ChangeType(o, paramArrayType), i-1);
				 });
			} else {
				paramArray = Array.CreateInstance(paramArrayType, 1);
				paramArray.SetValue(luaParamValue, 0);
			}

			return paramArray;
		}

		/// <summary>
		/// Matches a method against its arguments in the Lua stack.
		/// Returns if the match was succesful.
		/// It it was also returns the information necessary to invoke the method.
		/// </summary>
		internal bool matchParameters(lua.State L, MethodBase method, ref MethodCache methodCache)
		{
			Debug.Assert(L == translator.interpreter._L);
			ExtractValue extractValue;
			bool isMethod = true;
			ParameterInfo[] paramInfo = method.GetParameters();
			int currentLuaParam = 1;
			int nLuaParams = lua.gettop(L);
			var paramList = new ArrayList();
			var outList = new List<int>();
			var argTypes = new List<MethodArgs>();
			foreach (ParameterInfo currentNetParam in paramInfo)
			{
				if (!currentNetParam.IsIn && currentNetParam.IsOut)  // Skips out params
				{
					outList.Add(paramList.Add(null));
				}
				else if (currentLuaParam > nLuaParams) // Adds optional parameters
				{
					if (currentNetParam.IsOptional)
					{
						paramList.Add(currentNetParam.DefaultValue);
					}
					else
					{
						isMethod = false;
						break;
					}
				}
				else if (_IsTypeCorrect(L, currentLuaParam, currentNetParam, out extractValue))  // Type checking
				{
					int index = paramList.Add(extractValue(L, currentLuaParam));

					argTypes.Add(new MethodArgs {index = index, extractValue = extractValue});

					if (currentNetParam.ParameterType.IsByRef)
						outList.Add(index);
					currentLuaParam++;
				}  // Type does not match, ignore if the parameter is optional
				else if (_IsParamsArray(L, currentLuaParam, currentNetParam, out extractValue))
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
				}
				else if (currentNetParam.IsOptional)
				{
					paramList.Add(currentNetParam.DefaultValue);
				}
				else  // No match
				{
					isMethod = false;
					break;
				}
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

		/// <summary>
		/// CP: Fix for operator overloading failure
		/// Returns true if the type is set and assigns the extract value
		/// </summary>
		private bool _IsTypeCorrect(lua.State L, int currentLuaParam, ParameterInfo currentNetParam, out ExtractValue extractValue)
		{
			Debug.Assert(L == translator.interpreter._L);
			try
			{
				return (extractValue = translator.typeChecker.checkType(L, currentLuaParam, currentNetParam.ParameterType)) != null;
			}
			catch
			{
				extractValue = null;
				Debug.WriteLine("Type wasn't correct");
				return false;
			}
		}

		private bool _IsParamsArray(lua.State L, int currentLuaParam, ParameterInfo currentNetParam, out ExtractValue extractValue)
		{
			Debug.Assert(L == translator.interpreter._L);
			extractValue = null;

			if (currentNetParam.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0)
			{
				var luatype = lua.type(L, currentLuaParam);
				if (luatype == LUA.T.NONE)
				{
					Debug.WriteLine("Could not retrieve lua type while attempting to determine params Array Status.");
					extractValue = null;
					return false;
				}

				if (luatype == LUA.T.TABLE)
				{
					try
					{
						extractValue = translator.typeChecker.getExtractor(typeof(LuaTable));
					}
					catch (Exception ex)
					{
						Debug.WriteLine("An error occurred during an attempt to retrieve a LuaTable extractor while checking for params array status." + ex.ToString());
					}

					if (extractValue != null)
					{
						return true;
					}
				}
				else
				{
					Type paramElementType = currentNetParam.ParameterType.GetElementType();

					try
					{
						extractValue = translator.typeChecker.checkType(L, currentLuaParam, paramElementType);
					}
					catch (Exception ex)
					{
						Debug.WriteLine(string.Format("An error occurred during an attempt to retrieve an extractor ({0}) while checking for params array status:{1}", paramElementType.FullName, ex.ToString()));
					}

					if (extractValue != null)
					{
						return true;
					}
				}
			}

			Debug.WriteLine("Type wasn't Params object.");

			return false;
		}
	}
}