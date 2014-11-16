using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using LuaInterface.LuaAPI;

namespace LuaInterface
{
	/// <summary>
	/// Main class of LuaInterface
	/// Object-oriented wrapper to Lua API
	/// </summary>
	/// <remarks>
	/// Author: Fabio Mascarenhas
	/// </remarks>
	public class Lua : IDisposable
	{
		internal lua.State _L;
		internal ObjectTranslator translator;

		// lockCallback, unlockCallback; used by debug code commented out for now

		public Lua() : this(false) { }

		/// <param name="allowDebug">Specify true to keep LuaInterface from removing debug functions.</param>
		public Lua(bool allowDebug)
		{
			var L = luaL.newstate();
			if (L.IsNull)
				throw new OutOfMemoryException("Failed to allocate a new Lua state.");

			// Load libraries
			luaL.openlibs(L);

			// remove potentially exploitable debug functions
			if (!allowDebug) luaL.dostring(L, @"
				local d = debug
				debug = {
					getinfo = d.getinfo,
					traceback = d.traceback,
				}
			");

			lua.atpanic(L, panicCallback);
			Init(L);
		}

		const string _LuaInterfaceMarker = "LUAINTERFACE LOADED";
		private readonly bool _StatePassed;

		/// <summary>Wrap around an existing lua_State. CAUTION: Multiple LuaInterface.Lua instances can't share the same lua state.</summary>
		public Lua(lua.State L)
		{
			if (L.IsNull) throw new ArgumentNullException("L");

			 // Check for existing LuaInterface marker
			lua.pushstring(L, _LuaInterfaceMarker);
			lua.rawget(L, LUA.REGISTRYINDEX);
			if(luanet.popboolean(L))
				throw new LuaException("There is already a LuaInterface.Lua instance associated with this Lua state");

			lua.pushvalue(L, LUA.GLOBALSINDEX); // todo: why is this here?

			_StatePassed = true;
			Init(L);
		}

		void Init(lua.State L)
		{
			_L = L;

			// Add LuaInterface marker
			lua.pushstring(L, _LuaInterfaceMarker);
			lua.pushboolean(L, true);
			lua.rawset(L, LUA.REGISTRYINDEX);

			translator = new ObjectTranslator(this);

			lua.getglobal(L, "tostring");
			tostring_ref = luaL.@ref(L);
		}
		internal int tostring_ref { get; private set; }

		// We need to keep this in a managed reference so the delegate doesn't get garbage collected
		static readonly lua.CFunction panicCallback = L =>
		{
			// string desc = lua.tostring(L, 1);
			string reason = String.Format("unprotected error in call to Lua API ({0})", lua.tostring(L, -1));

		   //        lua_tostring(L, -1);

			throw new LuaException(reason);
		};



		/// <summary>
		/// Assuming we have a Lua error string sitting on the stack, throw a C# exception out to the user's app
		/// </summary>
		/// <exception cref="LuaScriptException">Thrown if the script caused an exception</exception>
		internal LuaScriptException ExceptionFromError(int oldTop)
		{
			object err = translator.getObject(_L, -1);
			lua.settop(_L, oldTop);

			// A pre-wrapped exception - just rethrow it (stack trace of InnerException will be preserved)
			var luaEx = err as LuaScriptException;
			if (luaEx != null) return luaEx;

			// A non-wrapped Lua error (best interpreted as a string) - wrap it and throw it
			if (err == null) err = "Unknown Lua Error";
			return new LuaScriptException(err.ToString(), "");
		}



		/// <summary>
		/// Convert C# exceptions into Lua errors
		/// </summary>
		/// <returns>num of things on stack</returns>
		/// <param name="e">null for no pending exception</param>
		internal int SetPendingException(Exception e)
		{
			Exception caughtExcept = e;

			if (caughtExcept != null)
			{
				translator.throwError(_L, caughtExcept);
				lua.pushnil(_L);

				return 1;
			}
			else
				return 0;
		}

		// incremented whenever execution passes from CLR to Lua and decremented when it returns
		internal uint _executing = 0;

		/// <summary>True while a script is being executed</summary>
		public bool IsExecuting { get { return _executing != 0; } }

		#region Execution

		/// <summary>Loads a Lua chunk from a string.</summary>
		public LuaFunction LoadString(string chunk) { return LoadString(chunk, chunk); }
		/// <summary>Loads a Lua chunk from a string.</summary>
		public LuaFunction LoadString(string chunk, string name)
		{
			int oldTop = lua.gettop(_L);

			++_executing;
			try
			{
				if (luaL.loadbuffer(_L, chunk, name) != LuaStatus.Ok)
					throw ExceptionFromError(oldTop);
			}
			finally { checked { --_executing; } }

			LuaFunction result = translator.getFunction(_L, -1);
			translator.popValues(_L, oldTop);

			return result;
		}

		/// <summary>Loads a Lua chunk from a file. If <paramref name="fileName"/> is null, the chunk will be loaded from standard input.</summary>
		public LuaFunction LoadFile(string fileName)
		{
			int oldTop = lua.gettop(_L);
			if (luaL.loadfile(_L, fileName) != LuaStatus.Ok)
				throw ExceptionFromError(oldTop);

			LuaFunction result = translator.getFunction(_L, -1);
			lua.settop(_L, oldTop);

			return result;
		}


		/// <summary>Excutes a Lua chunk and returns all the chunk's return values in an array</summary>
		public object[] DoString(string chunk)
		{
			return DoString(chunk,chunk);
		}

		/// <summary>Executes a Lua chunk</summary>
		/// <param name="chunk">Chunk to execute</param>
		/// <param name="chunkName">The name to pass to lua_load, which will be stored in lua_Debug.source and used in error messages.</param>
		/// <returns>all the chunk's return values in an array.</returns>
		public object[] DoString(string chunk, string chunkName)
		{
			int oldTop = lua.gettop(_L);
			++_executing;
			try
			{
				var status = luaL.loadbuffer(_L, chunk, chunkName);
				if (status == LuaStatus.Ok)
				{
					status = lua.pcall(_L, 0, LUA.MULTRET, 0);
					if (status == LuaStatus.Ok)
						return translator.popValues(_L, oldTop);
				}
				throw ExceptionFromError(oldTop);
			}
			finally { checked { --_executing; } }
		}
		/// <summary>
		/// Excutes a Lua chunk and returns all the chunk's return values in an array.
		/// This is for cases where the file was read into a string manually at an earlier time and now needs to be executed.
		/// The provided filename should include the path. It will be used for error messages and other debugging purposes.
		/// </summary>
		public object[] DoStringFromFile(string chunk, string fileName)
		{
			return DoString(chunk, "@"+fileName); // the same naming format is used by luaL_loadfile internally
		}
		/// <summary>
		/// Loads a Lua chunk from a string.
		/// This is for cases where the file was read into a string manually at an earlier time and now needs to be executed.
		/// The provided filename should include the path. It will be used for error messages and other debugging purposes.
		/// </summary>
		public LuaFunction LoadStringFromFile(string chunk, string fileName)
		{
			return LoadString(chunk, "@"+fileName);
		}

		static readonly lua.CFunction tracebackFunction = L =>
		{
			// hahaha why
			lua.getglobal(L,"debug");
			lua.getfield(L,-1,"traceback");
			lua.pushvalue(L,1);
			lua.pushnumber(L,2);
			lua.call (L,2,1);
			return 1;
		};

		/// <summary>Excutes a Lua file and returns all the chunk's return values in an array</summary>
		public object[] DoFile(string fileName)
		{
			lua.pushcfunction(_L,tracebackFunction);
			int oldTop=lua.gettop(_L);
			++_executing;
			try
			{
				var status = luaL.loadfile(_L,fileName);
				if (status == LuaStatus.Ok)
				{
					status = lua.pcall(_L, 0, LUA.MULTRET, -2);
					if (status == LuaStatus.Ok)
						return translator.popValues(_L, oldTop);
				}
				throw ExceptionFromError(oldTop);
			}
			finally
			{
				checked { --_executing; }
				lua.settop(_L, oldTop - 1);
			}
		}

		#endregion


		public void CollectGarbage()
		{
			lua.gc(_L, LuaGC.Collect, 0);
		}

		#region Global Variables

		/// <summary>Indexer for global variables from the LuaInterpreter Supports navigation of tables by using . operator</summary>
		public object this[string fullPath]
		{
			get
			{
				return getNestedObject(LUA.GLOBALSINDEX, fullPath.Split('.'));
			}
			set
			{
				setNestedObject(LUA.GLOBALSINDEX, fullPath.Split('.'), value);

				#if GLOBALS_AUTOCOMPLETE
				// Globals auto-complete
				if (value == null)
				{
					// Remove now obsolete entries
					globals.Remove(fullPath);
				}
				else
				{
					// Add new entries
					if (!globals.Contains(fullPath))
						registerGlobal(fullPath, value.GetType(), 0);
				}
				#endif
			}
		}

		#region Globals auto-complete
		#if GLOBALS_AUTOCOMPLETE
		private readonly List<string> globals = new List<string>();
		private bool globalsSorted;

		/// <summary>
		/// An alphabetically sorted list of all globals (objects, methods, etc.) externally added to this Lua instance
		/// </summary>
		/// <remarks>Members of globals are also listed. The formatting is optimized for text input auto-completion.</remarks>
		public IEnumerable<string> Globals
		{
			get
			{
				// Only sort list when necessary
				if (!globalsSorted)
				{
					globals.Sort();
					globalsSorted = true;
				}

				return globals;
			}
		}

		/// <summary>
		/// Adds an entry to <see cref="globals"/> (recursivley handles 2 levels of members)
		/// </summary>
		/// <param name="path">The index accessor path ot the entry</param>
		/// <param name="type">The type of the entry</param>
		/// <param name="recursionCounter">How deep have we gone with recursion?</param>
		private void registerGlobal(string path, Type type, int recursionCounter)
		{
			// If the type is a global method, list it directly
			if (type == typeof(lua.CFunction))
			{
				// Format for easy method invocation
				globals.Add(path + "(");
			}
			// If the type is a class or an interface and recursion hasn't been running too long, list the members
			else if ((type.IsClass || type.IsInterface) && type != typeof(string) && recursionCounter < 2)
			{
				#region Methods
				foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
				{
					if (
						// Check that the LuaHideAttribute and LuaGlobalAttribute were not applied
						(method.GetCustomAttributes(typeof(LuaHideAttribute), false).Length == 0) &&
						(method.GetCustomAttributes(typeof(LuaGlobalAttribute), false).Length == 0) &&
						// Exclude some generic .NET methods that wouldn't be very usefull in Lua
						method.Name != "GetType" && method.Name != "GetHashCode" && method.Name != "Equals" &&
						method.Name != "ToString" && method.Name != "Clone" && method.Name != "Dispose" &&
						method.Name != "GetEnumerator" && method.Name != "CopyTo" &&
						!method.Name.StartsWith("get_", StringComparison.Ordinal) &&
						!method.Name.StartsWith("set_", StringComparison.Ordinal) &&
						!method.Name.StartsWith("add_", StringComparison.Ordinal) &&
						!method.Name.StartsWith("remove_", StringComparison.Ordinal))
					{
						// Format for easy method invocation
						string command = path + ":" + method.Name + "(";
						if (method.GetParameters().Length == 0) command += ")";
						globals.Add(command);
					}
				}
				#endregion

				#region Fields
				foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
				{
					if (
						// Check that the LuaHideAttribute and LuaGlobalAttribute were not applied
						(field.GetCustomAttributes(typeof(LuaHideAttribute), false).Length == 0) &&
						(field.GetCustomAttributes(typeof(LuaGlobalAttribute), false).Length == 0))
					{
						// Go into recursion for members
						registerGlobal(path + "." + field.Name, field.FieldType, recursionCounter + 1);
					}
				}
				#endregion

				#region Properties
				foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
				{
					if (
						// Check that the LuaHideAttribute and LuaGlobalAttribute were not applied
						(property.GetCustomAttributes(typeof(LuaHideAttribute), false).Length == 0) &&
						(property.GetCustomAttributes(typeof(LuaGlobalAttribute), false).Length == 0)
						// Exclude some generic .NET properties that wouldn't be very usefull in Lua
						&& property.Name != "Item")
					{
						// Go into recursion for members
						registerGlobal(path + "." + property.Name, property.PropertyType, recursionCounter + 1);
					}
				}
				#endregion
			}
			// Otherwise simply add the element to the list
			else globals.Add(path);

			// List will need to be sorted on next access
			globalsSorted = false;
		}
		#endif
		#endregion

		/// <summary>Gets a reference to the global table.</summary>
		public LuaTable GetGlobals()  { return translator.getTable(_L, LUA.GLOBALSINDEX); }

		/// <summary>Gets a reference to the registry table. (LUA_REGISTRYINDEX)</summary>
		public LuaTable GetRegistry() { return translator.getTable(_L, LUA.REGISTRYINDEX); }

		/// <summary>Gets a numeric global variable</summary>
		public double GetNumber(string fullPath)
		{
			var L = _L;                         StackAssert.Start(L);
			luanet.getnestedfield(L, LUA.GLOBALSINDEX, fullPath);
			CheckType(L, fullPath, LuaType.Number);
			var ret = lua.tonumber(L,-1);
			lua.pop(L,1);                       StackAssert.End();
			return ret;
		}
		/// <summary>Gets a string global variable</summary>
		public string GetString(string fullPath)
		{
			var L = _L;                         StackAssert.Start(L);
			luanet.getnestedfield(L, LUA.GLOBALSINDEX, fullPath);
			CheckType(L, fullPath, LuaType.String);
			var ret = lua.tostring(L,-1);
			lua.pop(L,1);                       StackAssert.End();
			return ret;
		}
		/// <summary>Gets a table global variable</summary>
		public LuaTable GetTable(string fullPath)
		{
			var L = _L;
			luanet.getnestedfield(L, LUA.GLOBALSINDEX, fullPath);
			var type = lua.type(L,-1);
			if (type == LuaType.Nil)
			{
				lua.pop(L,1);
				return null;
			}
			return new LuaTable(L, this);
		}

#if ! __NOGEN__
		/// <summary>Gets a table global variable as an object implementing the interfaceType interface</summary>
		public object GetTable(Type interfaceType, string fullPath)
		{
			return CodeGeneration.Instance.GetClassInstance(interfaceType, this.GetTable(fullPath));
		}
#endif
		/// <summary>Gets a function global variable</summary>
		public LuaFunction GetFunction(string fullPath)
		{
			var L = _L;                         StackAssert.Start(L);
			luanet.getnestedfield(L, LUA.GLOBALSINDEX, fullPath);
			switch (lua.type(L,-1))
			{
			case LuaType.Function:              StackAssert.End(1);
				return new LuaFunction(L, this);

			case LuaType.Userdata:
				var o = translator.getNetObject(L, -1) as lua.CFunction;
				if (o == null) break;
				lua.pop(L,1);                       StackAssert.End();
				return new LuaFunction(o, this);

			case LuaType.Nil:
				lua.pop(L,1);                       StackAssert.End();
				return null;
			}
			lua.pop(L,1);                       StackAssert.End();
			throw NewTypeException(fullPath, LuaType.Function);
		}
		/// <summary>Gets a function global variable as a delegate of type delegateType</summary>
		public Delegate GetFunction(Type delegateType,string fullPath)
		{
#if __NOGEN__
			translator.throwError(L,"function delegates not implemented");
			return null;
#else
			return CodeGeneration.Instance.GetDelegate(delegateType, this.GetFunction(fullPath));
#endif
		}


		/// <summary>[-(0|1), +0, v]</summary>
		static void CheckType(lua.State L, string fullPath, LuaType type)
		{
			CheckType(L, fullPath, lua.type(L,-1), type);
		}
		/// <summary>[-(0|1), +0, v]</summary>
		static void CheckType(lua.State L, string fullPath, LuaType actual_type, LuaType type)
		{
			// the old behavior didn't support conversions and threw InvalidCastException
			if (actual_type == type) return;
			lua.pop(L, 1);
			throw NewTypeException(fullPath, type);
		}
		static InvalidCastException NewTypeException(string fullPath, LuaType type)
		{
			return new InvalidCastException(String.Format("Lua value at \"{0}\" was not a {1}.", fullPath, type.ToString().ToLowerInvariant()));
		}

		#endregion

		internal static LuaException NewCrossInterpreterError(LuaBase o)
		{
			return new LuaException(String.Format("Attempted to send a {0} from one Lua instance to another.", o.GetType().Name));
		}


		/// <summary>[-0, +0, e] Navigates a table in the top of the stack, returning the value of the specified field</summary>
		internal object getNestedObject(int index, IEnumerable<string> fields)
		{
			var L = _L;                         StackAssert.Start(L);
			luanet.getnestedfield(L, index, fields);
			var ret = translator.getObject(L,-1);
			lua.pop(L,1);                       StackAssert.End();
			return ret;
		}

		/// <summary>[-0, +0, e] Navigates a table to set the value of one of its fields</summary>
		internal void setNestedObject(int index, string[] path, object val)
		{
			setNestedObject(index, path.Take(path.Length-1), path[path.Length-1], val);
		}
		/// <summary>[-0, +0, e] Navigates a table to set the value of one of its fields</summary>
		internal void setNestedObject(int index, IEnumerable<string> pathWithoutField, string field, object val)
		{
			var L = _L;                         StackAssert.Start(L);
			luanet.getnestedfield(L, index, pathWithoutField);
			translator.push(L,val);
			lua.setfield(L, -2, field);
			lua.pop(L,1);                       StackAssert.End();
		}


		#region Factories

		/// <summary>Creates a new table as a global variable or as a field inside an existing table</summary>
		public void NewTable(string fullPath) { this.NewTable(fullPath, 0, 0); }
		/// <summary>
		/// Creates a new table as a global variable or as a field inside an existing table.
		/// The new table has space pre-allocated for <paramref name="narr"/> array elements and <paramref name="nrec"/> non-array elements.
		/// </summary>
		public void NewTable(string fullPath, int narr, int nrec)
		{
			var L = _L;                         StackAssert.Start(L);
			string[] path = fullPath.Split('.');
			luanet.getnestedfield(L, LUA.GLOBALSINDEX, path.Take(path.Length-1));
			lua.createtable(L, narr, nrec);
			lua.setfield(L, -2, path[path.Length-1]);
			lua.pop(L,1);                       StackAssert.End();
		}

		/// <summary>
		/// Creates a new unnamed table.
		/// The table will have <see cref="LuaTable.IsOrphaned"/> set to <see langword="true"/> by default.
		/// </summary>
		public LuaTable NewTable() { return this.NewTable(0, 0); }
		/// <summary>
		/// Creates a new unnamed table.
		/// The new table has space pre-allocated for <paramref name="narr"/> array elements and <paramref name="nrec"/> non-array elements.
		/// The table reference will have <see cref="LuaTable.IsOrphaned"/> set to <see langword="true"/> by default.
		/// </summary>
		public LuaTable NewTable(int narr, int nrec)
		{
			var L = _L;
			lua.createtable(L, narr, nrec);
			return new LuaTable(L, this) {IsOrphaned = true};
		}

		/// <summary>Creates a new unnamed userdata and returns a reference to it.</summary>
		/// <param name="size">The size of the userdata, in bytes.</param>
		/// <param name="metatable">A metatable to assign to the userdata, or null.</param>
		public LuaUserData NewUserData(int size, LuaTable metatable) { return NewUserData(size.ToSizeType(), metatable); }
		/// <summary>Creates a new unnamed userdata and returns a reference to it.</summary>
		/// <param name="size">The size of the userdata, in bytes.</param>
		/// <param name="metatable">A metatable to assign to the userdata, or null.</param>
		public LuaUserData NewUserData(UIntPtr size, LuaTable metatable)
		{
			var L = _L;
			var oldTop = lua.gettop(L);
			try
			{
				lua.newuserdata(L, size);
				if (metatable != null)
				{
					if (metatable.Owner != this) throw NewCrossInterpreterError(metatable);
					metatable.push(L);
					lua.setmetatable(L, -2);
				}
			}
			catch { lua.settop(L, oldTop); throw; }
			return new LuaUserData(L, this);
		}
		/// <summary>Creates a new userdata object with the same size and data as <paramref name="contents"/> and the specified metatable.</summary>
		public LuaUserData NewUserData(byte[] contents, LuaTable metatable)
		{
			var ud = NewUserData(contents.Length, metatable);
			ud.Contents = contents;
			return ud;
		}

		/// <summary>Registers an object's method as a Lua function (global or table field) The method may have any signature</summary>
		public LuaFunction RegisterFunction(string path, object target, MethodBase function)
		{
			var wrapper = new lua.CFunction(new LuaMethodWrapper(translator,target,function.DeclaringType,function).call);
			if (path != null) this[path] = wrapper;
			return new LuaFunction(wrapper, this);
		}

		/// <summary>Generates a Lua function which matches the behavior of Lua's standard print function but sends the strings to a specified delegate instead of stdout.
		/// <example><code>
		/// lua["print"] = lua.NewPrintFunction((lua, args) => Console.WriteLine(string.Join("\t", args)));
		/// </code></example></summary>
		/// <param name="callback">The function to call when the print function is called. It is passed the Lua instance that called it and an array of strings corresponding to the print function's arguments.</param>
		public LuaFunction NewPrintFunction(Action<Lua, string[]> callback)
		{
			lua.CFunction func = L =>
			{
				Debug.Assert(L == _L);

				// direct copy-paste of Lua's luaB_print function

				int n = lua.gettop(L);  /* number of arguments */
				var results = new string[n];
				lua.getglobal(L, "tostring");
				for (int i=1; i<=n; ++i)
				{
					lua.pushvalue(L, -1);  /* function to be called */
					lua.pushvalue(L, i);   /* value to print */
					lua.call(L, 1, 1);
					string s = lua.tostring(L, -1);  /* get result */
					if (s == null)
						return luaL.error(L, "'tostring' must return a string");
					results[i-1] = s;
					lua.pop(L, 1);  /* pop result */
				}
				Debug.Assert(lua.gettop(L) == n + 1);

				try { callback(this, results); }
				catch (Exception e) { this.translator.throwError(L, e); }

				return 0;
			};
			return new LuaFunction(func, this);
		}

		#endregion

		#region IDisposable

		~Lua()
		{
			Dispose(false);
			leaked();
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (disposing && translator != null)
			{
				translator.pendingEvents.Dispose();
				translator = null;
			}

			if (!this._StatePassed && !_L.IsNull)
				lua.close(_L);

			_L = lua.State.Null;
			// setting this to zero is important. LuaBase checks for this before disposing a reference.
			// this way, it's possible to safely dispose/finalize Lua before disposing/finalizing LuaBase objects.
			// also, it makes use after disposal slightly less dangerous (BUT NOT COMPLETELY)
		}

		#endregion

		#region LeakCount

		#if DEBUG
		/// <summary>The number of LuaInterface IDisposable objects that were disposed by the garbage collector.</summary>
		public static uint LeakCount { get { return g_leak_count; } }

		/// <summary>Gets the current <see cref="LeakCount"/> and resets the counter to 0.</summary>
		public static uint PopLeakCount()
		{
			var count = g_leak_count;
			g_leak_count = 0;
			return count;
		}

		private static uint g_leak_count = 0;
		#endif

		[Conditional("DEBUG")]
		internal static void leaked()
		{
			#if DEBUG
			checked { ++g_leak_count; }
			#endif
		}
		[Conditional("DEBUG")]
		internal static void leaked(int reference)
		{
			#if DEBUG
			if (reference >= LuaRefs.Min) checked { ++g_leak_count; }
			#endif
		}

		#endregion
	}
}