using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LuaInterface.LuaAPI
{
	using size_t = System.UIntPtr;

	/// <summary>Standard Lua constants with the "LUA_" prefix removed.</summary>
	public static class LUA
	{
		public const int
		REGISTRYINDEX = -10000,
		ENVIRONINDEX  = -10001,
		GLOBALSINDEX  = -10002,

		MULTRET = -1; // option for multiple returns in `lua_pcall' and `lua_call'
	}

	/// <summary>Lua types for the API, returned by the <see cref="lua.type"/> function</summary>
	public enum LuaType
	{
		/// <summary>The type returned when accessing an index outside the stack boundaries.</summary>
		None = -1,
		Nil = 0,
		Boolean,
		LightUserdata,
		Number,
		String,
		Table,
		Function,
		Userdata,
		Thread,
	}


	/// <summary>
	/// P/Invoke wrapper of the Lua API.
	/// Each function's summary starts with an indicator like this: [-o, +p, x]
	/// The first field, o, is how many elements the function pops from the stack.
	/// The second field, p, is how many elements the function pushes onto the stack. (Any function always pushes its results after popping its arguments.)
	/// A field in the form x|y means the function can push (or pop) x or y elements, depending on the situation;
	/// an interrogation mark '?' means that we cannot know how many elements the function pops/pushes by looking only at its arguments (e.g., they may depend on what is on the stack).
	/// The third field, x, tells whether the function may throw errors:
	/// '-' means the function never throws any error;
	/// 'm' means the function may throw an error only due to not enough memory;
	/// 'e' means the function may throw other kinds of errors;
	/// 'v' means the function may throw an error on purpose.
	/// </summary>
	/// <remarks>
	/// The summaries are mostly just abridged copy pastes of the Lua manual, with a focus on
	/// providing enough information to be useful in understanding and auditing existing code.
	/// You should consult the actual manual at http://www.lua.org/manual/5.1/
	/// whenever the documentation is insufficient.
	/// Confusingly, most parts of the manual say that the error system uses c-style "long jumps".
	/// LuaInterface's custom Lua build uses a C++ compiler, which automatically switches on
	/// C++ style Lua exceptions. The abridged documentation corrects those statements.
	/// </remarks>
	public static unsafe partial class lua
	{
		const string DLL = "lua51.dll";
		const CallingConvention CC = CallingConvention.Cdecl;
		/// <summary>.NET 4.5 AggressiveInlining. Should be auto discarded on older build targets or otherwise ignored.</summary>
		const MethodImplOptions INLINE = (MethodImplOptions) 0x0100;


		#region state manipulation
	} // lua

	/// <summary>Used to handle Lua panics</summary>
	public delegate int LuaFunctionCallback(IntPtr luaState);

	public static unsafe partial class lua
	{
		/// <summary>[-∞, +0, -] Destroys all objects in the given Lua state (calling the corresponding garbage-collection metamethods, if any) and frees all dynamic memory used by this state.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_close"  )] public static extern void close  (IntPtr luaState);
		/// <summary>[-0, +0, -] Sets a new panic function and returns the old one. If an error happens outside any protected environment, Lua calls a panic function and then calls exit(EXIT_FAILURE), thus exiting the host application. Your panic function can avoid this exit by never returning (e.g., throwing an exception). The panic function can access the error message at the top of the stack.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_atpanic")] public static extern void atpanic(IntPtr luaState, LuaFunctionCallback panicf);
		// omitted:
		//   lua_newstate  (use luaL.newstate)
		//   lua_newthread (no threads)

		#endregion

		#region basic stack manipulation

		/// <summary>[-0, +0, -] Returns the index of the top element in the stack. Because indices start at 1, this result is equal to the number of elements in the stack (and so 0 means an empty stack).</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_gettop"    )] public static extern int  gettop    (IntPtr luaState);
		/// <summary>[-?, +?, -] Accepts any acceptable index, or 0, and sets the stack top to this index. If the new top is larger than the old one, then the new elements are filled with nil. If index is 0, then all stack elements are removed.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_settop"    )] public static extern void settop    (IntPtr luaState, int index);
		/// <summary>[-0, +1, -] Pushes a copy of the element at the given valid index onto the stack.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_pushvalue" )] public static extern void pushvalue (IntPtr luaState, int index);
		/// <summary>[-1, +0, -] Removes the element at the given valid index, shifting down the elements above this index to fill the gap.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_remove"    )] public static extern void remove    (IntPtr luaState, int index);
		/// <summary>[-1, +1, -] Moves the top element into the given valid index, shifting up the elements above this index to open space.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_insert"    )] public static extern void insert    (IntPtr luaState, int index);
		/// <summary>[-1, +0, -] Moves the top element into the given position (and pops it), without shifting any element (therefore replacing the value at the given position).</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_replace"   )] public static extern void replace   (IntPtr luaState, int index);
		/// <summary>[-0, +0, m] Ensures that there are at least <paramref name="extra"/> free stack slots in the stack. It returns false if it cannot grow the stack to that size. This function never shrinks the stack; if the stack is already larger than the new size, it is left unchanged.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_checkstack")] public static extern bool checkstack(IntPtr luaState, int extra);
		// omitted: lua_xmove (no threads)

		/// <summary>[-n, +0, -] Pops n elements from the stack.</summary>
		[MethodImpl(INLINE)] public static void pop(IntPtr luaState, int n) { lua.settop(luaState, -n - 1); }

		#endregion

		#region access functions (stack -> C)

		/// <summary>[-0, +0, -] Returns the type of the value in the given acceptable index, or <see cref="LuaType.None"/> for a non-valid index (that is, an index to an "empty" stack position).</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_type")] public static extern LuaType type(IntPtr luaState, int index);
		/// <summary>[-0, +0, -] Returns the name of the type encoded by the value tp, which must be one the values returned by <see cref="lua.type"/>.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_typename")] public static extern string typename(IntPtr luaState, LuaType tp);

		#region lua.is*

		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a number or a string convertible to a number, and false otherwise.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_isnumber"   )] public static extern bool isnumber   (IntPtr luaState, int index);
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a string or a number (which is always convertible to a string), and false otherwise.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_isstring"   )] public static extern bool isstring   (IntPtr luaState, int index);
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a C function, and false otherwise.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_iscfunction")] public static extern bool iscfunction(IntPtr luaState, int index);
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a userdata (either full or light), and false otherwise.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_isuserdata" )] public static extern bool isuserdata (IntPtr luaState, int index);
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a function (either C or Lua), and false otherwise.</summary>
		public static bool isfunction     (IntPtr luaState, int index) { return lua.type(luaState,index) == LuaType.Function; }
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a table, and false otherwise.</summary>
		public static bool istable        (IntPtr luaState, int index) { return lua.type(luaState,index) == LuaType.Table; }
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a light userdata, and false otherwise.</summary>
		public static bool islightuserdata(IntPtr luaState, int index) { return lua.type(luaState,index) == LuaType.LightUserdata; }
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is nil, and false otherwise.</summary>
		public static bool isnil          (IntPtr luaState, int index) { return lua.type(luaState,index) == LuaType.Nil; }
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index has type boolean, and false otherwise.</summary>
		public static bool isboolean      (IntPtr luaState, int index) { return lua.type(luaState,index) == LuaType.Boolean; }
		/// <summary>[-0, +0, -] Returns true if the given acceptable index is not valid (that is, it refers to an element outside the current stack), and false otherwise.</summary>
		public static bool isnone         (IntPtr luaState, int index) { return lua.type(luaState,index) == LuaType.None; }
		/// <summary>[-0, +0, -] Returns true if the given acceptable index is not valid (that is, it refers to an element outside the current stack) or if the value at this index is nil, and false otherwise.</summary>
		public static bool isnoneornil    (IntPtr luaState, int index) { return (int)lua.type(luaState,index) <= 0; }

		#endregion

		/// <summary>[-0, +0, -] Returns the "length" of the value at the given acceptable index: for strings, this is the string length; for tables, this is the result of the length operator ('#'); for userdata, this is the size of the block of memory allocated for the userdata; for other values, it is 0.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_objlen"  )] public static extern size_t objlen  (IntPtr luaState, int index);
		/// <summary>[-0, +0, e] Returns true if the two values in acceptable indices index1 and index2 are equal, following the semantics of the Lua == operator (that is, may call metamethods). Otherwise returns false. Also returns false if any of the indices is non valid.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_equal"   )] public static extern bool   equal   (IntPtr luaState, int index1, int index2);
		/// <summary>[-0, +0, -] Returns true if the two values in acceptable indices index1 and index2 are primitively equal (that is, without calling metamethods). Otherwise returns false. Also returns false if any of the indices are non valid.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_rawequal")] public static extern bool   rawequal(IntPtr luaState, int index1, int index2);
		/// <summary>[-0, +0, e] Returns true if the value at acceptable index index1 is smaller than the value at acceptable index index2, following the semantics of the Lua &lt; operator (that is, may call metamethods). Otherwise returns false. Also returns false if any of the indices is non valid.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_lessthan")] public static extern bool   lessthan(IntPtr luaState, int index1, int index2);

		/// <summary>[-0, +0, -] Converts the Lua value at the given acceptable index to the C type lua_Number. The Lua value must be a number or a string convertible to a number; otherwise, 0 is returned.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_tonumber"   )] public static extern double tonumber   (IntPtr luaState, int index);
		/// <summary>[-0, +0, -] Converts the Lua value at the given acceptable index to a boolean value. Like all tests in Lua, <see cref="lua.toboolean"/> returns true for any Lua value different from false and nil; otherwise it returns false. It also returns false when called with a non-valid index.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_toboolean"  )] public static extern bool   toboolean  (IntPtr luaState, int index);
		/// <summary>[-0, +0, m] Use <see cref="lua.tostring"/> instead.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_tolstring"  )] public static extern void*  tolstring  (IntPtr luaState, int index, out size_t len);
		/// <summary>[-0, +0, -] Converts a value at the given acceptable index to a C function. That value must be a C function; otherwise, returns NULL.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_tocfunction")] public static extern void*  tocfunction(IntPtr luaState, int index);
		/// <summary>[-0, +0, -] If the value at the given acceptable index is a full userdata, returns its block address. If the value is a light userdata, returns its pointer. Otherwise, returns NULL.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_touserdata" )] public static extern void*  touserdata (IntPtr luaState, int index);
		/// <summary>[-0, +0, -] Converts the value at the given acceptable index to a generic C pointer (void*). The value can be a userdata, a table, a thread, or a function; otherwise, <see cref="lua.topointer"/> returns NULL. Typically this function is used only for debug information.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_topointer"  )] public static extern void*  topointer  (IntPtr luaState, int index);
		// omitted:
		//   lua_isthread  (no threads)
		//   lua_tointeger (use System.Convert)
		//   lua_tothread  (no threads)

		/// <summary>
		/// [-0, +0, m] Converts the Lua value at the given acceptable index to a C# string.
		/// If the value is a number, then <see cref="lua.tostring"/> also changes the actual value in the stack to a string!
		/// (This change confuses <see cref="lua.next"/> when <see cref="lua.tostring"/> is applied to keys during a table traversal.)
		/// </summary>
		/// <returns>The Lua value must be a string or a number; otherwise, the function returns null.</returns>
		public static string tostring(IntPtr luaState, int index)
		{
			size_t strlen;
			var str = lua.tolstring(luaState, index, out strlen);
			if (str == null) return null;
			return Marshal.PtrToStringAnsi((IntPtr)str, strlen.ToInt32());
		}

		#endregion

		#region push functions (C -> stack)

		/// <summary>[-0, +1, -] Pushes a nil value onto the stack.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_pushnil"          )] public static extern void pushnil          (IntPtr luaState);
		/// <summary>[-0, +1, -] Pushes a number with value n onto the stack.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_pushnumber"       )] public static extern void pushnumber       (IntPtr luaState, double n);
		/// <summary>[-0, +1, m] Pushes the string pointed to by s with size len onto the stack. Lua makes (or reuses) an internal copy of the given string, so the memory at s can be freed or reused immediately after the function returns. The string can contain embedded zeros.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_pushlstring"      )] public static extern void pushlstring      (IntPtr luaState, IntPtr s, size_t len);
		/// <summary>[-0, +1, -] Pushes a boolean value with value b onto the stack.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_pushboolean"      )] public static extern void pushboolean      (IntPtr luaState, bool b);
		/// <summary>[-0, +1, -] Pushes a light userdata (IntPtr) onto the stack.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_pushlightuserdata")] public static extern void pushlightuserdata(IntPtr luaState, IntPtr p);
		// omitted:
		//   lua_pushvfstring  (use lua.pushstring)
		//   lua_pushfstring   (use lua.pushstring)
		//   lua_pushcclosure  (no C functions/pointers)
		//   lua_pushcfunction (no C functions/pointers)
		//   lua_register      (no C functions/pointers)
		//   lua_pushthread    (no threads)

		// /// <summary>[-0, +1, m] Pushes the zero-terminated string s onto the stack.</summary>
		// [DllImport(LUADLL,CallingConvention=LUACC,EntryPoint="lua_pushstring")] public static extern void pushstring(IntPtr luaState, string s);

		/// <summary>[-0, +1, m] Pushes the string s onto the stack, which can contain embedded zeros.</summary>
		public static void pushstring(IntPtr luaState, string s)
		{
			Debug.Assert(s != null);
			IntPtr ptr = Marshal.StringToHGlobalAnsi(s);
			try { lua.pushlstring(luaState, ptr, s.Length.ToSizeType()); }
			finally { Marshal.FreeHGlobal(ptr); }
		}

		#endregion

		#region get functions (Lua -> stack)

		/// <summary>[-1, +1, e] Pushes onto the stack the value t[k], where t is the value at the given valid index and k is the value at the top of the stack. This function pops the key from the stack (putting the resulting value in its place). As in Lua, this function may trigger a metamethod.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_gettable"    )] public static extern void   gettable    (IntPtr luaState, int index);
		/// <summary>[-0, +1, e] Pushes onto the stack the value t[k], where t is the value at the given valid index. As in Lua, this function may trigger a metamethod.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_getfield"    )] public static extern void   getfield    (IntPtr luaState, int index, string k);
		/// <summary>[-1, +1, -] Similar to <see cref="lua.gettable"/> (pops the key and pushes the result from the table at <paramref name="index"/>) but does a raw access (i.e., without metamethods). (type MUST be a table)</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_rawget"      )] public static extern void   rawget      (IntPtr luaState, int index);
		/// <summary>[-0, +1, -] Pushes onto the stack the value t[n], where t is the value at the given valid index. It does not invoke metamethods. (type MUST be a table)</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_rawgeti"     )] public static extern void   rawgeti     (IntPtr luaState, int index, int n);
		/// <summary>[-0, +1, m] Creates a new empty table and pushes it onto the stack. The new table has space pre-allocated for <paramref name="narr"/> array elements and <paramref name="nrec"/> non-array elements.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_createtable" )] public static extern void   createtable (IntPtr luaState, int narr, int nrec);
		/// <summary>[-0, +1, m] This function allocates a new block of memory with the given size, pushes onto the stack a new full userdata with the block address, and returns this address.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_newuserdata" )] public static extern IntPtr newuserdata (IntPtr luaState, size_t size);
		/// <summary>[-0, +(0|1), -] Pushes onto the stack the metatable of the value at the given acceptable index. If the index is not valid, or if the value does not have a metatable, the function returns false and pushes nothing on the stack.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_getmetatable")] public static extern bool   getmetatable(IntPtr luaState, int index);
		/// <summary>[-0, +1, -] Pushes onto the stack the environment table of the value at the given index.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_getfenv"     )] public static extern void   getfenv     (IntPtr luaState, int index);

		/// <summary>[-0, +1, e] Pushes onto the stack the value of the global <paramref name="name"/>.</summary>
		[MethodImpl(INLINE)] public static void getglobal (IntPtr luaState, string name) { lua.getfield(luaState, LUA.GLOBALSINDEX, name); }
		/// <summary>[-0, +1, m] Creates a new empty table and pushes it onto the stack.</summary>
		[MethodImpl(INLINE)] public static void newtable  (IntPtr luaState) { lua.createtable(luaState, 0, 0); }

		#endregion

		#region set functions (stack -> Lua)

		/// <summary>[-2, +0, e] Does the equivalent to t[k] = v, where t is the value at the given valid index, v is the value at the top of the stack, and k is the value just below the top. This function pops both the key and the value from the stack. As in Lua, this function may trigger a metamethod.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_settable"    )] public static extern void settable    (IntPtr luaState, int index);
		/// <summary>[-1, +0, e] Does the equivalent to t[k] = v, where t is the value at the given valid index and v is the value at the top of the stack. This function pops the value from the stack. As in Lua, this function may trigger a metamethod.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_setfield"    )] public static extern void setfield    (IntPtr luaState, int index, string k);
		/// <summary>[-2, +0, m] Similar to <see cref="lua.settable"/> (pops the value then pops the key and modifies the table at <paramref name="index"/>) but does a raw assignment (i.e., without metamethods). (type MUST be a table)</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_rawset"      )] public static extern void rawset      (IntPtr luaState, int index);
		/// <summary>[-1, +0, m] Does the equivalent of t[n] = v, where t is the value at the given valid index and v is the value at the top of the stack. This function pops the value from the stack. It does not invoke metamethods. (type MUST be a table)</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_rawseti"     )] public static extern void rawseti     (IntPtr luaState, int index, int n);
		/// <summary>[-1, +0, -] Pops a table from the stack and sets it as the new metatable for the value at the given acceptable index.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_setmetatable")] public static extern void setmetatable(IntPtr luaState, int index);
		/// <summary>[-1, +0, -] Pops a table from the stack and sets it as the new environment for the value at the given index. If the value at the given index is neither a function nor a thread nor a userdata, <see cref="lua.setfenv"/> returns false. Otherwise it returns true.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_setfenv"     )] public static extern bool setfenv     (IntPtr luaState, int index);

		/// <summary>[-1, +0, e] Pops a value from the stack and sets it as the new value of global <paramref name="name"/>.</summary>
		[MethodImpl(INLINE)] public static void setglobal(IntPtr luaState, string name) { lua.setfield(luaState, LUA.GLOBALSINDEX, name); }

		#endregion

		#region `load' and `call' functions (load and run Lua code)
	} // lua

	/// <summary>
	/// Lua status codes returned by functions like <see cref="lua.call"/> and <see cref="lua.load"/>.
	/// Each of the functions can only return a subset of these values. See the function's documentation in the Lua manual for details.
	/// </summary>
	public enum LuaStatus
	{
		/// <summary>0: no errors.</summary>
		Ok = 0,
		/// <summary>LUA_ERRRUN: a runtime error.</summary>
		ErrRun = 2,
		/// <summary>LUA_ERRSYNTAX: syntax error during pre-compilation.</summary>
		ErrSyntax,
		/// <summary>LUA_ERRMEM: memory allocation error. For such errors, Lua does not call the error handler function.</summary>
		ErrMem,
		/// <summary>LUA_ERRERR: error while running the error handler function.</summary>
		ErrErr,
		/// <summary>LUA_ERRFILE: cannot open/read the file.</summary>
		ErrFile,
	}

	public static unsafe partial class lua
	{
		/// <summary>The reader function used by <see cref="lua.load"/>. Every time it needs another piece of the chunk, <see cref="lua.load"/> calls the reader, passing along its data parameter. The reader must return a pointer to a block of memory with a new piece of the chunk and set size to the block size. The block must exist until the reader function is called again. To signal the end of the chunk, the reader must return NULL or set size to zero. The reader function may return pieces of any size greater than zero.</summary>
		public delegate byte* Reader(IntPtr luaState, void* data, out size_t size);

		/// <summary>[-(nargs + 1), +nresults, e] Calls a function.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_call" )] public static extern void      call (IntPtr luaState, int nargs, int nresults);
		/// <summary>[-(nargs + 1), +(nresults|1), -] Calls a function in protected mode. If there is any error, <see cref="lua.pcall"/> catches it, pushes a single value on the stack (the error message), and returns an error code.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_pcall")] public static extern LuaStatus pcall(IntPtr luaState, int nargs, int nresults, int errfunc);
		/// <summary>[-0, +1, -] Loads a Lua chunk. If there are no errors, <see cref="lua.load"/> pushes the compiled chunk as a Lua function on top of the stack. Otherwise, it pushes an error message.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_load" )] public static extern LuaStatus load (IntPtr luaState, lua.Reader reader, IntPtr data, string chunkname);
		// omitted:
		//   lua_cpcall (no point in calling C functions)
		//   lua_dump   (would write to unmanaged memory via lua_Writer)

		#endregion

		#region coroutine functions

		// omitted:
		//   lua_yield  (no threads)
		//   lua_resume (no threads)
		//   lua_status (no threads)

		#endregion

		#region garbage-collection function and options
	} // lua

	/// <summary>Options for the Lua API function <see cref="lua.gc"/></summary>
	public enum LuaGC
	{
		/// <summary>Stops the garbage collector.</summary>
		Stop = 0,
		/// <summary>Restarts the garbage collector.</summary>
		Restart,
		/// <summary>Performs a full garbage-collection cycle.</summary>
		Collect,
		/// <summary>Returns the current amount of memory (in Kbytes) in use by Lua.</summary>
		Count,
		/// <summary>Returns the remainder of dividing the current amount of bytes of memory in use by Lua by 1024.</summary>
		CountB,
		/// <summary>Performs an incremental step of garbage collection. The step "size" is controlled by data (larger values mean more steps) in a non-specified way. If you want to control the step size you must experimentally tune the value of data. The function returns 1 if the step finished a garbage-collection cycle.</summary>
		Step,
		/// <summary>Sets data as the new value for the pause of the collector (see §2.10). The function returns the previous value of the pause.</summary>
		SetPause,
		/// <summary>Sets data as the new value for the step multiplier of the collector (see §2.10). The function returns the previous value of the step multiplier.</summary>
		SetStepMul,
	}

	public static unsafe partial class lua
	{
		/// <summary>[-0, +0, e] Controls the garbage collector. This function performs several tasks, according to the value of the parameter <paramref name="what"/>.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_gc")] public static extern int gc(IntPtr luaState, LuaGC what, int data);

		#endregion

		#region miscellaneous functions

		/// <summary>[-1, +0, v] Generates a Lua error. The error message (which can actually be a Lua value of any type) must be on the stack top. This function throws an exception, and therefore never returns.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_error" )] public static extern int  error (IntPtr luaState);
		/// <summary>[-1, +(2|0), e] Pops a key from the stack, and pushes a key-value pair from the table at the given index (the "next" pair after the given key). If there are no more elements in the table, then <see cref="lua.next"/> returns false (and pushes nothing). While traversing a table, do not call <see cref="lua.tostring"/> directly on a key, unless you know that the key is actually a string. Recall that <see cref="lua.tostring"/> changes the value at the given index; this confuses the next call to <see cref="lua.next"/>.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_next"  )] public static extern bool next  (IntPtr luaState, int index);
		/// <summary>[-n, +1, e] Concatenates the n values at the top of the stack, pops them, and leaves the result at the top. If n is 1, the result is the single value on the stack (that is, the function does nothing); if n is 0, the result is the empty string. Concatenation is performed following the usual semantics of Lua.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_concat")] public static extern void concat(IntPtr luaState, int n);

		#endregion


		// omitted:
		//   lua_gethook*** (complete lua_Debug interface omitted)
		//   lua_getinfo
		//   lua_getlocal
		//   lua_getstack
		//   lua_getupvalue
		//   lua_sethook
		//   lua_setlocal
		//   lua_setupvalue
	}
}