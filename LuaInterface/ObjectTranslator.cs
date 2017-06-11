using System;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;
using LuaInterface.LuaAPI;

namespace LuaInterface
{
	/// <summary>Passes objects from the CLR to Lua and vice-versa</summary>
	/// <remarks>
	/// Author: Fabio Mascarenhas
	/// Version: 1.0
	/// </remarks>
	public class ObjectTranslator
	{
		internal readonly CheckType typeChecker;

		public readonly Dictionary<int, object> objects = new Dictionary<int, object>();

		internal readonly Lua interpreter;
		private readonly MetaFunctions metaFunctions;
		private readonly lua.CFunction registerTableFunction,unregisterTableFunction,getMethodSigFunction,
			getConstructorSigFunction,importTypeFunction,loadAssemblyFunction, ctypeFunction, enumFromIntFunction;

		internal readonly EventHandlerContainer pendingEvents = new EventHandlerContainer();

		public ObjectTranslator(Lua interpreter)
		{
			this.interpreter=interpreter;
			var L = interpreter._L;
			luanet.checkstack(L, 3, "new ObjectTranslator");
			typeChecker = new CheckType(this);
			metaFunctions = new MetaFunctions(this);

			importTypeFunction        = this.importType;
			loadAssemblyFunction      = this.loadAssembly;
			registerTableFunction     = this.registerTable;
			unregisterTableFunction   = this.unregisterTable;
			getMethodSigFunction      = this.getMethodSignature;
			getConstructorSigFunction = this.getConstructorSignature;

			ctypeFunction             = this.ctype;
			enumFromIntFunction       = this.enumFromInt;

			createBaseClassMetatable(L);
			createClassMetatable(L);
			createFunctionMetatable(L);
			setGlobalFunctions(L);
		}

		/// <summary>[requires checkstack(2)] Creates the metatable for superclasses (the base field of registered tables)</summary>
		private void createBaseClassMetatable(lua.State L)
		{
			Debug.Assert(L == interpreter._L); StackAssert.Start(L);
			luaL.newmetatable(L,"luaNet_searchbase");
			lua.pushboolean(L, false);                           lua.setfield(L,-2,"__metatable");
			lua.pushcfunction(L,metaFunctions.gcFunction);       lua.setfield(L,-2,"__gc");
			lua.pushcfunction(L,metaFunctions.toStringFunction); lua.setfield(L,-2,"__tostring");
			lua.pushcfunction(L,metaFunctions.baseIndexFunction);lua.setfield(L,-2,"__index");
			lua.pushcfunction(L,metaFunctions.newindexFunction); lua.setfield(L,-2,"__newindex");
			lua.pop(L,1);                      StackAssert.End();
		}
		/// <summary>[requires checkstack(2)] Creates the metatable for type references</summary>
		private void createClassMetatable(lua.State L)
		{
			Debug.Assert(L == interpreter._L); StackAssert.Start(L);
			luaL.newmetatable(L,"luaNet_class");
			lua.pushboolean(L, false);                                  lua.setfield(L,-2,"__metatable");
			lua.pushcfunction(L,metaFunctions.gcFunction);              lua.setfield(L,-2,"__gc");
			lua.pushcfunction(L,metaFunctions.toStringFunction);        lua.setfield(L,-2,"__tostring");
			lua.pushcfunction(L,metaFunctions.classIndexFunction);      lua.setfield(L,-2,"__index");
			lua.pushcfunction(L,metaFunctions.classNewindexFunction);   lua.setfield(L,-2,"__newindex");
			lua.pushcfunction(L,metaFunctions.callConstructorFunction); lua.setfield(L,-2,"__call");
			lua.pop(L,1);                      StackAssert.End();
		}
		/// <summary>[requires checkstack(1)] Registers the global functions used by LuaInterface</summary>
		private void setGlobalFunctions(lua.State L)
		{
			Debug.Assert(L == interpreter._L); StackAssert.Start(L);
			lua.pushcfunction(L,importTypeFunction);        lua.setglobal(L,"import_type");
			//lua.pushcfunction(L,loadAssemblyFunction);      lua.setglobal(L,"load_assembly");
			//lua.pushcfunction(L,registerTableFunction);     lua.setglobal(L,"make_object");
			//lua.pushcfunction(L,unregisterTableFunction);   lua.setglobal(L,"free_object");
			lua.pushcfunction(L,getMethodSigFunction);      lua.setglobal(L,"get_method_bysig");
			lua.pushcfunction(L,getConstructorSigFunction); lua.setglobal(L,"get_constructor_bysig");
			lua.pushcfunction(L,ctypeFunction);             lua.setglobal(L,"ctype");
			lua.pushcfunction(L,enumFromIntFunction);       lua.setglobal(L,"enum");
			StackAssert.End();
		}

		/// <summary>[requires checkstack(2)] Creates the metatable for delegates</summary>
		private void createFunctionMetatable(lua.State L)
		{
			Debug.Assert(L == interpreter._L); StackAssert.Start(L);
			luaL.newmetatable(L,"luaNet_function");
			lua.pushboolean(L, false);                               lua.setfield(L,-2,"__metatable");
			lua.pushcfunction(L,metaFunctions.gcFunction);           lua.setfield(L,-2,"__gc");
			lua.pushcfunction(L,metaFunctions.execDelegateFunction); lua.setfield(L,-2,"__call");
			lua.pop(L,1);                      StackAssert.End();
		}
		/// <summary>[-0, +0, v] Passes errors (argument e) to the Lua interpreter. This function throws a Lua exception, and therefore never returns.</summary>
		internal int throwError(lua.State L, Exception ex)
		{
			Debug.Assert(L == interpreter._L);
			Debug.Assert(ex != null);
			luaL.checkstack(L, 1, "ObjectTranslator.throwError");
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

		private readonly List<Assembly> loaded_assemblies = new List<Assembly>();
		private readonly Dictionary<string, Type> loaded_types = new Dictionary<string, Type>();

		/// <summary>Implementation of load_assembly. Throws an error if the assembly is not found.</summary>
		private int loadAssembly(lua.State L)
		{
			Debug.Assert(L == interpreter._L && luanet.infunction(L));
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
			}
			catch (Exception e) { return throwError(L,e); }

			return 0;
		}
		internal void LoadAssembly(Assembly assembly)
		{
			Debug.Assert(assembly != null);
			if (!loaded_assemblies.Contains(assembly))
				loaded_assemblies.Add(assembly);
		}
		internal void LoadType(Type t, bool and_nested)
		{
			Debug.Assert(t != null);
			loaded_types.Add(t.FullName, t);
			if (!and_nested) return;
			foreach (var nested in t.GetNestedTypes(luanet.LuaBindingFlags | BindingFlags.FlattenHierarchy))
				LoadType(nested, true);
		}

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
		internal Type FindType(string className)
		{
			Type t;
			foreach (var assembly in loaded_assemblies)
				if ((t = assembly.GetType(className)) != null)
					return t;

			loaded_types.TryGetValue(className, out t); // null if not found
			return t;
		}

		/// <summary>Implementation of import_type. Returns nil if the type is not found.</summary>
		private int importType(lua.State L)
		{
			Debug.Assert(L == interpreter._L && luanet.infunction(L));
			string className=lua.tostring(L,1);
			Type klass = FindType(className);
			if(klass != null)
				pushType(L,klass);
			else
				lua.pushnil(L);
			return 1;
		}
		/// <summary>
		/// Implementation of make_object.
		/// Registers a table (first argument in the stack) as an object subclassing the type passed as second argument in the stack.
		/// </summary>
		private int registerTable(lua.State L)
		{
			Debug.Assert(L == interpreter._L && luanet.infunction(L));
#if __NOGEN__
			return luaL.error(L,"Tables as Objects not implemented");
#else
			luaL.checktype(L, 1, LUA.T.TABLE);

			var superclassName = luaL.checkstring(L, 2);
			Type klass = FindType(superclassName);
			if (klass == null)
				return luaL.argerror(L, 2, "can not find superclass '" + superclassName + "'");

			// Creates and pushes the object in the stack, setting
			// it as the indexer of the first argument
			object obj = CodeGeneration.Instance.GetClassInstance(klass, getTable(L,1));
			pushObject(L, obj, "luaNet_metatable");
			lua.newtable(L);
			lua.pushvalue(L, -2); lua.setfield(L, -2, "__index");
			lua.pushvalue(L, -2); lua.setfield(L, -2, "__newindex");
			lua.setmetatable(L, 1);
			// Pushes the object again, this time as the base field
			// of the table and with the luaNet_searchbase metatable
			lua.pushstring(L, "base");
			int index = addObject(obj);
			pushNewObject(L, obj, index, "luaNet_searchbase");
			lua.rawset(L, 1);
			return 0;
#endif
		}
		/// <summary>
		/// Implementation of free_object.
		/// Clears the metatable and the base field, freeing the created object for garbage-collection
		/// </summary>
		private int unregisterTable(lua.State L)
		{
			Debug.Assert(L == interpreter._L && luanet.infunction(L));
			if (!lua.getmetatable(L, 1))
				return luaL.argerror(L, 1, "invalid table");
			try
			{
				lua.getfield(L,-1,"__index");
				object obj = getRawNetObject(L,-1);
				if (obj == null) return luaL.argerror(L, 1, "invalid table");

				FieldInfo luaTableField = obj.GetType().GetField("__luaInterface_luaTable");
				if (luaTableField == null) return luaL.argerror(L, 1, "invalid table");

				var table = luaTableField.GetValue(obj) as LuaTable;
				if (table == null || table.Owner._L != L) return luaL.argerror(L, 1, "invalid table");
				table.Dispose();
				luaTableField.SetValue(obj,null);

				lua.pushnil(L); lua.setmetatable(L,1);
				lua.pushnil(L); lua.setfield(L,1,"base");
			}
			catch(Exception e) { return throwError(L,e); }
			return 0;
		}
		/// <summary>Implementation of get_method_bysig. Returns nil if no matching method is not found.</summary>
		private int getMethodSignature(lua.State L)
		{
			Debug.Assert(L == interpreter._L && luanet.infunction(L));
			IReflect klass; object target;
			int id=luanet.checkudata(L,1,"luaNet_class");
			if (id != -1)
			{
				klass = (IReflect) objects[id];
				target = null;
			}
			else
			{
				target = getRawNetObject(L,1);
				if(target == null)
					return luaL.argerror(L, 1, "type or object reference expected");
				klass=target.GetType();
			}
			string methodName = luaL.checkstring(L,2);
			var signature = new Type[lua.gettop(L)-2];
			for (int i = 0; i < signature.Length; ++i)
				signature[i] = FindType(luaL.checkstring(L,i+3));
			try
			{
				var method = klass.GetMethod(methodName,BindingFlags.Static | BindingFlags.Instance |
					BindingFlags.FlattenHierarchy | luanet.LuaBindingFlags, null, signature, null);
				pushFunction(L,new lua.CFunction((new LuaMethodWrapper(this,target,klass,method)).call));
			}
			catch (Exception e) { return throwError(L,e); }
			return 1;
		}
		/// <summary>Implementation of get_constructor_bysig. Returns nil if no matching constructor is found.</summary>
		private int getConstructorSignature(lua.State L)
		{
			Debug.Assert(L == interpreter._L && luanet.infunction(L));
			int id = luanet.checkudata(L,1,"luaNet_class");
			if (id == -1) return luaL.argerror(L,1,"type reference expected");
			var klass = (IReflect) objects[id];

			var signature = new Type[lua.gettop(L)-1];
			for (int i = 0; i < signature.Length; ++i)
				signature[i] = FindType(lua.tostring(L,i+2));
			try
			{
				var constructor = klass.UnderlyingSystemType.GetConstructor(signature);
				pushFunction(L,new lua.CFunction((new LuaMethodWrapper(this,null,klass,constructor)).call));
			}
			catch(Exception e) { return throwError(L,e); }
			return 1;
		}

		private Type typeOf(lua.State L, int idx)
		{
			Debug.Assert(L == interpreter._L);
			luaL.checkstack(L, 2, "ObjectTranslator.typeOf");
			int id = luanet.checkudata(L,idx,"luaNet_class");
			if (id == -1) return null;

			var pt = (ProxyType) objects[id];
			return pt.UnderlyingSystemType;
		}

		/// <summary>Implementation of ctype.</summary>
		private int ctype(lua.State L)
		{
			Debug.Assert(L == interpreter._L && luanet.infunction(L));
			Type t = typeOf(L,1);
			if (t == null)
			{
				lua.pushnil(L);
				return 1;
			}

			pushObject(L,t,"luaNet_metatable");
			return 1;
		}

		/// <summary>Implementation of enum.</summary>
		private int enumFromInt(lua.State L)
		{
			Debug.Assert(L == interpreter._L && luanet.infunction(L));
			Type t = typeOf(L,1);
			if (t == null || !t.IsEnum)
				return luaL.argerror(L, 1, "enum type reference expected");

			object res;
			switch (lua.type(L,2))
			{
			case LUA.T.NUMBER:
				res = Enum.ToObject(t,(int) lua.tonumber(L,2));
				break;
			case LUA.T.STRING:
				try { res = Enum.Parse(t, lua.tostring(L,2)); }
				catch (ArgumentException e) { return luaL.argerror(L, 2, e.Message); }
				break;
			default:
				return luaL.typerror(L, 2, "number or string");
			}
			pushObject(L,res,"luaNet_metatable");
			return 1;
		}

		/// <summary>[-0, +1, m] Pushes a type reference into the stack</summary>
		internal void pushType(lua.State L, Type t)
		{
			Debug.Assert(L == interpreter._L);
			pushObject(L,new ProxyType(t),"luaNet_class");
		}
		/// <summary>[-0, +1, m] Pushes a delegate into the stack</summary>
		internal void pushFunction(lua.State L, lua.CFunction func)
		{
			Debug.Assert(L == interpreter._L);
			pushObject(L,func,"luaNet_function");
		}
		/// <summary>[-0, +1, m] Pushes a CLR object into the Lua stack as an userdata with the provided metatable</summary>
		internal void pushObject(lua.State L, object o, string metatable)
		{
			Debug.Assert(L == interpreter._L);
			if (o == null)
			{
				lua.pushnil(L);
				return;
			}
			pushNewObject(L, o, addObject(o), metatable);
		}


		/// <summary>[-0, +1, m] Pushes a new object into the Lua stack with the provided metatable</summary>
		private void pushNewObject(lua.State L,object o,int id,string metatable)
		{
			Debug.Assert(L == interpreter._L);
			luaL.checkstack(L, 4, "ObjectTranslator.pushNewObject");
			StackAssert.Start(L);

			luanet.newudata(L,id);

			if (metatable != "luaNet_metatable")
				luaL.getmetatable(L, metatable);
			// Gets or creates the metatable for the object's type
			else if (luaL.newmetatable(L,o.GetType().AssemblyQualifiedName))
			{
				lua.pushboolean(L, false);                           lua.setfield(L,-2,"__metatable");
				lua.newtable(L);                                     lua.setfield(L,-2,"cache");
				lua.pushcfunction(L,metaFunctions.indexFunction);    lua.setfield(L,-2,"__index");
				lua.pushcfunction(L,metaFunctions.gcFunction);       lua.setfield(L,-2,"__gc");
				lua.pushcfunction(L,metaFunctions.toStringFunction); lua.setfield(L,-2,"__tostring");
				lua.pushcfunction(L,metaFunctions.newindexFunction); lua.setfield(L,-2,"__newindex");

				lua.pushlightuserdata(L,luanet.gettag());
				lua.pushnumber(L,1);
				lua.settable(L,-3);
			}

			Debug.Assert(lua.istable(L, -1));
			lua.setmetatable(L,-2);
			StackAssert.End(1);
		}
		/// <summary>Gets an object from the Lua stack with the desired type, if it matches, otherwise returns null.</summary>
		internal object getAsType(lua.State L,int index,Type paramType)
		{
			Debug.Assert(L == interpreter._L);
			ExtractValue extractor = typeChecker.checkType(L,index,paramType);
			if (extractor == null) return null;
			return extractor(L, index);
		}


		/// <summary>[nothrow] Given the Lua int ID for an object remove it from our maps</summary>
		internal void collectObject(int id)
		{
			objects.Remove(id);
		}


		/// <summary>We want to ensure that objects always have a unique ID</summary>
		int nextObj = 0;

		/// <summary>[nothrow] Stores the object and returns its ID.</summary>
		int addObject(object obj)
		{
			Debug.Assert(obj != null);
			// New object: inserts it in the list
			int id = nextObj++;

			// Debug.WriteLine("Adding " + obj.ToString() + " @ " + index);

			objects[id] = obj;

			return id;
		}



		/// <summary>[-0, +0, m] Gets an object from the Lua stack according to its Lua type.</summary>
		internal unsafe object getObject(lua.State L, int index)
		{
			Debug.Assert(L == interpreter._L);
			switch(lua.type(L,index))
			{
			case LUA.T.NONE:
				throw new ArgumentException("index points to an empty stack position", "index");

			case LUA.T.NIL:
				return null;

			case LUA.T.NUMBER:
				return lua.tonumber(L,index);

			case LUA.T.STRING:
				return lua.tostring(L,index);

			case LUA.T.BOOLEAN:
				return lua.toboolean(L,index);

			case LUA.T.TABLE:
				return getTable(L,index);

			case LUA.T.FUNCTION:
				return getFunction(L,index);

			case LUA.T.USERDATA:
				return getNetObject(L,index) ?? getUserData(L,index);

			case LUA.T.LIGHTUSERDATA:
				return new IntPtr(lua.touserdata(L, index));

			case LUA.T.THREAD:
				return LUA.T.THREAD; // not supported

			default:
				// all LUA.Ts have a case, so this shouldn't happen
				throw new Exception("incorrect or corrupt program");
			}
		}
		/// <summary>[-0, +0, m] Gets the table in the <paramref name="index"/> position of the Lua stack.</summary>
		internal LuaTable getTable(lua.State L, int index)
		{
			Debug.Assert(L == interpreter._L);
			lua.pushvalue(L,index);
			return new LuaTable(L,interpreter);
		}
		/// <summary>[-0, +0, m] Gets the userdata in the <paramref name="index"/> position of the Lua stack.</summary>
		internal LuaUserData getUserData(lua.State L, int index)
		{
			Debug.Assert(L == interpreter._L);
			lua.pushvalue(L,index);
			return new LuaUserData(L,interpreter);
		}
		/// <summary>[-0, +0, m] Gets the function in the <paramref name="index"/> position of the Lua stack.</summary>
		internal LuaFunction getFunction(lua.State L, int index)
		{
			Debug.Assert(L == interpreter._L);
			lua.pushvalue(L,index);
			return new LuaFunction(L,interpreter);
		}
		/// <summary>[-0, +0, m] Gets the raw string in the <paramref name="index"/> position of the Lua stack.</summary>
		internal LuaString getLuaString(lua.State L, int index)
		{
			Debug.Assert(L == interpreter._L);
			lua.pushvalue(L,index);
			return new LuaString(L,interpreter);
		}
		/// <summary>[-0, +0, -] Gets the CLR object in the <paramref name="index"/> position of the Lua stack.</summary>
		internal object getNetObject(lua.State L, int index)
		{
			Debug.Assert(L == interpreter._L);
			luaL.checkstack(L, 3, "ObjectTranslator.getNetObject");
			int id = luanet.tonetobject(L,index);
			if (id != -1)
				return objects[id];
			else
				return null;
		}
		/// <summary>Gets the CLR object in the <paramref name="index"/> position of the Lua stack. Only use this if you're completely sure the value is a luanet userdata.</summary>
		internal object getRawNetObject(lua.State L, int index)
		{
			Debug.Assert(L == interpreter._L);
			int id = luanet.rawnetobj(L,index);
			if (id != -1)
				return objects[id];
			else
				return null;
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
			Debug.Assert(L == interpreter._L);
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
			Debug.Assert(L == interpreter._L);
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
				              if (t.Owner != interpreter) throw Lua.NewCrossInterpreterError(t);
				              t.push(L); return; }  }
			#endif
			{	var x = o as LuaBase;
				if(x!=null) { if (x.Owner != interpreter) throw Lua.NewCrossInterpreterError(x);
				              x.push(L); return; }  }

			{	var x = o as lua.CFunction;
				if(x!=null) { pushFunction(L,x); return; }  }

			if(o is LuaValue) { ((LuaValue)o).push(L); return; }

			// disallow all reflection
			if ((o.GetType().Namespace ?? "").StartsWith("System.Reflect", StringComparison.Ordinal))
			{
				lua.pushstring(L, o.ToString());
				return;
			}

			pushObject(L,o,"luaNet_metatable");
		}

		/// <summary>Checks if the method matches the arguments in the Lua stack, getting the arguments if it does.</summary>
		internal bool matchParameters(lua.State L,MethodBase method,ref MethodCache methodCache)
		{
			Debug.Assert(L == interpreter._L);
			return metaFunctions.matchParameters(L,method,ref methodCache);
		}
	}
}