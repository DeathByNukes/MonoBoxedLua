using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LuaInterface
{
	using size_t = System.UIntPtr;
	public static partial class LuaDLL
	{
		/// <summary>Casts a <see cref="size_t"/> to an <see cref="Int32"/>, throwing <see cref="OverflowException"/> if it is too large.</summary>
		public static int ToInt32(this size_t value)
		{
			return checked((int)value.ToUInt32());
		}
		/// <summary>Casts an <see cref="Int32"/> to a <see cref="size_t"/>, throwing <see cref="OverflowException"/> if it is negative.</summary>
		public static size_t ToSizeType(this int value)
		{
			return new size_t(checked((uint)value));
		}
	}

	/// <summary>Standard Lua constants with the "LUA_" prefix removed.</summary>
	public static class LUA
	{
		public const int
		REGISTRYINDEX = -10000,
		ENVIRONINDEX  = -10001,
		GLOBALSINDEX  = -10002,

		MULTRET = -1; // option for multiple returns in `lua_pcall' and `lua_call'
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
	public static partial class LuaDLL
	{
		#region DllImport

		// for debugging
		// const string BASEPATH = @"C:\development\software\dotnet\tools\PulseRecognizer\PulseRecognizer\bin\Debug\";
		// const string BASEPATH = @"C:\development\software\ThirdParty\lua\Built\";
		const string LUADLL = "lua51.dll";
		const string LIBDLL = LUADLL; // lua aux lib
		const string STUBDLL = LUADLL;

		const CallingConvention LUACC = CallingConvention.Cdecl;

		#endregion


		#region state manipulation
	} // LuaDLL

	/// <summary>Used to handle Lua panics</summary>
	public delegate int LuaFunctionCallback(IntPtr luaState);

	public static partial class LuaDLL
	{
		/// <summary>[-0, +0, -] Creates a new Lua state. It calls lua_newstate with an allocator based on the standard C realloc function and then sets a panic function (see lua_atpanic) that prints an error message to the standard error output in case of fatal errors.</summary><returns>Returns the new state, or NULL if there is a memory allocation error.</returns>
		[DllImport(LIBDLL,CallingConvention=LUACC)] public static extern IntPtr luaL_newstate();
		/// <summary>[-∞, +0, -] Destroys all objects in the given Lua state (calling the corresponding garbage-collection metamethods, if any) and frees all dynamic memory used by this state.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void   lua_close(IntPtr luaState);
		/// <summary>[-0, +0, -] Sets a new panic function and returns the old one. If an error happens outside any protected environment, Lua calls a panic function and then calls exit(EXIT_FAILURE), thus exiting the host application. Your panic function can avoid this exit by never returning (e.g., throwing an exception). The panic function can access the error message at the top of the stack.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void lua_atpanic(IntPtr luaState, LuaFunctionCallback panicf);
		// omitted:
		//   lua_newstate  (use luaL_newstate)
		//   lua_newthread (no threads)

		#endregion

		#region basic stack manipulation

		/// <summary>[-0, +0, -] Returns the index of the top element in the stack. Because indices start at 1, this result is equal to the number of elements in the stack (and so 0 means an empty stack).</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern int  lua_gettop    (IntPtr luaState);
		/// <summary>[-?, +?, -] Accepts any acceptable index, or 0, and sets the stack top to this index. If the new top is larger than the old one, then the new elements are filled with nil. If index is 0, then all stack elements are removed.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void lua_settop    (IntPtr luaState, int index);
		/// <summary>[-0, +1, -] Pushes a copy of the element at the given valid index onto the stack.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void lua_pushvalue (IntPtr luaState, int index);
		/// <summary>[-1, +0, -] Removes the element at the given valid index, shifting down the elements above this index to fill the gap.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void lua_remove    (IntPtr luaState, int index);
		/// <summary>[-1, +1, -] Moves the top element into the given valid index, shifting up the elements above this index to open space.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void lua_insert    (IntPtr luaState, int index);
		/// <summary>[-1, +0, -] Moves the top element into the given position (and pops it), without shifting any element (therefore replacing the value at the given position).</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void lua_replace   (IntPtr luaState, int index);
		/// <summary>[-0, +0, m] Ensures that there are at least <paramref name="extra"/> free stack slots in the stack. It returns false if it cannot grow the stack to that size. This function never shrinks the stack; if the stack is already larger than the new size, it is left unchanged.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern bool lua_checkstack(IntPtr luaState, int extra);
		// omitted: lua_xmove (no threads)

		/// <summary>[-n, +0, -] Pops n elements from the stack.</summary>
		public static void lua_pop(IntPtr luaState, int n) { lua_settop(luaState, -n - 1); }

		#endregion

		#region access functions (stack -> C)
	} // LuaDLL

	/// <summary>Lua types for the API, returned by lua_type function</summary>
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

	public static partial class LuaDLL
	{
		/// <summary>[-0, +0, -] Returns the type of the value in the given acceptable index, or LUA_TNONE for a non-valid index (that is, an index to an "empty" stack position).</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern LuaType lua_type    (IntPtr luaState, int index);
		/// <summary>[-0, +0, -] Returns the name of the type encoded by the value tp, which must be one the values returned by lua_type.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern string  lua_typename(IntPtr luaState, LuaType tp);
		/// <summary>[-0, +0, -] Returns the name of the type of the value at the given index.</summary>
		public static string luaL_typename(IntPtr luaState, int index) { return lua_typename(luaState, lua_type(luaState, index)); }

		#region lua_is*

		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a number or a string convertible to a number, and false otherwise.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern bool lua_isnumber   (IntPtr luaState, int index);
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a string or a number (which is always convertible to a string), and false otherwise.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern bool lua_isstring   (IntPtr luaState, int index);
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a C function, and false otherwise.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern bool lua_iscfunction(IntPtr luaState, int index);
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a userdata (either full or light), and false otherwise.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern bool lua_isuserdata (IntPtr luaState, int index);
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a function (either C or Lua), and false otherwise.</summary>
		public static bool lua_isfunction     (IntPtr luaState, int index) { return lua_type(luaState,index) == LuaType.Function; }
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a table, and false otherwise.</summary>
		public static bool lua_istable        (IntPtr luaState, int index) { return lua_type(luaState,index) == LuaType.Table; }
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a light userdata, and false otherwise.</summary>
		public static bool lua_islightuserdata(IntPtr luaState, int index) { return lua_type(luaState,index) == LuaType.LightUserdata; }
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is nil, and false otherwise.</summary>
		public static bool lua_isnil          (IntPtr luaState, int index) { return lua_type(luaState,index) == LuaType.Nil; }
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index has type boolean, and false otherwise.</summary>
		public static bool lua_isboolean      (IntPtr luaState, int index) { return lua_type(luaState,index) == LuaType.Boolean; }
		/// <summary>[-0, +0, -] Returns true if the given acceptable index is not valid (that is, it refers to an element outside the current stack), and false otherwise.</summary>
		public static bool lua_isnone         (IntPtr luaState, int index) { return lua_type(luaState,index) == LuaType.None; }
		/// <summary>[-0, +0, -] Returns true if the given acceptable index is not valid (that is, it refers to an element outside the current stack) or if the value at this index is nil, and false otherwise.</summary>
		public static bool lua_isnoneornil    (IntPtr luaState, int index) { return (int)lua_type(luaState,index) <= 0; }

		#endregion

		/// <summary>[-0, +0, -] Returns the "length" of the value at the given acceptable index: for strings, this is the string length; for tables, this is the result of the length operator ('#'); for userdata, this is the size of the block of memory allocated for the userdata; for other values, it is 0.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern size_t lua_objlen  (IntPtr luaState, int index);
		/// <summary>[-0, +0, e] Returns true if the two values in acceptable indices index1 and index2 are equal, following the semantics of the Lua == operator (that is, may call metamethods). Otherwise returns false. Also returns false if any of the indices is non valid.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern bool lua_equal   (IntPtr luaState, int index1, int index2);
		/// <summary>[-0, +0, -] Returns true if the two values in acceptable indices index1 and index2 are primitively equal (that is, without calling metamethods). Otherwise returns false. Also returns false if any of the indices are non valid.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern bool lua_rawequal(IntPtr luaState, int index1, int index2);
		/// <summary>[-0, +0, e] Returns true if the value at acceptable index index1 is smaller than the value at acceptable index index2, following the semantics of the Lua &lt; operator (that is, may call metamethods). Otherwise returns false. Also returns false if any of the indices is non valid.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern bool lua_lessthan(IntPtr luaState, int index1, int index2);

		/// <summary>[-0, +0, -] Converts the Lua value at the given acceptable index to the C type lua_Number. The Lua value must be a number or a string convertible to a number; otherwise, 0 is returned.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern double lua_tonumber   (IntPtr luaState, int index);
		/// <summary>[-0, +0, -] Converts the Lua value at the given acceptable index to a C boolean value (0 or 1). Like all tests in Lua, lua_toboolean returns 1 for any Lua value different from false and nil; otherwise it returns 0. It also returns 0 when called with a non-valid index.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern bool   lua_toboolean  (IntPtr luaState, int index);
		/// <summary>[-0, +0, m] Use lua_tostring instead.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern IntPtr lua_tolstring  (IntPtr luaState, int index, out size_t len);
		/// <summary>[-0, +0, -] Converts a value at the given acceptable index to a C function. That value must be a C function; otherwise, returns NULL.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern IntPtr lua_tocfunction(IntPtr luaState, int index);
		/// <summary>[-0, +0, -] If the value at the given acceptable index is a full userdata, returns its block address. If the value is a light userdata, returns its pointer. Otherwise, returns NULL.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern IntPtr lua_touserdata (IntPtr luaState, int index);
		// omitted:
		//   lua_isthread  (no threads)
		//   lua_tointeger (use System.Convert)
		//   lua_topointer (no C functions/pointers)
		//   lua_tothread  (no threads)

		/// <summary>
		/// [-0, +0, m] Converts the Lua value at the given acceptable index to a C# string.
		/// If the value is a number, then lua_tolstring also changes the actual value in the stack to a string!
		/// (This change confuses lua_next when lua_tolstring is applied to keys during a table traversal.)
		/// </summary>
		/// <returns>The Lua value must be a string or a number; otherwise, the function returns null.</returns>
		public static string lua_tostring(IntPtr luaState, int index)
		{
			size_t strlen;
			IntPtr str = lua_tolstring(luaState, index, out strlen);
			if (str == IntPtr.Zero) return null;
			return Marshal.PtrToStringAnsi(str, strlen.ToInt32());
		}

		#endregion

		#region push functions (C -> stack)

		/// <summary>[-0, +1, -] Pushes a nil value onto the stack.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void lua_pushnil          (IntPtr luaState);
		/// <summary>[-0, +1, -] Pushes a number with value n onto the stack.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void lua_pushnumber       (IntPtr luaState, double n);
		/// <summary>[-0, +1, m] Pushes the string pointed to by s with size len onto the stack. Lua makes (or reuses) an internal copy of the given string, so the memory at s can be freed or reused immediately after the function returns. The string can contain embedded zeros.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void lua_pushlstring      (IntPtr luaState, IntPtr s, size_t len);
		/// <summary>[-0, +1, -] Pushes a boolean value with value b onto the stack.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void lua_pushboolean      (IntPtr luaState, bool b);
		/// <summary>[-0, +1, -] Pushes a light userdata (IntPtr) onto the stack.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void lua_pushlightuserdata(IntPtr luaState, IntPtr p);
		// omitted:
		//   lua_pushvfstring  (use lua_pushstring)
		//   lua_pushfstring   (use lua_pushstring)
		//   lua_pushcclosure  (no C functions/pointers)
		//   lua_pushcfunction (no C functions/pointers)
		//   lua_register      (no C functions/pointers)
		//   lua_pushthread    (no threads)

		// /// <summary>[-0, +1, m] Pushes the zero-terminated string pointed to by s onto the stack.</summary>
		// [DllImport(LUADLL,CallingConvention=LUACC)] public static extern void lua_pushstring       (IntPtr luaState, string s);

		/// <summary>[-0, +1, m] Pushes the string s onto the stack, which can contain embedded zeros.</summary>
		public static void lua_pushstring(IntPtr luaState, string s)
		{
			Debug.Assert(s != null);
			IntPtr ptr = Marshal.StringToHGlobalAnsi(s);
			try { lua_pushlstring(luaState, ptr, s.Length.ToSizeType()); }
			finally { Marshal.FreeHGlobal(ptr); }
		}

		#endregion

		#region get functions (Lua -> stack)

		/// <summary>[-1, +1, e] Pushes onto the stack the value t[k], where t is the value at the given valid index and k is the value at the top of the stack. This function pops the key from the stack (putting the resulting value in its place). As in Lua, this function may trigger a metamethod.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void   lua_gettable    (IntPtr luaState, int index);
		/// <summary>[-0, +1, e] Pushes onto the stack the value t[k], where t is the value at the given valid index. As in Lua, this function may trigger a metamethod.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void   lua_getfield    (IntPtr luaState, int index, string k);
		/// <summary>[-1, +1, -] Similar to lua_gettable (pops the key and pushes the result from the table at <paramref name="index"/>) but does a raw access (i.e., without metamethods). (type MUST be a table)</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void   lua_rawget      (IntPtr luaState, int index);
		/// <summary>[-0, +1, -] Pushes onto the stack the value t[n], where t is the value at the given valid index. It does not invoke metamethods. (type MUST be a table)</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void   lua_rawgeti     (IntPtr luaState, int index, int n);
		/// <summary>[-0, +1, m] Creates a new empty table and pushes it onto the stack. The new table has space pre-allocated for <paramref name="narr"/> array elements and <paramref name="nrec"/> non-array elements.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void   lua_createtable (IntPtr luaState, int narr, int nrec);
		/// <summary>[-0, +1, m] This function allocates a new block of memory with the given size, pushes onto the stack a new full userdata with the block address, and returns this address.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern IntPtr lua_newuserdata (IntPtr luaState, size_t size);
		/// <summary>[-0, +(0|1), -] Pushes onto the stack the metatable of the value at the given acceptable index. If the index is not valid, or if the value does not have a metatable, the function returns 0 and pushes nothing on the stack.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern bool   lua_getmetatable(IntPtr luaState, int index);
		/// <summary>[-0, +1, -] Pushes onto the stack the environment table of the value at the given index.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void  lua_getfenv      (IntPtr luaState, int index);

		/// <summary>[-0, +1, e] Pushes onto the stack the value of the global <paramref name="name"/>.</summary>
		public static void lua_getglobal    (IntPtr luaState, string name)  { lua_getfield(luaState, LUA.GLOBALSINDEX, name); }
		/// <summary>[-0, +1, m] Creates a new empty table and pushes it onto the stack.</summary>
		public static void lua_newtable     (IntPtr luaState)               { lua_createtable(luaState, 0, 0); }
		/// <summary>[-0, +1, -] Pushes onto the stack the metatable associated with name tname in the registry (see <see cref="luaL_newmetatable"/>).</summary>
		public static void luaL_getmetatable(IntPtr luaState, string tname) { lua_getfield(luaState, LUA.REGISTRYINDEX, tname); }
		/// <summary>[-0, +1, m] If the registry already has the key tname, returns false. Otherwise, creates a new table to be used as a metatable for userdata, adds it to the registry with key tname, and returns true. In both cases pushes onto the stack the final value associated with tname in the registry.</summary>
		[DllImport(LIBDLL,CallingConvention=LUACC)] public static extern bool luaL_newmetatable(IntPtr luaState, string tname);
		/// <summary>[-0, +(0|1), m] Pushes onto the stack the field e from the metatable of the object at index obj. If the object does not have a metatable, or if the metatable does not have this field, returns 0 and pushes nothing.</summary>
		[DllImport(LIBDLL,CallingConvention=LUACC)] public static extern bool luaL_getmetafield(IntPtr luaState, int index, string field);

		#endregion

		#region set functions (stack -> Lua)

		/// <summary>[-2, +0, e] Does the equivalent to t[k] = v, where t is the value at the given valid index, v is the value at the top of the stack, and k is the value just below the top. This function pops both the key and the value from the stack. As in Lua, this function may trigger a metamethod.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void lua_settable    (IntPtr luaState, int index);
		/// <summary>[-1, +0, e] Does the equivalent to t[k] = v, where t is the value at the given valid index and v is the value at the top of the stack. This function pops the value from the stack. As in Lua, this function may trigger a metamethod.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void lua_setfield    (IntPtr luaState, int index, string k);
		/// <summary>[-2, +0, m] Similar to lua_settable (pops the value then pops the key and modifies the table at <paramref name="index"/>) but does a raw assignment (i.e., without metamethods). (type MUST be a table)</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void lua_rawset      (IntPtr luaState, int index);
		/// <summary>[-1, +0, m] Does the equivalent of t[n] = v, where t is the value at the given valid index and v is the value at the top of the stack. This function pops the value from the stack. It does not invoke metamethods. (type MUST be a table)</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void lua_rawseti     (IntPtr luaState, int index, int n);
		/// <summary>[-1, +0, -] Pops a table from the stack and sets it as the new metatable for the value at the given acceptable index.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void lua_setmetatable(IntPtr luaState, int index);
		/// <summary>[-1, +0, -] Pops a table from the stack and sets it as the new environment for the value at the given index. If the value at the given index is neither a function nor a thread nor a userdata, lua_setfenv returns 0. Otherwise it returns 1.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern bool lua_setfenv     (IntPtr luaState, int index);

		/// <summary>[-1, +0, e] Pops a value from the stack and sets it as the new value of global <paramref name="name"/>.</summary>
		public static void lua_setglobal(IntPtr luaState, string name) { lua_setfield(luaState, LUA.GLOBALSINDEX, name); }

		#endregion

		#region `load' and `call' functions (load and run Lua code)
	} // LuaDLL

	/// <summary>
	/// Lua status codes returned by functions like <see cref="LuaDLL.lua_call"/> and <see cref="LuaDLL.lua_load"/>.
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

	/// <summary>Structure used by the chunk reader</summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct ReaderInfo
	{
		public String chunkData;
		public bool finished;
	}

	/// <summary>Delegate for chunk readers used with lua_load</summary>
	public delegate string LuaChunkReader(IntPtr luaState,ref ReaderInfo data,ref size_t size);

	public static partial class LuaDLL
	{

		/// <summary>[-(nargs + 1), +nresults, e] Calls a function.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void      lua_call (IntPtr luaState, int nargs, int nresults);
		/// <summary>[-(nargs + 1), +(nresults|1), -] Calls a function in protected mode. If there is any error, lua_pcall catches it, pushes a single value on the stack (the error message), and returns an error code.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern LuaStatus lua_pcall(IntPtr luaState, int nargs, int nresults, int errfunc);
		/// <summary>[-0, +1, -] Loads a Lua chunk. If there are no errors, lua_load pushes the compiled chunk as a Lua function on top of the stack. Otherwise, it pushes an error message.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern LuaStatus lua_load (IntPtr luaState, LuaChunkReader reader, ref ReaderInfo data, string chunkname);
		// omitted:
		//   lua_cpcall (no point in calling C functions)
		//   lua_dump   (would write to unmanaged memory via lua_Writer)

		/// <summary>[-0, +1, m] Loads a buffer as a Lua chunk. This function uses lua_load to load the chunk in the buffer pointed to by buff with size sz. <paramref name="name"/> is the chunk name, used for debug information and error messages.</summary>
		[DllImport(LIBDLL,CallingConvention=LUACC)] public static extern LuaStatus luaL_loadbuffer(IntPtr luaState, IntPtr buff, int sz, string name);
		/// <summary>[-0, +1, m] Loads a string as a Lua chunk. This function uses lua_load to load the chunk in the zero-terminated string s.</summary>
		[DllImport(LIBDLL,CallingConvention=LUACC)] public static extern LuaStatus luaL_loadstring(IntPtr luaState, string s);
		/// <summary>[-0, +1, m] Loads a file as a Lua chunk. This function uses lua_load to load the chunk in the file named filename. If filename is NULL, then it loads from the standard input. The first line in the file is ignored if it starts with a #.</summary>
		[DllImport(LIBDLL,CallingConvention=LUACC)] public static extern LuaStatus luaL_loadfile  (IntPtr luaState, string filename);
		/// <summary>[-0, +(0|1), e] Calls a metamethod. If the object at index <paramref name="obj"/> has a metatable and this metatable has a field e, this function calls this field and passes the object as its only argument. In this case this function returns 1 and pushes onto the stack the value returned by the call. If there is no metatable or no metamethod, this function returns 0 (without pushing any value on the stack).</summary>
		[DllImport(LIBDLL,CallingConvention=LUACC)] public static extern int luaL_callmeta  (IntPtr luaState, int obj, string e);

		/// <summary>[-0, +1, m] Loads a buffer as a Lua chunk. This function uses lua_load to load the chunk in the string s, which can contain embedded zeros. <paramref name="name"/> is the chunk name, used for debug information and error messages.</summary>
		public static LuaStatus luaL_loadbuffer(IntPtr luaState, string s, string name)
		{
			Debug.Assert(s != null);
			IntPtr ptr = Marshal.StringToHGlobalAnsi(s);
			try { return luaL_loadbuffer(luaState, ptr, s.Length, name); }
			finally { Marshal.FreeHGlobal(ptr); }
		}
		/// <summary>[-0, +?, m] Loads and runs the given string, which can contain embedded zeros.</summary>
		public static LuaStatus luaL_dostring(IntPtr luaState, string str)
		{
			var result = luaL_loadstring(luaState, str);
			return result != LuaStatus.Ok ? result : lua_pcall(luaState, 0, LUA.MULTRET, 0);
		}
		/// <summary>[-0, +?, m] Loads and runs the given file.</summary>
		public static LuaStatus luaL_dofile(IntPtr luaState, string filename)
		{
			var result = luaL_loadfile(luaState, filename);
			return result != LuaStatus.Ok ? result : lua_pcall(luaState, 0, LUA.MULTRET, 0);
		}

		/// <summary>[-0, +0, m] Opens all standard Lua libraries into the given state.</summary>
		[DllImport(LIBDLL,CallingConvention=LUACC)] public static extern void luaL_openlibs(IntPtr luaState);
		/*
		[DllImport(LUALIBDLL,CallingConvention=LUACC)] public static extern void luaopen_base   (IntPtr luaState);
		[DllImport(LUALIBDLL,CallingConvention=LUACC)] public static extern void luaopen_io     (IntPtr luaState);
		[DllImport(LUALIBDLL,CallingConvention=LUACC)] public static extern void luaopen_table  (IntPtr luaState);
		[DllImport(LUALIBDLL,CallingConvention=LUACC)] public static extern void luaopen_string (IntPtr luaState);
		[DllImport(LUALIBDLL,CallingConvention=LUACC)] public static extern void luaopen_math   (IntPtr luaState);
		[DllImport(LUALIBDLL,CallingConvention=LUACC)] public static extern void luaopen_debug  (IntPtr luaState);
		[DllImport(LUALIBDLL,CallingConvention=LUACC)] public static extern void luaopen_loadlib(IntPtr luaState);
		*/

		#endregion

		#region coroutine functions

		// omitted:
		//   lua_yield			(no threads)
		//   lua_resume			(no threads)
		//   lua_status			(no threads)

		#endregion

		#region garbage-collection function and options
	} // LuaDLL

	/// <summary>Options for the LuaDLL function <see cref="LuaDLL.lua_gc"/></summary>
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

	public static partial class LuaDLL
	{
		/// <summary>[-0, +0, e] Controls the garbage collector. This function performs several tasks, according to the value of the parameter <paramref name="what"/>.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern int lua_gc(IntPtr luaState, LuaGC what, int data);

		#endregion

		#region miscellaneous functions

		/// <summary>[-1, +0, v] Generates a Lua error. The error message (which can actually be a Lua value of any type) must be on the stack top. This function throws an exception, and therefore never returns.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern int  lua_error (IntPtr luaState);
		/// <summary>[-1, +(2|0), e] Pops a key from the stack, and pushes a key-value pair from the table at the given index (the "next" pair after the given key). If there are no more elements in the table, then lua_next returns 0 (and pushes nothing). While traversing a table, do not call lua_tolstring directly on a key, unless you know that the key is actually a string. Recall that lua_tolstring changes the value at the given index; this confuses the next call to lua_next.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern bool lua_next  (IntPtr luaState, int index);
		/// <summary>[-n, +1, e] Concatenates the n values at the top of the stack, pops them, and leaves the result at the top. If n is 1, the result is the single value on the stack (that is, the function does nothing); if n is 0, the result is the empty string. Concatenation is performed following the usual semantics of Lua.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void lua_concat(IntPtr luaState, int n);
		/// <summary>[-0, +1, m] Pushes onto the stack a string identifying the current position of the control at level lvl in the call stack. Typically this string has the following format: <c>chunkname:currentline:</c> Level 0 is the running function, level 1 is the function that called the running function, etc. This function is used to build a prefix for error messages.</summary>
		[DllImport(LUADLL,CallingConvention=LUACC)] public static extern void luaL_where(IntPtr luaState, int level);
		// omitted:
		//   lua_getallocf (no C functions/pointers)
		//   lua_setallocf (no C functions/pointers)

		/// <summary>[-0, +0, v] Raises an error. The error message format is given by fmt plus any extra arguments, following the same rules of String.Format. It also adds at the beginning of the message the file name and the line number where the error occurred, if this information is available. This function never returns, but it is an idiom to use it in C functions as <c>return luaL_error(args)</c>.</summary>
		public static int luaL_error(IntPtr luaState, string fmt, params object[] args) { return luaL_error(luaState, string.Format(fmt, args)); }
		/// <summary>[-0, +0, v] Raises an error. It adds at the beginning of the message the file name and the line number where the error occurred, if this information is available. This function never returns, but it is an idiom to use it in C functions as <c>return luaL_error(args)</c>.</summary>
		public static int luaL_error(IntPtr luaState, string message)
		{
			// re-implemented to avoid headaches with native variable arguments
			luaL_where(luaState, 1);
			lua_pushstring(luaState, message);
			lua_concat(luaState, 2);
			return lua_error(luaState);
		}

		#endregion

		#region reference system
	} // LuaDLL

	/// <summary>Special references</summary>
	public static class LuaRefs
	{
		public const int
		Min  =  1, // see http://www.lua.org/source/5.1/lauxlib.c.html#luaL_ref, registry[0] is FREELIST_REF
		None = -2, // LUA_NOREF
		Nil  = -1; // LUA_REFNIL
	}

	public static partial class LuaDLL
	{
		/// <summary>[-1, +0, m] Creates and returns a reference, in the table at index t, for the object at the top of the stack (and pops the object).</summary>
		[DllImport(LIBDLL,CallingConvention=LUACC)] public static extern int  luaL_ref  (IntPtr luaState, int t);
		/// <summary>[-0, +0, -] Releases reference ref from the table at index t (see luaL_ref). The entry is removed from the table, so that the referred object can be collected. The reference ref is also freed to be used again.</summary>
		[DllImport(LIBDLL,CallingConvention=LUACC)] public static extern void luaL_unref(IntPtr luaState, int registryIndex, int reference);

		/// <summary>[-1, +0, m] Creates and returns a reference for the object at the top of the stack (and pops the object).</summary>
		public static int  lua_ref   (IntPtr luaState)                { return luaL_ref(luaState,LUA.REGISTRYINDEX); }
		/// <summary>[-0, +0, -] Releases reference ref. The entry is removed from the registry, so that the referred object can be collected. The reference ref is also freed to be used again.</summary>
		public static void lua_unref (IntPtr luaState, int reference) {      luaL_unref(luaState,LUA.REGISTRYINDEX,reference); }
		/// <summary>[-0, +1, -] Pushes the referenced object onto the stack.</summary>
		public static void lua_getref(IntPtr luaState, int reference) {     lua_rawgeti(luaState,LUA.REGISTRYINDEX,reference); }

		#endregion

		#region luanet misc functions

		/// <summary>[-0, +0, -] Checks if the object at <paramref name="index"/> has a metatable containing a field with a light userdata key matching <see cref="luanet_gettag"/>.</summary>
		[DllImport(STUBDLL,CallingConvention=LUACC)] public static extern bool   luaL_checkmetatable(IntPtr luaState, int index);
		/// <summary>[-0, +0, -] The address of a static variable in the luanet DLL. The variable's contents are never used. Rather, the address itself serves as a unique identifier for luanet metatables. (see <see cref="luaL_checkmetatable"/>)</summary>
		[DllImport(STUBDLL,CallingConvention=LUACC)] public static extern IntPtr luanet_gettag();
		/// <summary>[-0, +0, -] Pushes a new luanet userdata object, which stores a single integer, onto the stack. The object does not have a metatable by default.</summary>
		[DllImport(STUBDLL,CallingConvention=LUACC)] public static extern int    luanet_newudata    (IntPtr luaState, int val);
		/// <summary>[-0, +0, -] Retrieves the int stored in a luanet userdata object. Returns -1 if luaL_checkmetatable fails and the object's metatable isn't luaNet_class, luaNet_searchbase, or luaNet_function.</summary>
		[DllImport(STUBDLL,CallingConvention=LUACC)] public static extern int    luanet_tonetobject (IntPtr luaState, int index);
		/// <summary>[-0, +0, -] Like <see cref="luanet_tonetobject"/>, but doesn't perform the safety checks. Only use this if you're completely sure the value is a luanet userdata.</summary>
		[DllImport(STUBDLL,CallingConvention=LUACC)] public static extern int    luanet_rawnetobj   (IntPtr luaState, int index);
		/// <summary>[-0, +0, -] Checks if the specified userdata object uses the specified metatable. If so, it does the same thing as <see cref="luanet_rawnetobj"/>. Otherwise, returns -1.</summary>
		[DllImport(STUBDLL,CallingConvention=LUACC)] public static extern int    luanet_checkudata  (IntPtr luaState, int index, string meta);

		#endregion

		#region LuaCSFunction
	} // LuaDLL

	/// <summary>Delegate for functions passed to Lua as function pointers</summary>
	public delegate int LuaCSFunction(IntPtr luaState);

	public static partial class LuaDLL
	{
		/// <summary>[-0, +1, m] Pushes a delegate onto the stack as a C function. http://www.lua.org/manual/5.1/manual.html#lua_CFunction </summary>
		[DllImport(STUBDLL,CallingConvention=LUACC)] public static extern void lua_pushstdcallcfunction(IntPtr luaState, [MarshalAs(UnmanagedType.FunctionPtr)]LuaCSFunction function);

		/*
			Several functions in the auxiliary library are used to check C function arguments. Their names are
			always luaL_check* or luaL_opt*. All of these functions throw an error if the check is not satisfied.
			Because the error message is formatted for arguments (e.g., "bad argument #1"), you should not use
			these functions for other stack values.
		*/

		/// <summary>[-0, +0, v] Checks whether the function argument narg is a userdata of the type tname (see luaL_newmetatable).</summary>
		[DllImport(LIBDLL,CallingConvention=LUACC)] public static extern IntPtr luaL_checkudata(IntPtr luaState, int narg, string meta);
		// omitted:
		//   luaL_checkany (function argument checking unnecessary)
		//   luaL_checkint
		//   luaL_checkinteger
		//   luaL_checklong
		//   luaL_checklstring
		//   luaL_checknumber
		//   luaL_checkoption
		//   luaL_checkstring
		//   luaL_checktype

		#endregion


		// omitted:
		//   luaL_add*       (use System.String concatenation or similar)
		//   luaL_argcheck   (function argument checking unnecessary)
		//   luaL_argerror   (function argument checking unnecessary)
		//   luaL_buffinit   (use System.String concatenation or similar)
		//   luaL_prepbuffer (use System.String concatenation or similar)
		//   luaL_pushresult (use System.String concatenation or similar)
		//   luaL_gsub       (use System.String concatenation or similar)
		//   luaL_register   (all libs already opened, use require in scripts for external libs)
		//   luaL_typerror   (function argument checking unnecessary)
		//
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