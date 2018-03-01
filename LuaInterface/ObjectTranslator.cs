using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;
using LuaInterface.LuaAPI;

namespace LuaInterface
{
	/// <summary>Passes objects from the CLR to Lua and vice-versa</summary>
	/// <remarks>Author: Fabio Mascarenhas</remarks>
	class ObjectTranslator
	{
		internal readonly CheckType typeChecker;
		internal readonly Lua interpreter;

		readonly MetaFunctions metaFunctions;
		#if INSECURE
		readonly lua.CFunction _loadAssembly, _makeObject, _freeObject;
		#endif
		readonly lua.CFunction _importType, _getMethodBysig, _getConstructorBysig, _ctype, _enum;

		internal readonly EventHandlerContainer pendingEvents = new EventHandlerContainer();

		public ObjectTranslator(lua.State L, Lua interpreter)
		{
			Debug.Assert(interpreter.IsSameLua(L));
			luanet.checkstack(L, 1, "new ObjectTranslator");
			this.interpreter = interpreter;
			typeChecker      = new CheckType(interpreter);
			metaFunctions    = new MetaFunctions(L, this);

			StackAssert.Start(L);
			#if INSECURE
			lua.pushcfunction(L,_loadAssembly        = this.loadAssembly       ); lua.setglobal(L,"load_assembly");
			lua.pushcfunction(L,_makeObject          = this.makeObject         ); lua.setglobal(L,"make_object");
			lua.pushcfunction(L,_freeObject          = this.freeObject         ); lua.setglobal(L,"free_object");
			#endif
			lua.pushcfunction(L,_importType          = this.importType         ); lua.setglobal(L,"import_type");
			lua.pushcfunction(L,_getMethodBysig      = this.getMethodBysig     ); lua.setglobal(L,"get_method_bysig");
			lua.pushcfunction(L,_getConstructorBysig = this.getConstructorBysig); lua.setglobal(L,"get_constructor_bysig");
			lua.pushcfunction(L,_ctype               = this.ctype              ); lua.setglobal(L,"ctype");
			lua.pushcfunction(L,_enum                = this.@enum              ); lua.setglobal(L,"enum");
			StackAssert.End();
		}
		/// <summary>[-0, +0, v] Passes errors (argument e) to the Lua interpreter. This function throws a Lua exception, and therefore never returns.</summary>
		internal int throwError(lua.State L, Exception ex)
		{
			Debug.Assert(interpreter.IsSameLua(L));
			Debug.Assert(ex != null);
			// Determine the position in the script where the exception was triggered
			// Stack frame #0 is our C# wrapper, so not very interesting to the user
			// Stack frame #1 must be the lua code that called us, so that's what we want to use
			string errLocation = luanet.where(L, 1);

			// Wrap generic .NET exception as an InnerException and store the error location
			ex = new LuaScriptException(ex, errLocation);

			// don't need to check the stack, same pattern as luaL_error
			push(L, ex);
			return lua.error(L);
		}

		readonly List<Assembly> loaded_assemblies = new List<Assembly>();
		readonly Dictionary<string, Type> loaded_types = new Dictionary<string, Type>();

		/// <summary>Implementation of load_assembly. Throws an error if the assembly is not found.</summary>
		int loadAssembly(lua.State L)
		{
			Debug.Assert(interpreter.IsSameLua(L) && luanet.infunction(L));
			string assemblyName = luaL.checkstring(L,1);
			try
			{
				Assembly assembly = null;
				try
				{
					#pragma warning disable 618 // LoadWithPartialName deprecated. No alternative exists.
					assembly = Assembly.LoadWithPartialName(assemblyName);
					#pragma warning restore 618
				}
				catch (BadImageFormatException) {} // The assemblyName was invalid.  It is most likely a path.

				if (assembly == null)
					assembly = Assembly.Load(AssemblyName.GetAssemblyName(assemblyName));

				if (assembly != null)
					LoadAssembly(assembly);

				return 0;
			}
			catch (Exception ex) { return throwError(L,ex); }
		}
		internal void LoadAssembly(Assembly assembly)
		{
			Debug.Assert(assembly != null);
			if (!loaded_assemblies.Contains(assembly))
				loaded_assemblies.Add(assembly);
		}
		/// <exception cref="NullReferenceException"><paramref name="t"/> is required</exception>
		/// <exception cref="ArgumentException">The type is already registered or another type with the same <see cref="Type.FullName"/> is already registered.</exception>
		internal void LoadType(Type t, bool and_nested)
		{
			Debug.Assert(t != null);
			loaded_types.Add(t.FullName, t);
			if (!and_nested) return;
			foreach (var nested in t.GetNestedTypes(luanet.LuaBindingFlags | BindingFlags.FlattenHierarchy))
				LoadType(nested, true);
		}

		/// <exception cref="NullReferenceException"><paramref name="t"/> is required</exception>
		internal bool FindType(Type t)
		{
			Debug.Assert(t != null);
			if (loaded_assemblies.Contains(t.Assembly))
				return true;
			Type found;
			if (!loaded_types.TryGetValue(t.FullName, out found))
				return false;
			return t == found;
		}
		/// <summary>[-0, +0, e]</summary>
		internal Type FindType(lua.State L, string className)
		{
			try
			{
				Type t;
				foreach (var assembly in loaded_assemblies)
					if ((t = assembly.GetType(className)) != null) // can throw exceptions in circumstances we cannot control
						return t;

				loaded_types.TryGetValue(className, out t); // null if not found
				return t;
			}
			catch (Exception ex) { throwError(L, ex); return null; }
		}

		/// <summary>Implementation of import_type. Returns nil if the type is not found.</summary>
		int importType(lua.State L)
		{
			Debug.Assert(interpreter.IsSameLua(L) && luanet.infunction(L));
			string className = luaL.checkstring(L,1);
			Type klass = FindType(L, className);
			if (klass != null)
				pushType(L,klass);
			else
				lua.pushnil(L);
			return 1;
		}
		/// <summary>
		/// Implementation of make_object.
		/// Registers a table (first argument in the stack) as an object subclassing the type passed as second argument in the stack.
		/// </summary>
		int makeObject(lua.State L)
		{
			Debug.Assert(interpreter.IsSameLua(L) && luanet.infunction(L));
#if __NOGEN__
			return luaL.error(L,"Tables as Objects not implemented");
#else
			luaL.checktype(L, 1, LUA.T.TABLE);

			var superclassName = luaL.checkstring(L, 2);
			Type klass = FindType(L, superclassName);
			if (klass == null)
				return luaL.argerror(L, 2, "can not find superclass '" + superclassName + "'");

			// Creates and pushes the object in the stack, setting
			// it as the indexer of the first argument
			object obj = CodeGeneration.Instance.GetClassInstance(klass, new LuaTable(L, interpreter, 1));
			pushObject(L, obj);
			lua.createtable(L, 0, 2);
			lua.pushvalue(L, -2); lua.setfield(L, -2, "__index");
			lua.pushvalue(L, -2); lua.setfield(L, -2, "__newindex");
			lua.setmetatable(L, 1);
			// Pushes the object again, this time as the base field
			// of the table and with the luaNet_searchbase metatable
			lua.pushstring(L, "base");

			luaclr.newref(L, obj);
			luaL.getmetatable(L, "luaNet_searchbase");
			Debug.Assert(luaclr.isrefmeta(L, -1));
			lua.setmetatable(L, -2);

			lua.rawset(L, 1); // t["base"] = new luaNet_searchbase(obj)
			return 0;
#endif
		}
		/// <summary>
		/// Implementation of free_object.
		/// Clears the metatable and the base field, freeing the created object for garbage-collection
		/// </summary>
		int freeObject(lua.State L)
		{
			Debug.Assert(interpreter.IsSameLua(L) && luanet.infunction(L));
			if (!lua.getmetatable(L, 1))
				return luaL.argerror(L, 1, "invalid table");
			try
			{
				lua.getfield(L,-1,"__index");
				var obj = luaclr.toref(L,-1);
				if (obj == null) return luaL.argerror(L, 1, "invalid table");

				FieldInfo luaTableField = obj.GetType().GetField("__luaInterface_luaTable");
				if (luaTableField == null) return luaL.argerror(L, 1, "invalid table");

				var table = luaTableField.GetValue(obj) as LuaTable;
				if (table == null || !table.Owner.IsSameLua(L)) return luaL.argerror(L, 1, "invalid table");
				table.Dispose();
				luaTableField.SetValue(obj,null);

				lua.pushnil(L); lua.setmetatable(L,1);
				lua.pushnil(L); lua.setfield(L,1,"base");
			}
			catch (LuaInternalException) { throw; }
			catch(Exception ex) { return throwError(L,ex); }
			return 0;
		}
		/// <summary>Implementation of get_method_bysig.</summary>
		int getMethodBysig(lua.State L)
		{
			Debug.Assert(interpreter.IsSameLua(L) && luanet.infunction(L));
			object target = luaclr.checkref(L, 1);
			string method_name = luaL.checkstring(L,2);

			IReflect klass;
			if (luanet.hasmetatable(L, 1, "luaNet_class"))
			{
				Debug.Assert(target is ProxyType);
				klass = (ProxyType) target;
				target = null;
			}
			else
			{
				klass = target.GetType();
			}

			var signature = new Type[lua.gettop(L)-2];
			for (int i = 0; i < signature.Length; ++i)
				signature[i] = FindType(L, luaL.checkstring(L,i+3)); // todo: would be useful to also accept type references
			try
			{
				var method = klass.GetMethod(method_name,BindingFlags.Static | BindingFlags.Instance |
					BindingFlags.FlattenHierarchy | luanet.LuaBindingFlags, null, signature, null);
				if (method == null)
					return luaL.error(L, "Method with the specified name and signature was not found.");
				luaclr.pushcfunction(L, new LuaMethodWrapper(this, target, klass, method).call);
				return 1;
			}
			catch (LuaInternalException) { throw; }
			catch (Exception ex) { return throwError(L,ex); }
		}
		/// <summary>Implementation of get_constructor_bysig.</summary>
		int getConstructorBysig(lua.State L)
		{
			Debug.Assert(interpreter.IsSameLua(L) && luanet.infunction(L));
			var klass = (ProxyType) luaclr.checkref(L, 1, "luaNet_class");

			var signature = new Type[lua.gettop(L)-1];
			for (int i = 0; i < signature.Length; ++i)
				signature[i] = FindType(L, lua.tostring(L,i+2));
			try
			{
				var constructor = klass.UnderlyingSystemType.GetConstructor(signature);
				if (constructor == null)
					return luaL.error(L, "Constructor with the specified signature was not found.");
				luaclr.pushcfunction(L, new LuaMethodWrapper(this, null, klass, constructor).call);
				return 1;
			}
			catch (LuaInternalException) { throw; }
			catch (Exception ex) { return throwError(L,ex); }
		}

		static Type _classType(lua.State L, int narg)
		{
			var pt = luaclr.checkref(L, narg, "luaNet_class");
			Debug.Assert(pt is ProxyType);
			return ((ProxyType) pt).UnderlyingSystemType;
		}

		/// <summary>Implementation of ctype.</summary>
		int ctype(lua.State L)
		{
			Debug.Assert(interpreter.IsSameLua(L) && luanet.infunction(L));
			pushObject(L, _classType(L,1));
			return 1;
		}

		/// <summary>Implementation of enum.</summary>
		int @enum(lua.State L)
		{
			Debug.Assert(interpreter.IsSameLua(L) && luanet.infunction(L));
			Type t = _classType(L,1);
			if (!t.IsEnum)
				return luaL.argerror(L, 1, "enum type reference expected");

			object res;
			switch (lua.type(L,2))
			{
			case LUA.T.NUMBER:
				res = Enum.ToObject(t, (int) lua.tonumber(L,2));
				break;
			case LUA.T.STRING:
				try { res = Enum.Parse(t, lua.tostring(L,2)); }
				catch (ArgumentException e) { return luaL.argerror(L, 2, e.Message); }
				break;
			default:
				return luaL.typerror(L, 2, "number or string");
			}
			pushObject(L,res);
			return 1;
		}

		/// <summary>[-0, +1, m] Pushes a type reference into the stack</summary>
		internal static void pushType(lua.State L, Type t)
		{
			Debug.Assert(Lua.GetOwner(L) != null && t != null);
			luaL.checkstack(L, 2, "ObjectTranslator.pushType");
			luaclr.newref(L, new ProxyType(t));
			luaL.getmetatable(L, "luaNet_class");
			Debug.Assert(luaclr.isrefmeta(L,-1));
			lua.setmetatable(L, -2);
		}
		/// <summary>[-0, +1, m] Pushes a CLR object into the Lua stack as a standard LuaInterface userdata</summary>
		void pushObject(lua.State L, object o)
		{
			Debug.Assert(interpreter.IsSameLua(L));
			if (o == null)
			{
				lua.pushnil(L);
				return;
			}
			luaL.checkstack(L, 3, "ObjectTranslator.pushObject");
			StackAssert.Start(L);

			luaclr.newref(L, o);

			// Gets or creates the metatable for the object's type
			if (luaclr.newrefmeta(L, o.GetType().AssemblyQualifiedName, 4))
				metaFunctions.BuildObjectMetatable(L);

			lua.setmetatable(L,-2);
			StackAssert.End(1);
		}
		/// <summary>[-0, +0, m] Gets an object from the Lua stack with the desired type, if it matches, otherwise returns null.</summary>
		/// <exception cref="NullReferenceException"><paramref name="paramType"/> is required</exception>
		internal object getAsType(lua.State L,int index,Type paramType)
		{
			Debug.Assert(interpreter.IsSameLua(L));
			Debug.Assert(paramType != null);
			ExtractValue extractor = typeChecker.checkType(L,index,paramType);
			if (extractor == null) return null;
			return extractor(L, index);
		}



		/// <summary>[-0, +0, m] Gets an object from the Lua stack according to its Lua type.</summary>
		internal unsafe object getObject(lua.State L, int index)
		{
			Debug.Assert(interpreter.IsSameLua(L));
			switch(lua.type(L,index))
			{
			case LUA.T.NONE:
				return luaL.error(L, "index points to an empty stack position");

			case LUA.T.NIL:
				return null;

			case LUA.T.NUMBER:
				return lua.tonumber(L,index);

			case LUA.T.STRING:
				return lua.tostring(L,index);

			case LUA.T.BOOLEAN:
				return lua.toboolean(L,index);

			case LUA.T.TABLE:
				return new LuaTable(L, interpreter, index);

			case LUA.T.FUNCTION:
				return new LuaFunction(L, interpreter, index);

			case LUA.T.USERDATA:
				return luaclr.isref(L, index) ? luaclr.getref(L, index) : new LuaUserData(L, interpreter, index); // translate freed references as null

			case LUA.T.LIGHTUSERDATA:
				return new IntPtr(lua.touserdata(L, index));

			case LUA.T.THREAD:
				return LUA.T.THREAD; // todo: coroutine support

			default:
				// all LUA.Ts have a case, so this shouldn't happen
				return luaL.error(L, "incorrect or corrupt program");
			}
		}

		/// <summary>Gets the values from the provided index to the top of the stack and returns them in an array.</summary>
		internal object[] popValues(lua.State L, int oldTop)
		{
			return popValues(L, oldTop, null);
		}
		/// <summary>
		/// [-(top-oldTop), +0, m] Gets the values from the provided index to the top of the stack and returns them in an array,
		/// casting them to the provided types.
		/// </summary>
		internal object[] popValues(lua.State L, int oldTop, Type[] popTypes)
		{
			Debug.Assert(interpreter.IsSameLua(L));
			Debug.Assert(oldTop >= 0, "oldTop must be a value previously returned by lua_gettop");
			int newTop = lua.gettop(L);
			if(oldTop >= newTop) return null;

			var values = new object[newTop - oldTop];
			int j = 0;
			if (popTypes == null)
			{
				for(int i = oldTop+1; i <= newTop; ++i)
					values[j++] = getObject(L,i);
			}
			else
			{
				int iTypes = popTypes[0] == typeof(void) ? 1 : 0;
				if (popTypes.Length + iTypes < values.Length)
					luaL.error(L, "too many arguments"); // avoid index out of bounds exception
				for(int i = oldTop+1; i <= newTop; ++i)
					values[j++] = getAsType(L,i,popTypes[iTypes++]);
			}

			lua.settop(L,oldTop);
			return values;
		}

#if ! __NOGEN__
		static bool IsILua(object o)
		{
			if (o is ILuaGeneratedType)
			{
				// remoting proxies always return a match for 'is'
				// Make sure we are _really_ ILuaGenerated
				return o.GetType().GetInterface("ILuaGeneratedType") != null;
			}
			return false;
		}
		static ILuaGeneratedType AsILua(object o)
		{
			var result = o as ILuaGeneratedType;
			if (result != null && o.GetType().GetInterface("ILuaGeneratedType") != null)
				return result;
			return null;
		}
#endif

		/// <summary>[-0, +1, e] Pushes the object into the Lua stack according to its type. Disposes orphaned objects. (<see cref="LuaTable.IsOrphaned"/>)</summary>
		internal void pushReturnValue(lua.State L, object o)
		{
			push(L, o);
			var t = o as LuaTable;
			if (t != null && t.IsOrphaned) t.Dispose();
		}
		/// <summary>[-0, +1, e] Pushes the object into the Lua stack according to its type.</summary>
		internal void push(lua.State L, object o)
		{
			Debug.Assert(interpreter.IsSameLua(L));
			if (o == null)
			{
				lua.pushnil(L);
				return;
			}
			var conv = o as IConvertible;
			if (conv != null && conv is Enum == false)
			switch (conv.GetTypeCode())
			{
			case TypeCode.Boolean: lua.pushboolean(L, (bool)   conv);       return;
			case TypeCode.Double:  lua.pushnumber (L, (double) conv);       return;
			case TypeCode.Single:  lua.pushnumber (L, (float)  conv);       return;
			case TypeCode.String:  lua.pushstring (L, (string) conv);       return;
			case TypeCode.Char:    lua.pushnumber (L, (char)   conv);       return;

			// all the integer types and System.Decimal
			default:               lua.pushnumber (L, conv.ToDouble(null)); return;

			case TypeCode.Empty:
			case TypeCode.DBNull:  lua.pushnil    (L);                      return;

			case TypeCode.Object:
			case TypeCode.DateTime: break;
			}

			#if ! __NOGEN__
			{	var x = AsILua(o);
				if(x!=null) { var t = x.__luaInterface_getLuaTable();
				              if (t.Owner != interpreter) luaL.error(L, t.CrossInterpreterError());
				              t.push(L); return; }  }
			#endif
			{	var x = o as LuaBase;
				if(x!=null) { if (x.Owner != interpreter) luaL.error(L, x.CrossInterpreterError());
				              x.push(L); return; }  }

			{	var x = o as lua.CFunction;
				if(x!=null) { luaclr.pushcfunction(L,x); return; }  }

			if(o is LuaValue) { ((LuaValue)o).push(L); return; }

			// disallow all reflection
			if ((o.GetType().Namespace ?? "").StartsWith("System.Reflect", StringComparison.Ordinal))
			{
				lua.pushstring(L, o.ToString());
				return;
			}

			pushObject(L,o);
		}
		

		static bool IsInteger(double x) {
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
			Debug.Assert(interpreter.IsSameLua(L));
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
				try { extractValue = typeChecker.checkType(L, currentLuaParam, currentNetParam.ParameterType); }
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

		bool _IsParamsArray(lua.State L, int index, ParameterInfo param, out ExtractValue extractValue)
		{
			Debug.Assert(interpreter.IsSameLua(L));
			extractValue = null;

			if (param.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0)
			switch (lua.type(L, index))
			{
			case LUA.T.NONE:
				Debug.WriteLine("_IsParamsArray: Could not retrieve lua type.");
				return false;

			case LUA.T.TABLE:
				extractValue = typeChecker.getExtractor(typeof(LuaTable));
				if (extractValue != null)
					return true;
				break;

			default:
				Type elementType = param.ParameterType.GetElementType();
				if (elementType == null)
					break; // not an array; invalid ParamArrayAttribute
				try
				{
					extractValue = typeChecker.checkType(L, index, elementType);
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