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
		internal CheckType typeChecker;

		// object # to object (FIXME - it should be possible to get object address as an object #)
		public readonly Dictionary<int, object> objects = new Dictionary<int, object>();
		// object to object #
		public readonly Dictionary<object, int> objectsBackMap = new Dictionary<object, int>();
		internal Lua interpreter;
		private MetaFunctions metaFunctions;
		private List<Assembly> assemblies;
		private lua.CFunction registerTableFunction,unregisterTableFunction,getMethodSigFunction,
			getConstructorSigFunction,importTypeFunction,loadAssemblyFunction, ctypeFunction, enumFromIntFunction;

		internal EventHandlerContainer pendingEvents = new EventHandlerContainer();

		public ObjectTranslator(Lua interpreter)
		{
			this.interpreter=interpreter;
			var L = interpreter._L;
			typeChecker=new CheckType(this);
			metaFunctions=new MetaFunctions(this);
			assemblies=new List<Assembly>();

			importTypeFunction = this.importType;
			loadAssemblyFunction = this.loadAssembly;
			registerTableFunction = this.registerTable;
			unregisterTableFunction = this.unregisterTable;
			getMethodSigFunction = this.getMethodSignature;
			getConstructorSigFunction = this.getConstructorSignature;

			ctypeFunction = this.ctype;
			enumFromIntFunction = this.enumFromInt;

			createLuaObjectList(L);
			createIndexingMetaFunction(L);
			createBaseClassMetatable(L);
			createClassMetatable(L);
			createFunctionMetatable(L);
			setGlobalFunctions(L);
		}

		/// <summary>Sets up the list of objects in the Lua side</summary>
		private static void createLuaObjectList(lua.State L)
		{
			StackAssert.Start(L);
			lua.pushstring(L,"luaNet_objects");
			lua.newtable(L);
			lua.newtable(L);
			lua.pushstring(L,"__mode");
			lua.pushstring(L,"v");
			lua.settable(L,-3);
			lua.setmetatable(L,-2);
			lua.settable(L, LUA.REGISTRYINDEX);
			StackAssert.End();
		}
		/// <summary>Registers the indexing function of CLR objects passed to Lua</summary>
		private static void createIndexingMetaFunction(lua.State L)
		{
			StackAssert.Start(L);
			lua.pushstring(L,"luaNet_indexfunction");
			luaL.dostring(L,MetaFunctions.luaIndexFunction);
			//lua.pushcfunction(L,indexFunction);
			lua.rawset(L, LUA.REGISTRYINDEX);
			StackAssert.End();
		}
		/// <summary>Creates the metatable for superclasses (the base field of registered tables)</summary>
		private void createBaseClassMetatable(lua.State L)
		{
			Debug.Assert(L == interpreter._L); StackAssert.Start(L);
			luaL.newmetatable(L,"luaNet_searchbase");
			lua.pushstring(L,"__gc");
			lua.pushcfunction(L,metaFunctions.gcFunction);
			lua.settable(L,-3);
			lua.pushstring(L,"__tostring");
			lua.pushcfunction(L,metaFunctions.toStringFunction);
			lua.settable(L,-3);
			lua.pushstring(L,"__index");
			lua.pushcfunction(L,metaFunctions.baseIndexFunction);
			lua.settable(L,-3);
			lua.pushstring(L,"__newindex");
			lua.pushcfunction(L,metaFunctions.newindexFunction);
			lua.settable(L,-3);
			lua.pop(L,1);                      StackAssert.End();
		}
		/// <summary>Creates the metatable for type references</summary>
		private void createClassMetatable(lua.State L)
		{
			Debug.Assert(L == interpreter._L); StackAssert.Start(L);
			luaL.newmetatable(L,"luaNet_class");
			lua.pushstring(L,"__gc");
			lua.pushcfunction(L,metaFunctions.gcFunction);
			lua.settable(L,-3);
			lua.pushstring(L,"__tostring");
			lua.pushcfunction(L,metaFunctions.toStringFunction);
			lua.settable(L,-3);
			lua.pushstring(L,"__index");
			lua.pushcfunction(L,metaFunctions.classIndexFunction);
			lua.settable(L,-3);
			lua.pushstring(L,"__newindex");
			lua.pushcfunction(L,metaFunctions.classNewindexFunction);
			lua.settable(L,-3);
			lua.pushstring(L,"__call");
			lua.pushcfunction(L,metaFunctions.callConstructorFunction);
			lua.settable(L,-3);
			lua.pop(L,1);                      StackAssert.End();
		}
		/// <summary>Registers the global functions used by LuaInterface</summary>
		private void setGlobalFunctions(lua.State L)
		{
			Debug.Assert(L == interpreter._L); StackAssert.Start(L);
			lua.pushcfunction(L,metaFunctions.indexFunction);
			lua.setglobal(L,"get_object_member");
			/*lua.pushcfunction(L,importTypeFunction);
			lua.setglobal(L,"import_type");
			lua.pushcfunction(L,loadAssemblyFunction);
			lua.setglobal(L,"load_assembly");
			lua.pushcfunction(L,registerTableFunction);
			lua.setglobal(L,"make_object");
			lua.pushcfunction(L,unregisterTableFunction);
			lua.setglobal(L,"free_object");*/
			lua.pushcfunction(L,getMethodSigFunction);
			lua.setglobal(L,"get_method_bysig");
			lua.pushcfunction(L,getConstructorSigFunction);
			lua.setglobal(L,"get_constructor_bysig");
			lua.pushcfunction(L,ctypeFunction);
			lua.setglobal(L,"ctype");
			lua.pushcfunction(L,enumFromIntFunction);
			lua.setglobal(L,"enum");           StackAssert.End();
		}

		/// <summary>Creates the metatable for delegates</summary>
		private void createFunctionMetatable(lua.State L)
		{
			Debug.Assert(L == interpreter._L); StackAssert.Start(L);
			luaL.newmetatable(L,"luaNet_function");
			lua.pushstring(L,"__gc");
			lua.pushcfunction(L,metaFunctions.gcFunction);
			lua.settable(L,-3);
			lua.pushstring(L,"__call");
			lua.pushcfunction(L,metaFunctions.execDelegateFunction);
			lua.settable(L,-3);
			lua.pop(L,1);                      StackAssert.End();
		}
		/// <summary>Passes errors (argument e) to the Lua interpreter</summary>
		internal void throwError(lua.State L, object e)
		{
			Debug.Assert(L == interpreter._L);
			// We use this to remove anything pushed by luaL_where
			int oldTop = lua.gettop(L);

			// Stack frame #1 is our C# wrapper, so not very interesting to the user
			// Stack frame #2 must be the lua code that called us, so that's what we want to use
			luaL.where(L, 1);
			object[] curlev = popValues(L, oldTop);

			// Determine the position in the script where the exception was triggered
			string errLocation = "";
			if (curlev.Length > 0)
				errLocation = curlev[0].ToString();

			string message = e as string;
			if (message != null)
			{
				// Wrap Lua error (just a string) and store the error location
				e = new LuaScriptException(message, errLocation);
			}
			else
			{
				Exception ex = e as Exception;
				if (ex != null)
				{
					// Wrap generic .NET exception as an InnerException and store the error location
					e = new LuaScriptException(ex, errLocation);
				}
			}

			push(L, e);
			lua.error(L);
		}
		/// <summary>Implementation of load_assembly. Throws an error if the assembly is not found.</summary>
		private int loadAssembly(lua.State L)
		{
			Debug.Assert(L == interpreter._L);
			try
			{
				string assemblyName=lua.tostring(L,1);

				Assembly assembly = null;

				try
				{
					#pragma warning disable 618 // LoadWithPartialName deprecated. No alternative exists.
					assembly = Assembly.LoadWithPartialName(assemblyName);
					#pragma warning restore 618
				}
				catch (BadImageFormatException)
				{
					// The assemblyName was invalid.  It is most likely a path.
				}

				if (assembly == null)
				{
					assembly = Assembly.Load(AssemblyName.GetAssemblyName(assemblyName));
				}

				if (assembly != null && !assemblies.Contains(assembly))
				{
					assemblies.Add(assembly);
				}
			}
			catch(Exception e)
			{
				throwError(L,e);
			}

			return 0;
		}

		internal Type FindType(string className)
		{
			foreach(Assembly assembly in assemblies)
			{
				Type klass=assembly.GetType(className);
				if(klass!=null)
				{
					return klass;
				}
			}
			return null;
		}

		/// <summary>Implementation of import_type. Returns nil if the type is not found.</summary>
		private int importType(lua.State L)
		{
			Debug.Assert(L == interpreter._L);
			string className=lua.tostring(L,1);
			Type klass=FindType(className);
			if(klass!=null)
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
			Debug.Assert(L == interpreter._L);
#if __NOGEN__
			throwError(L,"Tables as Objects not implemented");
#else
			if(lua.type(L,1)==LuaType.Table)
			{
				string superclassName = lua.tostring(L, 2);
				if (superclassName != null)
				{
					Type klass = FindType(superclassName);
					if (klass != null)
					{
						// Creates and pushes the object in the stack, setting
						// it as the  metatable of the first argument
						object obj = CodeGeneration.Instance.GetClassInstance(klass, getTable(L,1));
						pushObject(L, obj, "luaNet_metatable");
						lua.newtable(L);
						lua.pushstring(L, "__index");
						lua.pushvalue(L, -3);
						lua.settable(L, -3);
						lua.pushstring(L, "__newindex");
						lua.pushvalue(L, -3);
						lua.settable(L, -3);
						lua.setmetatable(L, 1);
						// Pushes the object again, this time as the base field
						// of the table and with the luaNet_searchbase metatable
						lua.pushstring(L, "base");
						int index = addObject(obj);
						pushNewObject(L, obj, index, "luaNet_searchbase");
						lua.rawset(L, 1);
					}
					else
						throwError(L, "register_table: can not find superclass '" + superclassName + "'");
				}
				else
					throwError(L, "register_table: superclass name can not be null");
			}
			else throwError(L,"register_table: first arg is not a table");
#endif
			return 0;
		}
		/// <summary>
		/// Implementation of free_object.
		/// Clears the metatable and the base field, freeing the created object for garbage-collection
		/// </summary>
		private int unregisterTable(lua.State L)
		{
			Debug.Assert(L == interpreter._L);
			try
			{
				if (!lua.getmetatable(L, 1))
					throwError(L, "unregister_table: arg is not valid table");
				else
				{
					lua.pushstring(L,"__index");
					lua.gettable(L,-2);
					object obj=getRawNetObject(L,-1);
					if(obj==null) throwError(L,"unregister_table: arg is not valid table");
					FieldInfo luaTableField=obj.GetType().GetField("__luaInterface_luaTable");
					if(luaTableField==null) throwError(L,"unregister_table: arg is not valid table");
					luaTableField.SetValue(obj,null);
					lua.pushnil(L);
					lua.setmetatable(L,1);
					lua.pushstring(L,"base");
					lua.pushnil(L);
					lua.settable(L,1);
				}
			}
			catch(Exception e)
			{
				throwError(L,e.Message);
			}
			return 0;
		}
		/// <summary>Implementation of get_method_bysig. Returns nil if no matching method is not found.</summary>
		private int getMethodSignature(lua.State L)
		{
			Debug.Assert(L == interpreter._L);
			IReflect klass; object target;
			int id=luanet.checkudata(L,1,"luaNet_class");
			if(id!=-1)
			{
				klass=(IReflect)objects[id];
				target=null;
			}
			else
			{
				target=getRawNetObject(L,1);
				if(target==null)
				{
					throwError(L,"get_method_bysig: first arg is not type or object reference");
					lua.pushnil(L);
					return 1;
				}
				klass=target.GetType();
			}
			string methodName=lua.tostring(L,2);
			Type[] signature=new Type[lua.gettop(L)-2];
			for(int i=0;i<signature.Length;i++)
				signature[i]=FindType(lua.tostring(L,i+3));
			try
			{
				//CP: Added ignore case
				MethodInfo method=klass.GetMethod(methodName,BindingFlags.Public | BindingFlags.Static |
					BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase, null, signature, null);
				pushFunction(L,new lua.CFunction((new LuaMethodWrapper(this,target,klass,method)).call));
			}
			catch(Exception e)
			{
				throwError(L,e);
				lua.pushnil(L);
			}
			return 1;
		}
		/// <summary>Implementation of get_constructor_bysig. Returns nil if no matching constructor is found.</summary>
		private int getConstructorSignature(lua.State L)
		{
			Debug.Assert(L == interpreter._L);
			IReflect klass=null;
			int id=luanet.checkudata(L,1,"luaNet_class");
			if(id!=-1)
			{
				klass=(IReflect)objects[id];
			}
			if(klass==null)
			{
				throwError(L,"get_constructor_bysig: first arg is invalid type reference");
			}
			Type[] signature=new Type[lua.gettop(L)-1];
			for(int i=0;i<signature.Length;i++)
				signature[i]=FindType(lua.tostring(L,i+2));
			try
			{
				ConstructorInfo constructor=klass.UnderlyingSystemType.GetConstructor(signature);
				pushFunction(L,new lua.CFunction((new LuaMethodWrapper(this,null,klass,constructor)).call));
			}
			catch(Exception e)
			{
				throwError(L,e);
				lua.pushnil(L);
			}
			return 1;
		}

		private Type typeOf(lua.State L, int idx)
		{
			Debug.Assert(L == interpreter._L);
			int id=luanet.checkudata(L,idx,"luaNet_class");
			if (id == -1) return null;

			var pt = (ProxyType)objects[id];
			return pt.UnderlyingSystemType;
		}

		public static int pushError(lua.State L, string msg)
		{
			lua.pushnil(L);
			lua.pushstring(L,msg);
			return 2;
		}

		/// <summary>Implementation of ctype.</summary>
		private int ctype(lua.State L)
		{
			Debug.Assert(L == interpreter._L);
			Type t = typeOf(L,1);
			if (t == null) {
				return pushError(L,"not a CLR class");
			}
			pushObject(L,t,"luaNet_metatable");
			return 1;
		}

		/// <summary>Implementation of enum.</summary>
		private int enumFromInt(lua.State L)
		{
			Debug.Assert(L == interpreter._L);
			Type t = typeOf(L,1);
			if (t == null || ! t.IsEnum) {
				return pushError(L,"not an enum");
			}
			object res = null;
			LuaType lt = lua.type(L,2);
			if (lt == LuaType.Number) {
				int ival = (int)lua.tonumber(L,2);
				res = Enum.ToObject(t,ival);
			} else
			if (lt == LuaType.String) {
				string sflags = lua.tostring(L,2);
				string err = null;
				try {
					res = Enum.Parse(t,sflags);
				} catch (ArgumentException e) {
					err = e.Message;
				}
				if (err != null) {
					return pushError(L,err);
				}
			} else {
				return pushError(L,"second argument must be a integer or a string");
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
			Debug.Assert(L == interpreter._L); StackAssert.Start(L);
			// Pushes nil
			if(o==null)
			{
				lua.pushnil(L);
				StackAssert.End(1);
				return;
			}

			// Object already in the list of Lua objects? Push the stored reference.
			int id;
			if (objectsBackMap.TryGetValue(o, out id))
			{
				luaL.getmetatable(L,"luaNet_objects");
				lua.rawgeti(L,-1,id);

				// Note: starting with lua5.1 the garbage collector may remove weak reference items (such as our luaNet_objects values) when the initial GC sweep
				// occurs, but the actual call of the __gc finalizer for that object may not happen until a little while later.  During that window we might call
				// this routine and find the element missing from luaNet_objects, but collectObject() has not yet been called.  In that case, we go ahead and call collect
				// object here
				// did we find a non nil object in our table? if not, we need to call collect object
				if (!lua.isnil(L, -1))
				{
					lua.remove(L, -2); // drop the metatable - we're going to leave our object on the stack
					StackAssert.End(1);
					return;
				}

				// MetaFunctions.dumpStack(this, L);
				lua.pop(L, 2); // remove the nil object value and the metatable

				collectObject(o, id);            // Remove from both our tables and fall out to get a new ID
			}

			pushNewObject(L, o, addObject(o), metatable);
			StackAssert.End(1);
		}


		/// <summary>[-0, +1, m] Pushes a new object into the Lua stack with the provided metatable</summary>
		private void pushNewObject(lua.State L,object o,int id,string metatable)
		{
			Debug.Assert(L == interpreter._L); StackAssert.Start(L);

			luanet.newudata(L,id);

			if (metatable != "luaNet_metatable")
				luaL.getmetatable(L, metatable);
			// Gets or creates the metatable for the object's type
			else if (luaL.newmetatable(L,o.GetType().AssemblyQualifiedName))
			{
				lua.pushstring(L,"cache");
				lua.newtable(L);
				lua.rawset(L,-3);

				lua.pushlightuserdata(L,luanet.gettag());
				lua.pushnumber(L,1); // todo: is this a dummy value? pushbool would probably be better
				lua.rawset(L,-3);

				lua.pushstring(L,"__index");
				lua.pushstring(L,"luaNet_indexfunction");
				lua.rawget(L, LUA.REGISTRYINDEX);
				lua.rawset(L,-3);

				lua.pushstring(L,"__gc");
				lua.pushcfunction(L,metaFunctions.gcFunction);
				lua.rawset(L,-3);

				lua.pushstring(L,"__tostring");
				lua.pushcfunction(L,metaFunctions.toStringFunction);
				lua.rawset(L,-3);

				lua.pushstring(L,"__newindex");
				lua.pushcfunction(L,metaFunctions.newindexFunction);
				lua.rawset(L,-3);
			}

			Debug.Assert(lua.istable(L, -1));
			lua.setmetatable(L,-2);

			luaL.getmetatable(L,"luaNet_objects");
			lua.pushvalue(L,-2);
			lua.rawseti(L,-2,id); // luaNet_objects[id] = o
			lua.pop(L,1);
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
			// The other variant of collectObject might have gotten here first, in that case we will silently ignore the missing entry
			object o;
			if (objects.TryGetValue(id, out o))
				collectObject(o, id);
		}


		/// <summary>[nothrow] Given an object reference, remove it from our maps</summary>
		void collectObject(object o, int id)
		{
			Debug.Assert(o != null);
			// Debug.WriteLine("Removing " + o.ToString() + " @ " + udata);
			objects.Remove(id);
			objectsBackMap.Remove(o);
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
			objectsBackMap[obj] = id;

			return id;
		}



		/// <summary>[-0, +0, m] Gets an object from the Lua stack according to its Lua type.</summary>
		internal unsafe object getObject(lua.State L,int index)
		{
			Debug.Assert(L == interpreter._L);
			switch(lua.type(L,index))
			{
			case LuaType.None:
				throw new ArgumentException("index points to an empty stack position", "index");

			case LuaType.Nil:
				return null;

			case LuaType.Number:
				return lua.tonumber(L,index);

			case LuaType.String:
				return lua.tostring(L,index);

			case LuaType.Boolean:
				return lua.toboolean(L,index);

			case LuaType.Table:
				return getTable(L,index);

			case LuaType.Function:
				return getFunction(L,index);

			case LuaType.Userdata:
				return getNetObject(L,index) ?? getUserData(L,index);

			case LuaType.LightUserdata:
				return new IntPtr(lua.touserdata(L, index));

			case LuaType.Thread:
				return LuaType.Thread; // not supported

			default:
				// all LuaTypes have a case, so this shouldn't happen
				throw new Exception("incorrect or corrupt program");
			}
		}
		/// <summary>[-0, +0, m] Gets the table in the <paramref name="index"/> position of the Lua stack.</summary>
		internal LuaTable getTable(lua.State L,int index)
		{
			Debug.Assert(L == interpreter._L);
			lua.pushvalue(L,index);
			return new LuaTable(L,interpreter);
		}
		/// <summary>[-0, +0, m] Gets the userdata in the <paramref name="index"/> position of the Lua stack.</summary>
		internal LuaUserData getUserData(lua.State L,int index)
		{
			Debug.Assert(L == interpreter._L);
			lua.pushvalue(L,index);
			return new LuaUserData(L,interpreter);
		}
		/// <summary>[-0, +0, m] Gets the function in the <paramref name="index"/> position of the Lua stack.</summary>
		internal LuaFunction getFunction(lua.State L,int index)
		{
			Debug.Assert(L == interpreter._L);
			lua.pushvalue(L,index);
			return new LuaFunction(L,interpreter);
		}
		/// <summary>[-0, +0, -] Gets the CLR object in the <paramref name="index"/> position of the Lua stack. Returns delegates as Lua functions.</summary>
		internal object getNetObject(lua.State L,int index)
		{
			Debug.Assert(L == interpreter._L);
			int id=luanet.tonetobject(L,index);
			if(id!=-1)
				return objects[id];
			else
				return null;
		}
		/// <summary>Gets the CLR object in the index positon of the Lua stack. Returns delegates as-is.</summary>
		internal object getRawNetObject(lua.State L,int index)
		{
			Debug.Assert(L == interpreter._L);
			int id=luanet.rawnetobj(L,index);
			if (id == -1) return null;
			return objects[id];
		}

		/// <summary>Gets the values from the provided index to the top of the stack and returns them in an array.</summary>
		internal object[] popValues(lua.State L,int oldTop)
		{
			return popValues(L, oldTop, null);
		}
		/// <summary>
		/// Gets the values from the provided index to the top of the stack and returns them in an array,
		/// casting them to the provided types.
		/// </summary>
		internal object[] popValues(lua.State L,int oldTop,Type[] popTypes)
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
			if(o is ILuaGeneratedType)
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

		/// <summary>[-0, +1, m] Pushes the object into the Lua stack according to its type.</summary>
		internal void push(lua.State L, object o)
		{
			Debug.Assert(L == interpreter._L);
			if (o == null)
			{
				lua.pushnil(L);
				return;
			}
			var conv = o as IConvertible;
			if (conv != null)
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

			// disallow all reflection
			{
				var type = o.GetType();
				do
				{
					if (type.Namespace.StartsWith("System.Reflection", StringComparison.Ordinal))
					{
						lua.pushstring(L, o.ToString());
						return;
					}
					type = type.BaseType;
				} while (type != null);
			}

			pushObject(L,o,"luaNet_metatable");
		}

		/// <summary>Checks if the method matches the arguments in the Lua stack, getting the arguments if it does.</summary>
		internal bool matchParameters(lua.State L,MethodBase method,ref MethodCache methodCache)
		{
			Debug.Assert(L == interpreter._L);
			return metaFunctions.matchParameters(L,method,ref methodCache);
		}

		/// <summary>Note: this disposes <paramref name="luaParamValue"/> if it is a table.</summary>
		internal Array tableToArray(object luaParamValue, Type paramArrayType) {
			return metaFunctions.TableToArray(luaParamValue,paramArrayType);
		}
	}
}