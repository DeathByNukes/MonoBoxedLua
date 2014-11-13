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
		private LuaCSFunction registerTableFunction,unregisterTableFunction,getMethodSigFunction,
			getConstructorSigFunction,importTypeFunction,loadAssemblyFunction, ctypeFunction, enumFromIntFunction;

		internal EventHandlerContainer pendingEvents = new EventHandlerContainer();

		public ObjectTranslator(Lua interpreter)
		{
			this.interpreter=interpreter;
			var luaState = interpreter.luaState;
			typeChecker=new CheckType(this);
			metaFunctions=new MetaFunctions(this);
			assemblies=new List<Assembly>();

			importTypeFunction=new LuaCSFunction(this.importType);
			loadAssemblyFunction=new LuaCSFunction(this.loadAssembly);
			registerTableFunction=new LuaCSFunction(this.registerTable);
			unregisterTableFunction=new LuaCSFunction(this.unregisterTable);
			getMethodSigFunction=new LuaCSFunction(this.getMethodSignature);
			getConstructorSigFunction=new LuaCSFunction(this.getConstructorSignature);

			ctypeFunction = new LuaCSFunction(this.ctype);
			enumFromIntFunction = new LuaCSFunction(this.enumFromInt);

			createLuaObjectList(luaState);
			createIndexingMetaFunction(luaState);
			createBaseClassMetatable(luaState);
			createClassMetatable(luaState);
			createFunctionMetatable(luaState);
			setGlobalFunctions(luaState);
		}

		/// <summary>Sets up the list of objects in the Lua side</summary>
		private static void createLuaObjectList(IntPtr luaState)
		{
			StackAssert.Start(luaState);
			lua.pushstring(luaState,"luaNet_objects");
			lua.newtable(luaState);
			lua.newtable(luaState);
			lua.pushstring(luaState,"__mode");
			lua.pushstring(luaState,"v");
			lua.settable(luaState,-3);
			lua.setmetatable(luaState,-2);
			lua.settable(luaState, LUA.REGISTRYINDEX);
			StackAssert.End();
		}
		/// <summary>Registers the indexing function of CLR objects passed to Lua</summary>
		private static void createIndexingMetaFunction(IntPtr luaState)
		{
			StackAssert.Start(luaState);
			lua.pushstring(luaState,"luaNet_indexfunction");
			luaL.dostring(luaState,MetaFunctions.luaIndexFunction);
			//luanet.pushstdcallcfunction(luaState,indexFunction);
			lua.rawset(luaState, LUA.REGISTRYINDEX);
			StackAssert.End();
		}
		/// <summary>Creates the metatable for superclasses (the base field of registered tables)</summary>
		private void createBaseClassMetatable(IntPtr luaState)
		{
			Debug.Assert(luaState == interpreter.luaState); StackAssert.Start(luaState);
			luaL.newmetatable(luaState,"luaNet_searchbase");
			lua.pushstring(luaState,"__gc");
			luanet.pushstdcallcfunction(luaState,metaFunctions.gcFunction);
			lua.settable(luaState,-3);
			lua.pushstring(luaState,"__tostring");
			luanet.pushstdcallcfunction(luaState,metaFunctions.toStringFunction);
			lua.settable(luaState,-3);
			lua.pushstring(luaState,"__index");
			luanet.pushstdcallcfunction(luaState,metaFunctions.baseIndexFunction);
			lua.settable(luaState,-3);
			lua.pushstring(luaState,"__newindex");
			luanet.pushstdcallcfunction(luaState,metaFunctions.newindexFunction);
			lua.settable(luaState,-3);
			lua.pop(luaState,1);                            StackAssert.End();
		}
		/// <summary>Creates the metatable for type references</summary>
		private void createClassMetatable(IntPtr luaState)
		{
			Debug.Assert(luaState == interpreter.luaState); StackAssert.Start(luaState);
			luaL.newmetatable(luaState,"luaNet_class");
			lua.pushstring(luaState,"__gc");
			luanet.pushstdcallcfunction(luaState,metaFunctions.gcFunction);
			lua.settable(luaState,-3);
			lua.pushstring(luaState,"__tostring");
			luanet.pushstdcallcfunction(luaState,metaFunctions.toStringFunction);
			lua.settable(luaState,-3);
			lua.pushstring(luaState,"__index");
			luanet.pushstdcallcfunction(luaState,metaFunctions.classIndexFunction);
			lua.settable(luaState,-3);
			lua.pushstring(luaState,"__newindex");
			luanet.pushstdcallcfunction(luaState,metaFunctions.classNewindexFunction);
			lua.settable(luaState,-3);
			lua.pushstring(luaState,"__call");
			luanet.pushstdcallcfunction(luaState,metaFunctions.callConstructorFunction);
			lua.settable(luaState,-3);
			lua.pop(luaState,1);                            StackAssert.End();
		}
		/// <summary>Registers the global functions used by LuaInterface</summary>
		private void setGlobalFunctions(IntPtr luaState)
		{
			Debug.Assert(luaState == interpreter.luaState); StackAssert.Start(luaState);
			luanet.pushstdcallcfunction(luaState,metaFunctions.indexFunction);
			lua.setglobal(luaState,"get_object_member");
			/*luanet.pushstdcallcfunction(luaState,importTypeFunction);
			lua.setglobal(luaState,"import_type");
			luanet.pushstdcallcfunction(luaState,loadAssemblyFunction);
			lua.setglobal(luaState,"load_assembly");
			luanet.pushstdcallcfunction(luaState,registerTableFunction);
			lua.setglobal(luaState,"make_object");
			luanet.pushstdcallcfunction(luaState,unregisterTableFunction);
			lua.setglobal(luaState,"free_object");*/
			luanet.pushstdcallcfunction(luaState,getMethodSigFunction);
			lua.setglobal(luaState,"get_method_bysig");
			luanet.pushstdcallcfunction(luaState,getConstructorSigFunction);
			lua.setglobal(luaState,"get_constructor_bysig");
			luanet.pushstdcallcfunction(luaState,ctypeFunction);
			lua.setglobal(luaState,"ctype");
			luanet.pushstdcallcfunction(luaState,enumFromIntFunction);
			lua.setglobal(luaState,"enum");                 StackAssert.End();
		}

		/// <summary>Creates the metatable for delegates</summary>
		private void createFunctionMetatable(IntPtr luaState)
		{
			Debug.Assert(luaState == interpreter.luaState); StackAssert.Start(luaState);
			luaL.newmetatable(luaState,"luaNet_function");
			lua.pushstring(luaState,"__gc");
			luanet.pushstdcallcfunction(luaState,metaFunctions.gcFunction);
			lua.settable(luaState,-3);
			lua.pushstring(luaState,"__call");
			luanet.pushstdcallcfunction(luaState,metaFunctions.execDelegateFunction);
			lua.settable(luaState,-3);
			lua.pop(luaState,1);                            StackAssert.End();
		}
		/// <summary>Passes errors (argument e) to the Lua interpreter</summary>
		internal void throwError(IntPtr luaState, object e)
		{
			Debug.Assert(luaState == interpreter.luaState);
			// We use this to remove anything pushed by luaL_where
			int oldTop = lua.gettop(luaState);

			// Stack frame #1 is our C# wrapper, so not very interesting to the user
			// Stack frame #2 must be the lua code that called us, so that's what we want to use
			luaL.where(luaState, 1);
			object[] curlev = popValues(luaState, oldTop);

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

			push(luaState, e);
			lua.error(luaState);
		}
		/// <summary>Implementation of load_assembly. Throws an error if the assembly is not found.</summary>
		private int loadAssembly(IntPtr luaState)
		{
			Debug.Assert(luaState == interpreter.luaState);
			try
			{
				string assemblyName=lua.tostring(luaState,1);

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
				throwError(luaState,e);
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
		private int importType(IntPtr luaState)
		{
			Debug.Assert(luaState == interpreter.luaState);
			string className=lua.tostring(luaState,1);
			Type klass=FindType(className);
			if(klass!=null)
				pushType(luaState,klass);
			else
				lua.pushnil(luaState);
			return 1;
		}
		/// <summary>
		/// Implementation of make_object.
		/// Registers a table (first argument in the stack) as an object subclassing the type passed as second argument in the stack.
		/// </summary>
		private int registerTable(IntPtr luaState)
		{
			Debug.Assert(luaState == interpreter.luaState);
#if __NOGEN__
			throwError(luaState,"Tables as Objects not implemented");
#else
			if(lua.type(luaState,1)==LuaType.Table)
			{
				string superclassName = lua.tostring(luaState, 2);
				if (superclassName != null)
				{
					Type klass = FindType(superclassName);
					if (klass != null)
					{
						// Creates and pushes the object in the stack, setting
						// it as the  metatable of the first argument
						object obj = CodeGeneration.Instance.GetClassInstance(klass, getTable(luaState,1));
						pushObject(luaState, obj, "luaNet_metatable");
						lua.newtable(luaState);
						lua.pushstring(luaState, "__index");
						lua.pushvalue(luaState, -3);
						lua.settable(luaState, -3);
						lua.pushstring(luaState, "__newindex");
						lua.pushvalue(luaState, -3);
						lua.settable(luaState, -3);
						lua.setmetatable(luaState, 1);
						// Pushes the object again, this time as the base field
						// of the table and with the luaNet_searchbase metatable
						lua.pushstring(luaState, "base");
						int index = addObject(obj);
						pushNewObject(luaState, obj, index, "luaNet_searchbase");
						lua.rawset(luaState, 1);
					}
					else
						throwError(luaState, "register_table: can not find superclass '" + superclassName + "'");
				}
				else
					throwError(luaState, "register_table: superclass name can not be null");
			}
			else throwError(luaState,"register_table: first arg is not a table");
#endif
			return 0;
		}
		/// <summary>
		/// Implementation of free_object.
		/// Clears the metatable and the base field, freeing the created object for garbage-collection
		/// </summary>
		private int unregisterTable(IntPtr luaState)
		{
			Debug.Assert(luaState == interpreter.luaState);
			try
			{
				if (!lua.getmetatable(luaState, 1))
					throwError(luaState, "unregister_table: arg is not valid table");
				else
				{
					lua.pushstring(luaState,"__index");
					lua.gettable(luaState,-2);
					object obj=getRawNetObject(luaState,-1);
					if(obj==null) throwError(luaState,"unregister_table: arg is not valid table");
					FieldInfo luaTableField=obj.GetType().GetField("__luaInterface_luaTable");
					if(luaTableField==null) throwError(luaState,"unregister_table: arg is not valid table");
					luaTableField.SetValue(obj,null);
					lua.pushnil(luaState);
					lua.setmetatable(luaState,1);
					lua.pushstring(luaState,"base");
					lua.pushnil(luaState);
					lua.settable(luaState,1);
				}
			}
			catch(Exception e)
			{
				throwError(luaState,e.Message);
			}
			return 0;
		}
		/// <summary>Implementation of get_method_bysig. Returns nil if no matching method is not found.</summary>
		private int getMethodSignature(IntPtr luaState)
		{
			Debug.Assert(luaState == interpreter.luaState);
			IReflect klass; object target;
			int id=luanet.checkudata(luaState,1,"luaNet_class");
			if(id!=-1)
			{
				klass=(IReflect)objects[id];
				target=null;
			}
			else
			{
				target=getRawNetObject(luaState,1);
				if(target==null)
				{
					throwError(luaState,"get_method_bysig: first arg is not type or object reference");
					lua.pushnil(luaState);
					return 1;
				}
				klass=target.GetType();
			}
			string methodName=lua.tostring(luaState,2);
			Type[] signature=new Type[lua.gettop(luaState)-2];
			for(int i=0;i<signature.Length;i++)
				signature[i]=FindType(lua.tostring(luaState,i+3));
			try
			{
				//CP: Added ignore case
				MethodInfo method=klass.GetMethod(methodName,BindingFlags.Public | BindingFlags.Static |
					BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase, null, signature, null);
				pushFunction(luaState,new LuaCSFunction((new LuaMethodWrapper(this,target,klass,method)).call));
			}
			catch(Exception e)
			{
				throwError(luaState,e);
				lua.pushnil(luaState);
			}
			return 1;
		}
		/// <summary>Implementation of get_constructor_bysig. Returns nil if no matching constructor is found.</summary>
		private int getConstructorSignature(IntPtr luaState)
		{
			Debug.Assert(luaState == interpreter.luaState);
			IReflect klass=null;
			int id=luanet.checkudata(luaState,1,"luaNet_class");
			if(id!=-1)
			{
				klass=(IReflect)objects[id];
			}
			if(klass==null)
			{
				throwError(luaState,"get_constructor_bysig: first arg is invalid type reference");
			}
			Type[] signature=new Type[lua.gettop(luaState)-1];
			for(int i=0;i<signature.Length;i++)
				signature[i]=FindType(lua.tostring(luaState,i+2));
			try
			{
				ConstructorInfo constructor=klass.UnderlyingSystemType.GetConstructor(signature);
				pushFunction(luaState,new LuaCSFunction((new LuaMethodWrapper(this,null,klass,constructor)).call));
			}
			catch(Exception e)
			{
				throwError(luaState,e);
				lua.pushnil(luaState);
			}
			return 1;
		}

		private Type typeOf(IntPtr luaState, int idx)
		{
			Debug.Assert(luaState == interpreter.luaState);
			int id=luanet.checkudata(luaState,idx,"luaNet_class");
			if (id == -1) return null;

			var pt = (ProxyType)objects[id];
			return pt.UnderlyingSystemType;
		}

		public static int pushError(IntPtr luaState, string msg)
		{
			lua.pushnil(luaState);
			lua.pushstring(luaState,msg);
			return 2;
		}

		/// <summary>Implementation of ctype.</summary>
		private int ctype(IntPtr luaState)
		{
			Debug.Assert(luaState == interpreter.luaState);
			Type t = typeOf(luaState,1);
			if (t == null) {
				return pushError(luaState,"not a CLR class");
			}
			pushObject(luaState,t,"luaNet_metatable");
			return 1;
		}

		/// <summary>Implementation of enum.</summary>
		private int enumFromInt(IntPtr luaState)
		{
			Debug.Assert(luaState == interpreter.luaState);
			Type t = typeOf(luaState,1);
			if (t == null || ! t.IsEnum) {
				return pushError(luaState,"not an enum");
			}
			object res = null;
			LuaType lt = lua.type(luaState,2);
			if (lt == LuaType.Number) {
				int ival = (int)lua.tonumber(luaState,2);
				res = Enum.ToObject(t,ival);
			} else
			if (lt == LuaType.String) {
				string sflags = lua.tostring(luaState,2);
				string err = null;
				try {
					res = Enum.Parse(t,sflags);
				} catch (ArgumentException e) {
					err = e.Message;
				}
				if (err != null) {
					return pushError(luaState,err);
				}
			} else {
				return pushError(luaState,"second argument must be a integer or a string");
			}
			pushObject(luaState,res,"luaNet_metatable");
			return 1;
		}

		/// <summary>[-0, +1, m] Pushes a type reference into the stack</summary>
		internal void pushType(IntPtr luaState, Type t)
		{
			Debug.Assert(luaState == interpreter.luaState);
			pushObject(luaState,new ProxyType(t),"luaNet_class");
		}
		/// <summary>[-0, +1, m] Pushes a delegate into the stack</summary>
		internal void pushFunction(IntPtr luaState, LuaCSFunction func)
		{
			Debug.Assert(luaState == interpreter.luaState);
			pushObject(luaState,func,"luaNet_function");
		}
		/// <summary>[-0, +1, m] Pushes a CLR object into the Lua stack as an userdata with the provided metatable</summary>
		internal void pushObject(IntPtr luaState, object o, string metatable)
		{
			Debug.Assert(luaState == interpreter.luaState); StackAssert.Start(luaState);
			// Pushes nil
			if(o==null)
			{
				lua.pushnil(luaState);
				StackAssert.End(1);
				return;
			}

			// Object already in the list of Lua objects? Push the stored reference.
			int id;
			if (objectsBackMap.TryGetValue(o, out id))
			{
				luaL.getmetatable(luaState,"luaNet_objects");
				lua.rawgeti(luaState,-1,id);

				// Note: starting with lua5.1 the garbage collector may remove weak reference items (such as our luaNet_objects values) when the initial GC sweep
				// occurs, but the actual call of the __gc finalizer for that object may not happen until a little while later.  During that window we might call
				// this routine and find the element missing from luaNet_objects, but collectObject() has not yet been called.  In that case, we go ahead and call collect
				// object here
				// did we find a non nil object in our table? if not, we need to call collect object
				if (!lua.isnil(luaState, -1))
				{
					lua.remove(luaState, -2); // drop the metatable - we're going to leave our object on the stack
					StackAssert.End(1);
					return;
				}

				// MetaFunctions.dumpStack(this, luaState);
				lua.pop(luaState, 2); // remove the nil object value and the metatable

				collectObject(o, id);            // Remove from both our tables and fall out to get a new ID
			}

			pushNewObject(luaState, o, addObject(o), metatable);
			StackAssert.End(1);
		}


		/// <summary>[-0, +1, m] Pushes a new object into the Lua stack with the provided metatable</summary>
		private void pushNewObject(IntPtr luaState,object o,int id,string metatable)
		{
			Debug.Assert(luaState == interpreter.luaState); StackAssert.Start(luaState);

			luanet.newudata(luaState,id);

			if (metatable != "luaNet_metatable")
				luaL.getmetatable(luaState, metatable);
			// Gets or creates the metatable for the object's type
			else if (luaL.newmetatable(luaState,o.GetType().AssemblyQualifiedName))
			{
				lua.pushstring(luaState,"cache");
				lua.newtable(luaState);
				lua.rawset(luaState,-3);

				lua.pushlightuserdata(luaState,luanet.gettag());
				lua.pushnumber(luaState,1); // todo: is this a dummy value? pushbool would probably be better
				lua.rawset(luaState,-3);

				lua.pushstring(luaState,"__index");
				lua.pushstring(luaState,"luaNet_indexfunction");
				lua.rawget(luaState, LUA.REGISTRYINDEX);
				lua.rawset(luaState,-3);

				lua.pushstring(luaState,"__gc");
				luanet.pushstdcallcfunction(luaState,metaFunctions.gcFunction);
				lua.rawset(luaState,-3);

				lua.pushstring(luaState,"__tostring");
				luanet.pushstdcallcfunction(luaState,metaFunctions.toStringFunction);
				lua.rawset(luaState,-3);

				lua.pushstring(luaState,"__newindex");
				luanet.pushstdcallcfunction(luaState,metaFunctions.newindexFunction);
				lua.rawset(luaState,-3);
			}

			Debug.Assert(lua.istable(luaState, -1));
			lua.setmetatable(luaState,-2);

			luaL.getmetatable(luaState,"luaNet_objects");
			lua.pushvalue(luaState,-2);
			lua.rawseti(luaState,-2,id); // luaNet_objects[id] = o
			lua.pop(luaState,1);
			StackAssert.End(1);
		}
		/// <summary>Gets an object from the Lua stack with the desired type, if it matches, otherwise returns null.</summary>
		internal object getAsType(IntPtr luaState,int index,Type paramType)
		{
			Debug.Assert(luaState == interpreter.luaState);
			ExtractValue extractor = typeChecker.checkType(luaState,index,paramType);
			if (extractor == null) return null;
			return extractor(luaState, index);
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
		internal unsafe object getObject(IntPtr luaState,int index)
		{
			Debug.Assert(luaState == interpreter.luaState);
			switch(lua.type(luaState,index))
			{
			case LuaType.None:
				throw new ArgumentException("index points to an empty stack position", "index");

			case LuaType.Nil:
				return null;

			case LuaType.Number:
				return lua.tonumber(luaState,index);

			case LuaType.String:
				return lua.tostring(luaState,index);

			case LuaType.Boolean:
				return lua.toboolean(luaState,index);

			case LuaType.Table:
				return getTable(luaState,index);

			case LuaType.Function:
				return getFunction(luaState,index);

			case LuaType.Userdata:
				return getNetObject(luaState,index) ?? getUserData(luaState,index);

			case LuaType.LightUserdata:
				return new IntPtr(lua.touserdata(luaState, index));

			case LuaType.Thread:
				return LuaType.Thread; // not supported

			default:
				// all LuaTypes have a case, so this shouldn't happen
				throw new Exception("incorrect or corrupt program");
			}
		}
		/// <summary>[-0, +0, m] Gets the table in the <paramref name="index"/> position of the Lua stack.</summary>
		internal LuaTable getTable(IntPtr luaState,int index)
		{
			Debug.Assert(luaState == interpreter.luaState);
			lua.pushvalue(luaState,index);
			return new LuaTable(luaState,interpreter);
		}
		/// <summary>[-0, +0, m] Gets the userdata in the <paramref name="index"/> position of the Lua stack.</summary>
		internal LuaUserData getUserData(IntPtr luaState,int index)
		{
			Debug.Assert(luaState == interpreter.luaState);
			lua.pushvalue(luaState,index);
			return new LuaUserData(luaState,interpreter);
		}
		/// <summary>[-0, +0, m] Gets the function in the <paramref name="index"/> position of the Lua stack.</summary>
		internal LuaFunction getFunction(IntPtr luaState,int index)
		{
			Debug.Assert(luaState == interpreter.luaState);
			lua.pushvalue(luaState,index);
			return new LuaFunction(luaState,interpreter);
		}
		/// <summary>[-0, +0, -] Gets the CLR object in the <paramref name="index"/> position of the Lua stack. Returns delegates as Lua functions.</summary>
		internal object getNetObject(IntPtr luaState,int index)
		{
			Debug.Assert(luaState == interpreter.luaState);
			int id=luanet.tonetobject(luaState,index);
			if(id!=-1)
				return objects[id];
			else
				return null;
		}
		/// <summary>Gets the CLR object in the index positon of the Lua stack. Returns delegates as-is.</summary>
		internal object getRawNetObject(IntPtr luaState,int index)
		{
			Debug.Assert(luaState == interpreter.luaState);
			int id=luanet.rawnetobj(luaState,index);
			if (id == -1) return null;
			return objects[id];
		}

		/// <summary>Gets the values from the provided index to the top of the stack and returns them in an array.</summary>
		internal object[] popValues(IntPtr luaState,int oldTop)
		{
			return popValues(luaState, oldTop, null);
		}
		/// <summary>
		/// Gets the values from the provided index to the top of the stack and returns them in an array,
		/// casting them to the provided types.
		/// </summary>
		internal object[] popValues(IntPtr luaState,int oldTop,Type[] popTypes)
		{
			Debug.Assert(luaState == interpreter.luaState);
			Debug.Assert(oldTop >= 0, "oldTop must be a value previously returned by lua_gettop");
			int newTop = lua.gettop(luaState);
			if(oldTop >= newTop) return null;

			var values = new object[newTop - oldTop];
			int j = 0;
			if (popTypes == null)
			{
				for(int i = oldTop+1; i <= newTop; ++i)
					values[j++] = getObject(luaState,i);
			}
			else
			{
				int iTypes = popTypes[0] == typeof(void) ? 1 : 0;
				for(int i = oldTop+1; i <= newTop; ++i)
					values[j++] = getAsType(luaState,i,popTypes[iTypes++]);
			}

			lua.settop(luaState,oldTop);
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
		internal void push(IntPtr luaState, object o)
		{
			Debug.Assert(luaState == interpreter.luaState);
			if (o == null)
			{
				lua.pushnil(luaState);
				return;
			}
			var conv = o as IConvertible;
			if (conv != null)
			switch (conv.GetTypeCode())
			{
			case TypeCode.Boolean: lua.pushboolean(luaState, (bool)   conv);       return;
			case TypeCode.Double:  lua.pushnumber (luaState, (double) conv);       return;
			case TypeCode.Single:  lua.pushnumber (luaState, (float)  conv);       return;
			case TypeCode.String:  lua.pushstring (luaState, (string) conv);       return;
			case TypeCode.Char:    lua.pushnumber (luaState, (char)   conv);       return;

			// all the integer types and System.Decimal
			default:               lua.pushnumber (luaState, conv.ToDouble(null)); return;

			case TypeCode.Empty:
			case TypeCode.DBNull:  lua.pushnil    (luaState);                      return;

			case TypeCode.Object:
			case TypeCode.DateTime: break;
			}

			#if ! __NOGEN__
			{	var x = AsILua(o);
				if(x!=null) { var t = x.__luaInterface_getLuaTable();
				              if (t.Owner != interpreter) throw Lua.NewCrossInterpreterError(t);
				              t.push(luaState); return; }  }
			#endif
			{	var x = o as LuaBase;
				if(x!=null) { if (x.Owner != interpreter) throw Lua.NewCrossInterpreterError(x);
				              x.push(luaState); return; }  }

			{	var x = o as LuaCSFunction;
				if(x!=null) { pushFunction(luaState,x); return; }  }

			pushObject(luaState,o,"luaNet_metatable");
		}

		/// <summary>Checks if the method matches the arguments in the Lua stack, getting the arguments if it does.</summary>
		internal bool matchParameters(IntPtr luaState,MethodBase method,ref MethodCache methodCache)
		{
			Debug.Assert(luaState == interpreter.luaState);
			return metaFunctions.matchParameters(luaState,method,ref methodCache);
		}

		/// <summary>Note: this disposes <paramref name="luaParamValue"/> if it is a table.</summary>
		internal Array tableToArray(object luaParamValue, Type paramArrayType) {
			return metaFunctions.TableToArray(luaParamValue,paramArrayType);
		}
	}
}