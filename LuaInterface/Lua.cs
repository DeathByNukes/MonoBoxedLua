using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using LuaInterface.LuaAPI;
using System.IO;
using System.Threading;

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
			luanet.checkstack(L, LUA.MINSTACK, "new Lua(lua.State)");

			 // Check for existing LuaInterface marker
			lua.pushstring(L, _LuaInterfaceMarker);
			lua.rawget(L, LUA.REGISTRYINDEX);
			bool found_marker = lua.toboolean(L,-1);
			lua.pop(L,1);
			if (found_marker)
				throw new LuaException("There is already a LuaInterface.Lua instance associated with this Lua state");

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
			var err = lua.tostring(L,-1) ?? "Error message was a "+lua.type(L,-1).ToString();
			lua.pop(L, 1);
			Trace.TraceError("Lua panic! "+err);
			throw new LuaException(string.Format("unprotected error in call to Lua API ({0})", err));
			// note: panic completely wiped the old stack!
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



		/// <summary>True while a script is being executed</summary>
		public bool IsExecuting { get { return luanet.infunction(_L); } }

		#region Execution

		/// <summary><para>Lua bytecode created by lua_dump/string.dump is prefixed with this character, which must be present in order for Lua to recognize it as bytecode.</para><para>Bytecode is completely insecure; specially crafted bytecode can write arbitrary memory. You are advised to check untrusted scripts for this prefix and reject them.</para></summary>
		public const char BytecodePrefix = LUA.SIGNATURE_0;

		/// <summary>Loads a Lua chunk from a string.</summary>
		public LuaFunction LoadString(string chunk)
		{
			if (chunk == null) throw new ArgumentNullException("chunk");
			Debug.Assert(chunk.IndexOf('\0') == -1, "must not contain null characters");
			var L = _L;
			luanet.checkstack(L, 1, "Lua.LoadString");
			if (luaL.loadstring(L, chunk) == LUA.ERR.Success)
				return new LuaFunction(L, this);
			else
				throw ExceptionFromError(-2);
		}
		/// <summary>Loads a Lua chunk from a string.</summary>
		public LuaFunction LoadString(string chunk, string name)
		{
			if (chunk == null || name == null) throw new ArgumentNullException(chunk == null ? "chunk" : "name");
			Debug.Assert(chunk.IndexOf('\0') == -1, "must not contain null characters");
			var L = _L;
			luanet.checkstack(L, 1, "Lua.LoadString");
			if (luaL.loadbuffer(L, chunk, name) == LUA.ERR.Success)
				return new LuaFunction(L, this);
			else
				throw ExceptionFromError(-2);
		}

		/// <summary>Loads a Lua chunk from a file. If <paramref name="fileName"/> is null, the chunk will be loaded from standard input.</summary>
		public LuaFunction LoadFile(string fileName)
		{
			if (fileName == null) throw new ArgumentNullException("fileName");
			var L = _L;
			luanet.checkstack(L, 1, "Lua.LoadFile");
			if (luaL.loadfile(L, fileName) == LUA.ERR.Success)
				return new LuaFunction(L, this);
			else
				throw ExceptionFromError(-2);
		}

		/// <summary>Loads a Lua chunk from a buffer.</summary>
		public LuaFunction LoadBuffer(byte[] chunk, string name) { return LoadBuffer(new ArraySegment<byte>(chunk), name); }
		/// <summary>Loads a Lua chunk from a buffer.</summary>
		public unsafe LuaFunction LoadBuffer(ArraySegment<byte> chunk, string name)
		{
			if (chunk.Array == null) throw new ArgumentNullException("chunk");
			// ArraySegment validates the offset and length when it is first constructed
			var L = _L;
			luanet.checkstack(L, 1, "Lua.LoadBuffer");
			LUA.ERR status;
			fixed (byte* array = chunk.Array)
				status = luaL.loadbuffer(L, new IntPtr(array + chunk.Offset), chunk.Count, name);
			if (status == LUA.ERR.Success)
				return new LuaFunction(L, this);
			else
				throw ExceptionFromError(-2);
		}


		/// <summary>Excutes a Lua expression and returns its first return value.</summary>
		public object Eval(string expression) { return Eval(expression, (object[])null); }
		/// <summary>Excutes a Lua expression and returns its first return value. Use <c>...</c> in the expression to refer to the argument(s).</summary>
		public object Eval(string expression, params object[] args)
		{
			expression = "return "+expression;
			var L = _L;
			int arg_count = args == null ? 0 : args.Length;
			luanet.checkstack(L, arg_count+1, "Lua.Eval");
			if (luaL.loadstring(L, expression) == LUA.ERR.Success)
			{
				for (int i = 0; i < arg_count; ++i)
					translator.push(L, args[i]);
				if (lua.pcall(L, arg_count, 1, 0) == LUA.ERR.Success)
				{
					var ret = translator.getObject(L, -1);
					lua.pop(L, 1);
					return ret;
				}
			}
			throw ExceptionFromError(-2); // pop 1
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
			var L = _L;
			luanet.checkstack(L, 1, "Lua.DoString");
			int oldTop = lua.gettop(L);
			if (luaL.loadbuffer(L, chunk, chunkName) == LUA.ERR.Success)
				if (lua.pcall(L, 0, LUA.MULTRET, 0) == LUA.ERR.Success)
					return translator.popValues(L, oldTop);
			throw ExceptionFromError(oldTop);
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
			// copy pasted from lua.c traceback()
			// todo: custom implementation of debug.traceback that fills out a special CLR exception
			if (!lua.isstring(L, 1)) // 'message' not a string?
				return 1; // keep it intact
			lua.getfield(L, LUA.GLOBALSINDEX, "debug");
			if (!lua.istable(L, -1)) {
				lua.pop(L, 1);
				return 1;
			}
			lua.getfield(L, -1, "traceback");
			if (!lua.isfunction(L, -1)) {
				lua.pop(L, 2);
				return 1;
			}
			lua.pushvalue(L, 1); // pass error message
			lua.pushnumber(L, 2); // skip this function and traceback
			lua.call(L, 2, 1); // call debug.traceback
			return 1;
		};

		/// <summary>Excutes a Lua file and returns all the chunk's return values in an array</summary>
		public object[] DoFile(string fileName)
		{
			var L = _L;
			luanet.checkstack(L, 2, "Lua.DoFile");
			int oldTop=lua.gettop(L);
			lua.pushcfunction(L,tracebackFunction); // not removed by pcall
			if (luaL.loadfile(L,fileName) == LUA.ERR.Success)
			if (lua.pcall(L, 0, LUA.MULTRET, -2) == LUA.ERR.Success)
			{
				lua.remove(L, oldTop+1);
				return translator.popValues(L, oldTop);
			}
			throw ExceptionFromError(oldTop);
		}

		/// <summary>Calls <paramref name="function"/> in protected mode. If a Lua error occurs inside the function, it will be caught at this point and converted to a C# exception.</summary>
		public void CPCall(Action function)
		{
			if (function == null) throw new ArgumentNullException("function");
			lua.CFunction wrapper = L =>
			{
				lua.settop(L, 0);
				function();
				return 0;
			};
			if (lua.cpcall(_L, wrapper, default(IntPtr)) != LUA.ERR.Success)
				throw ExceptionFromError(-2);
		}

		#endregion

		/// <summary>Performs a full garbage-collection cycle.</summary>
		public void CollectGarbage()
		{
			lua.gc(_L, LUA.GC.COLLECT, 0);
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
			var L = _L;
			luanet.checkstack(L, 2, "Lua.GetNumber"); StackAssert.Start(L);
			luanet.getnestedfield(L, LUA.GLOBALSINDEX, fullPath);
			CheckType(L, fullPath, LUA.T.NUMBER);
			var ret = lua.tonumber(L,-1);
			lua.pop(L,1);                             StackAssert.End();
			return ret;
		}
		/// <summary>Gets a string global variable</summary>
		public string GetString(string fullPath)
		{
			var L = _L;
			luanet.checkstack(L, 2, "Lua.GetString"); StackAssert.Start(L);
			luanet.getnestedfield(L, LUA.GLOBALSINDEX, fullPath);
			CheckType(L, fullPath, LUA.T.STRING);
			var ret = lua.tostring(L,-1);
			lua.pop(L,1);                             StackAssert.End();
			return ret;
		}
		/// <summary>Gets a table global variable</summary>
		public LuaTable GetTable(string fullPath)
		{
			var L = _L;
			luanet.checkstack(L, 2, "Lua.GetTable");
			luanet.getnestedfield(L, LUA.GLOBALSINDEX, fullPath);
			var type = lua.type(L,-1);
			if (type == LUA.T.NIL)
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
			luanet.checkstack(L, 2, "Lua.GetFunction");
			luanet.getnestedfield(L, LUA.GLOBALSINDEX, fullPath);
			switch (lua.type(L,-1))
			{
			case LUA.T.FUNCTION:              StackAssert.End(1);
				return new LuaFunction(L, this);

			case LUA.T.USERDATA:
				var o = translator.getNetObject(L, -1) as lua.CFunction;
				if (o == null) break;
				lua.pop(L,1);                       StackAssert.End();
				return new LuaFunction(o, this);

			case LUA.T.NIL:
				lua.pop(L,1);                       StackAssert.End();
				return null;
			}
			lua.pop(L,1);                       StackAssert.End();
			throw NewTypeException(fullPath, LUA.T.FUNCTION);
		}
		/// <summary>Gets a function global variable as a delegate of type delegateType</summary>
		public Delegate GetFunction(Type delegateType,string fullPath)
		{
#if __NOGEN__
			throw new NotSupportedException("function delegates not implemented");
#else
			return CodeGeneration.Instance.GetDelegate(delegateType, this.GetFunction(fullPath));
#endif
		}


		/// <summary>[-(0|1), +0, -]</summary><exception cref="InvalidCastException"></exception>
		static void CheckType(lua.State L, string fullPath, LUA.T type)
		{
			CheckType(L, fullPath, lua.type(L,-1), type);
		}
		/// <summary>[-(0|1), +0, -]</summary><exception cref="InvalidCastException"></exception>
		static void CheckType(lua.State L, string fullPath, LUA.T actual_type, LUA.T type)
		{
			// the old behavior didn't support conversions and threw InvalidCastException
			if (actual_type == type) return;
			lua.pop(L, 1);
			throw NewTypeException(fullPath, type);
		}
		static InvalidCastException NewTypeException(string fullPath, LUA.T type)
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
			var L = _L;
			luanet.checkstack(L, 2, "Lua.getNestedObject"); StackAssert.Start(L);
			luanet.getnestedfield(L, index, fields);
			var ret = translator.getObject(L,-1);
			lua.pop(L,1);                                   StackAssert.End();
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
			var L = _L;
			luanet.checkstack(L, 2, "Lua.setNestedObject"); StackAssert.Start(L);
			luanet.getnestedfield(L, index, pathWithoutField);
			translator.push(L,val);
			lua.setfield(L, -2, field);
			lua.pop(L,1);                                   StackAssert.End();
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
			var L = _L;
			luanet.checkstack(L, 2, "Lua.NewTable"); StackAssert.Start(L);
			string[] path = fullPath.Split('.');
			luanet.getnestedfield(L, LUA.GLOBALSINDEX, path.Take(path.Length-1));
			lua.createtable(L, narr, nrec);
			lua.setfield(L, -2, path[path.Length-1]);
			lua.pop(L,1);                            StackAssert.End();
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
			luanet.checkstack(L, 1, "Lua.NewTable");
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
			luanet.checkstack(L, 2, "Lua.NewUserData");
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

		/// <summary>Registers an object's method as a Lua function (global or table field) The method may have any signature.</summary>
		/// <param name="path">The global variable to store it in, or null. Can contain one or more dots to store it as a field inside a table.</param>
		/// <param name="target">The instance to invoke the method on, or null if it is a static method.</param>
		/// <param name="function">The method to register.</param>
		/// <exception cref="ArgumentNullException"><paramref name="path"/> or <paramref name="function"/> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="target"/> was null and <paramref name="function"/> is an instance method, or vice versa.</exception>
		public void RegisterFunction(string path, object target, MethodBase function)
		{
			if (path == null) throw new ArgumentNullException("path");
			if (function == null) throw new ArgumentNullException("function");
			if (function.IsStatic != (target == null)) throw NewMethodTargetError(target);

			this[path] = new lua.CFunction(new LuaMethodWrapper(translator, target, function.DeclaringType, function).call);
		}
		/// <summary>Creates a Lua function from an object's method. The method may have any signature.</summary>
		/// <param name="target">The instance to invoke the method on, or null if it is a static method.</param>
		/// <param name="function">The method to register.</param>
		/// <exception cref="ArgumentNullException"><paramref name="function"/> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="target"/> was null and <paramref name="function"/> is an instance method, or vice versa.</exception>
		public LuaFunction NewFunction(object target, MethodBase function)
		{
			if (function == null) throw new ArgumentNullException("function");
			if (function.IsStatic != (target == null)) throw NewMethodTargetError(target);

			return new LuaFunction(new LuaMethodWrapper(translator, target, function.DeclaringType, function).call, this);
		}
		static ArgumentException NewMethodTargetError(object target)
		{
			return new ArgumentException(target == null
				? "No target instance provided for instance method."
				: "Target instance provided for static method."
				, "target");
		}

		/// <summary>Registers a delegate as a Lua function (global or table field) The delegate may have any signature.</summary>
		/// <param name="path">The global variable to store it in, or null. Can contain one or more dots to store it as a field inside a table.</param>
		/// <param name="function">The delegate to register.</param>
		/// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
		public void RegisterFunction(string path, Delegate function)
		{
			if (path == null) throw new ArgumentNullException("path");
			var method = function.Method;
			this[path] = new lua.CFunction(new LuaMethodWrapper(translator, function.Target, method.DeclaringType, method).call);
		}
		/// <summary>Creates a Lua function from a delegate. The delegate may have any signature.</summary>
		public LuaFunction NewFunction(Delegate function)
		{
			var method = function.Method;
			return new LuaFunction(new LuaMethodWrapper(translator, function.Target, method.DeclaringType, method).call, this);
		}

		/// <summary>Creates a type proxy object just like Lua import_type.</summary>
		public LuaUserData ImportType(Type type)
		{
			if (type == null) throw new ArgumentNullException("type");
			var L = _L;
			luanet.checkstack(L, 1, "Lua.ImportType");
			translator.pushType(L, type);
			return new LuaUserData(L, this);
		}
		/// <summary>Creates a type proxy object just like Lua import_type, assigning it to a global variable named after the type.</summary>
		public void RegisterType(Type type)
		{
			if (type == null) throw new ArgumentNullException("type");
			Debug.Assert(type.Name.IndexOf('.') == -1);
			RegisterType(type.Name, type);
		}
		/// <summary>Creates a type proxy object just like Lua import_type, assigning it to the specified global variable.</summary>
		public void RegisterType(string path, Type type)
		{
			if (path == null) throw new ArgumentNullException("path");
			using (var type_ref = ImportType(type))
				this[path] = type_ref;
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
				catch (Exception e) { return this.translator.throwError(L, e); }

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

		/// <summary><para>Frees the Lua references held by <see cref="LuaBase"/> objects that weren't properly disposed but got cleaned up by the .NET garbage collector.</para><para>Returns the number of leaks that were cleaned up.</para><para>It is not necessary to call this if you are about to dispose the entire <see cref="Lua"/> instance.</para></summary>
		public int CleanLeaks()
		{
			var refs = _leaked_refs;
			lock (refs)
			{
				var L = _L;
				foreach (var r in refs)
					luaL.unref(L, r);
				var count = refs.Count;
				refs.Clear();
				return count;
			}
		}
		internal void Leaked(int reference)
		{
			Debug.Assert(reference >= LUA.MinRef);
			var refs = _leaked_refs;
			if (!Monitor.TryEnter(refs)) // avoid deadlock if the main thread is suspended and is holding the lock
			{
				GC.ReRegisterForFinalize(this); // try again later
				return;
			}
			try
			{
				refs.Add(reference);
				Lua.leaked();
			}
			finally { Monitor.Exit(refs); }
		}
		readonly List<int> _leaked_refs = new List<int>(); // System.Collections.Concurrent isn't available in .NET 3.5


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

		#endregion
	}
}