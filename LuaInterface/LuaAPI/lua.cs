using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LuaInterface.LuaAPI
{
	using size_t = UIntPtr;
	using char8 = Byte;
	using Dbg = Debug;

	/// <summary>Standard Lua constants with the "LUA_" prefix removed.</summary>
	public static partial class LUA
	{
		public const int
		REGISTRYINDEX = -10000,
		ENVIRONINDEX  = -10001,
		GLOBALSINDEX  = -10002,

		MULTRET = -1, // option for multiple returns in `lua_pcall' and `lua_call'
		IDSIZE = 60, // gives the maximum size for the description of the source of a function in debug information.

		MINSTACK = 20,

		VERSION_NUM = 501;

		public const string
		VERSION = "Lua 5.1",
		RELEASE = "Lua 5.1.5",
		COPYRIGHT = "Copyright (C) 1994-2012 Lua.org, PUC-Rio",
		AUTHORS = "R. Ierusalimschy, L. H. de Figueiredo & W. Celes";

		public const string SIGNATURE = "\x1BLua"; // mark for precompiled code (`<esc>Lua')
		/// <summary>the only time LUA_SIGNATURE is used in Lua 5.1 is when it outputs new bytecode. all the parsing code just uses LUA_SIGNATURE[0]</summary>
		public const char SIGNATURE_0 = '\x1B';
	}
	public static partial class lua
	{
		/// <summary>
		/// <para>[-0, +0, -] When a C function is created, it is possible to associate some values with it, thus creating a C closure; these values are called upvalues and are accessible to the function whenever it is called (see <see cref="lua.pushcclosure"/>).</para>
		/// <para>Whenever a C function is called, its upvalues are located at specific pseudo-indices. These pseudo-indices are produced by <see cref="lua.upvalueindex"/>. The first value associated with a function is at position lua.upvalueindex(1), and so on. Any access to lua.upvalueindex(n), where n is greater than the number of upvalues of the current function (but not greater than 256), produces an acceptable (but invalid) index.</para>
		/// </summary>
		[MethodImpl(INLINE)] public static int upvalueindex(int i) { return LUA.GLOBALSINDEX - i; }
	}

	public static partial class LUA
	{
		/// <summary>Lua types for the API, returned by the <see cref="lua.type"/> function</summary>
		public enum T
		{
			/// <summary>The type returned when accessing an index outside the stack boundaries.</summary>
			NONE          = -1,
			NIL           = 0,
			BOOLEAN       = 1,
			LIGHTUSERDATA = 2,
			NUMBER        = 3,
			STRING        = 4,
			TABLE         = 5,
			FUNCTION      = 6,
			USERDATA      = 7,
			THREAD        = 8,
		}
	}


	/// <summary>
	/// <para>P/Invoke wrapper of the Lua API.</para>
	/// <para>Each function's summary starts with an indicator like this: [-o, +p, x]</para>
	/// <para>The first field, o, is how many elements the function pops from the stack.</para>
	/// <para>The second field, p, is how many elements the function pushes onto the stack. (Any function always pushes its results after popping its arguments.)</para>
	/// <para>A field in the form x|y means the function can push (or pop) x or y elements, depending on the situation;</para>
	/// <para>an interrogation mark '?' means that we cannot know how many elements the function pops/pushes by looking only at its arguments (e.g., they may depend on what is on the stack).</para>
	/// <para>The third field, x, tells whether the function may throw errors:</para>
	/// <para>'-' means the function never throws any error;</para>
	/// <para>'m' means the function may throw an error only due to not enough memory;</para>
	/// <para>'e' means the function may throw other kinds of errors;</para>
	/// <para>'v' means the function may throw an error on purpose.</para>
	/// </summary>
	/// <remarks>
	/// <para>The summaries are mostly just abridged copy pastes of the Lua manual, with a focus on
	/// providing enough information to be useful in understanding and auditing existing code.
	/// You should consult the actual manual at http://www.lua.org/manual/5.1/
	/// whenever the documentation is insufficient.</para>
	/// <para>Confusingly, most parts of the manual say that the error system uses c-style "long jumps".
	/// LuaInterface's custom Lua build uses a C++ compiler, which automatically switches on
	/// C++ style Lua exceptions. The abridged documentation corrects those statements.</para>
	/// </remarks>
	public static unsafe partial class lua
	{
		const string DLL = "lua51.dll";
		const CallingConvention CC = CallingConvention.Cdecl;
		/// <summary>.NET 4.5 AggressiveInlining. Should be auto discarded on older build targets or otherwise ignored.</summary>
		const MethodImplOptions INLINE = (MethodImplOptions) 0x0100;


		/// <summary>Delegate for methods passed to Lua as function pointers.</summary>
		[UnmanagedFunctionPointer(CC)] public delegate int CFunction(lua.State L);


		#region state manipulation

		/// <summary>[-0, +1, m] <para>Creates a new thread, pushes it on the stack, and returns a pointer to a lua_State that represents this new thread. The new state returned by this function shares with the original state all global objects (such as tables), but has an independent execution stack.</para><para>There is no explicit function to close or to destroy a thread. Threads are subject to garbage collection, like any Lua object.</para></summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_newthread")] public static extern lua.State newthread(lua.State L);
		/// <summary>[-∞, +0, -] Destroys all objects in the given Lua state (calling the corresponding garbage-collection metamethods, if any) and frees all dynamic memory used by this state.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_close"    )] public static extern void close         (lua.State L);
		/// <summary>[-0, +0, -] Sets a new panic function and returns the old one. If an error happens outside any protected environment, Lua calls a panic function and then calls exit(EXIT_FAILURE), thus exiting the host application. Your panic function can avoid this exit by never returning (e.g., throwing an exception). The panic function can access the error message at the top of the stack.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_atpanic"  )] public static extern void atpanic       (lua.State L, lua.CFunction panicf);
		// omitted:
		//   lua_newstate  (use luaL.newstate)

		#endregion

		#region basic stack manipulation

		/// <summary>[-0, +0, -] Returns the index of the top element in the stack. Because indices start at 1, this result is equal to the number of elements in the stack (and so 0 means an empty stack).</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_gettop"    )] public static extern int  gettop    (lua.State L);
		/// <summary>[-?, +?, -] Accepts any acceptable index, or 0, and sets the stack top to this index. If the new top is larger than the old one, then the new elements are filled with nil. If index is 0, then all stack elements are removed.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_settop"    )] public static extern void settop    (lua.State L, int index);
		/// <summary>[-0, +1, -] Pushes a copy of the element at the given valid index onto the stack.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_pushvalue" )] public static extern void pushvalue (lua.State L, int index);
		/// <summary>[-1, +0, -] Removes the element at the given valid index, shifting down the elements above this index to fill the gap.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_remove"    )] public static extern void remove    (lua.State L, int index);
		/// <summary>[-1, +1, -] Moves the top element into the given valid index, shifting up the elements above this index to open space.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_insert"    )] public static extern void insert    (lua.State L, int index);
		/// <summary>[-1, +0, -] Moves the top element into the given position (and pops it), without shifting any element (therefore replacing the value at the given position).</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_replace"   )] public static extern void replace   (lua.State L, int index);
		/// <summary>[-0, +0, m] Ensures that there are at least <paramref name="extra"/> free stack slots in the stack. It returns false if it cannot grow the stack to that size. This function never shrinks the stack; if the stack is already larger than the new size, it is left unchanged.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_checkstack")] public static extern bool checkstack(lua.State L, int extra);
		/// <summary>[-?, +?, -] <para>Exchange values between different threads of the same global state.</para><para>This function pops <paramref name="n"/> values from the stack <paramref name="from"/>, and pushes them onto the stack <paramref name="to"/>.</para></summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_xmove"     )] public static extern void xmove     (lua.State from, lua.State to, int n);

		/// <summary>[-n, +0, -] Pops n elements from the stack.</summary>
		[MethodImpl(INLINE)] public static void pop(lua.State L, int n) { lua.settop(L, -n - 1); }

		#endregion

		#region access functions (stack -> C)

		/// <summary>[-0, +0, -] Returns the type of the value in the given acceptable index, or <see cref="LUA.T.NONE"/> for a non-valid index (that is, an index to an "empty" stack position).</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_type")] public static extern LUA.T type(lua.State L, int index);
		/// <summary>[-0, +0, -] Returns the name of the type encoded by the value tp, which must be one the values returned by <see cref="lua.type"/>.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_typename")] public static extern string typename(lua.State L, LUA.T tp);

		#region lua.is*

		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a number or a string convertible to a number, and false otherwise.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_isnumber"   )] public static extern bool isnumber   (lua.State L, int index);
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a string or a number (which is always convertible to a string), and false otherwise.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_isstring"   )] public static extern bool isstring   (lua.State L, int index);
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a C function, and false otherwise.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_iscfunction")] public static extern bool iscfunction(lua.State L, int index);
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a userdata (either full or light), and false otherwise.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_isuserdata" )] public static extern bool isuserdata (lua.State L, int index);
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a function (either C or Lua), and false otherwise.</summary>
		public static bool isfunction     (lua.State L, int index) { return lua.type(L,index) == LUA.T.FUNCTION; }
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a table, and false otherwise.</summary>
		public static bool istable        (lua.State L, int index) { return lua.type(L,index) == LUA.T.TABLE; }
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a light userdata, and false otherwise.</summary>
		public static bool islightuserdata(lua.State L, int index) { return lua.type(L,index) == LUA.T.LIGHTUSERDATA; }
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is nil, and false otherwise.</summary>
		public static bool isnil          (lua.State L, int index) { return lua.type(L,index) == LUA.T.NIL; }
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index has type boolean, and false otherwise.</summary>
		public static bool isboolean      (lua.State L, int index) { return lua.type(L,index) == LUA.T.BOOLEAN; }
		/// <summary>[-0, +0, -] Returns true if the given acceptable index is not valid (that is, it refers to an element outside the current stack), and false otherwise.</summary>
		public static bool isnone         (lua.State L, int index) { return lua.type(L,index) == LUA.T.NONE; }
		/// <summary>[-0, +0, -] Returns true if the given acceptable index is not valid (that is, it refers to an element outside the current stack) or if the value at this index is nil, and false otherwise.</summary>
		public static bool isnoneornil    (lua.State L, int index) { return (int)lua.type(L,index) <= 0; }
		/// <summary>[-0, +0, -] Returns true if the value at the given acceptable index is a thread, and false otherwise.</summary>
		public static bool isthread       (lua.State L, int index) { return lua.type(L,index) == LUA.T.THREAD; }

		#endregion

		/// <summary>[-0, +0, -] Returns the "length" of the value at the given acceptable index: for strings, this is the string length; for tables, this is the result of the length operator ('#'); for userdata, this is the size of the block of memory allocated for the userdata; for other values, it is 0.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_objlen"  )] public static extern size_t objlen  (lua.State L, int index);
		/// <summary>[-0, +0, e] Returns true if the two values in acceptable indices index1 and index2 are equal, following the semantics of the Lua == operator (that is, may call metamethods). Otherwise returns false. Also returns false if any of the indices is non valid.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_equal"   )] public static extern bool   equal   (lua.State L, int index1, int index2);
		/// <summary>[-0, +0, -] Returns true if the two values in acceptable indices index1 and index2 are primitively equal (that is, without calling metamethods). Otherwise returns false. Also returns false if any of the indices are non valid.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_rawequal")] public static extern bool   rawequal(lua.State L, int index1, int index2);
		/// <summary>[-0, +0, e] Returns true if the value at acceptable index index1 is smaller than the value at acceptable index index2, following the semantics of the Lua &lt; operator (that is, may call metamethods). Otherwise returns false. Also returns false if any of the indices is non valid.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_lessthan")] public static extern bool   lessthan(lua.State L, int index1, int index2);

		/// <summary>[-0, +0, -] Converts the Lua value at the given acceptable index to the C type lua_Number. The Lua value must be a number or a string convertible to a number; otherwise, 0 is returned.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_tonumber"   )] public static extern double    tonumber   (lua.State L, int index);
		/// <summary>[-0, +0, -] Converts the Lua value at the given acceptable index to a boolean value. Like all tests in Lua, <see cref="lua.toboolean"/> returns true for any Lua value different from false and nil; otherwise it returns false. It also returns false when called with a non-valid index.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_toboolean"  )] public static extern bool      toboolean  (lua.State L, int index);
		/// <summary>[-0, +0, m] Use <see cref="lua.tostring"/> instead.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_tolstring"  )] public static extern void*     tolstring  (lua.State L, int index, out size_t len);
		/// <summary>[-0, +0, -] Converts a value at the given acceptable index to a C function. That value must be a C function; otherwise, returns <see langword="null"/>.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_tocfunction")] public static extern void*     tocfunction(lua.State L, int index);
		/// <summary>[-0, +0, -] If the value at the given acceptable index is a full userdata, returns its block address. If the value is a light userdata, returns its pointer. Otherwise, returns <see langword="null"/>.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_touserdata" )] public static extern void*     touserdata (lua.State L, int index);
		/// <summary>[-0, +0, -] Converts the value at the given acceptable index to a generic C pointer (void*). The value can be a userdata, a table, a thread, or a function; otherwise, <see cref="lua.topointer"/> returns <see langword="null"/>. Typically this function is used only for debug information.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_topointer"  )] public static extern void*     topointer  (lua.State L, int index);
		/// <summary>[-0, +0, -] Converts the value at the given acceptable index to a Lua thread (represented as lua_State*). This value must be a thread; otherwise, the function returns NULL.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_tothread"   )] public static extern lua.State tothread   (lua.State L, int index);
		// omitted:
		//   lua_tointeger (use System.Convert)

		/// <summary>
		/// [-0, +0, m] Converts the Lua value at the given acceptable index to a C# string.
		/// If the value is a number, then <see cref="lua.tostring"/> also changes the actual value in the stack to a string!
		/// (This change confuses <see cref="lua.next"/> when <see cref="lua.tostring"/> is applied to keys during a table traversal.)
		/// </summary>
		/// <returns>The Lua value must be a string or a number; otherwise, the function returns <see langword="null"/>.</returns>
		public static string tostring(lua.State L, int index)
		{
			size_t strlen;
			var str = lua.tolstring(L, index, out strlen);
			if (str == null) return null;
			return Marshal.PtrToStringAnsi((IntPtr)str, strlen.ToInt32());
		}

		#endregion

		#region push functions (C -> stack)

		/// <summary>[-0, +1, -] Pushes a nil value onto the stack.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_pushnil"          )] public static extern void pushnil          (lua.State L);
		/// <summary>[-0, +1, -] Pushes a number with value n onto the stack.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_pushnumber"       )] public static extern void pushnumber       (lua.State L, double n);
		/// <summary>[-0, +1, m] Pushes the string pointed to by s with size len onto the stack. Lua makes (or reuses) an internal copy of the given string, so the memory at s can be freed or reused immediately after the function returns. The string can contain embedded zeros.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_pushlstring"      )] public static extern void pushlstring      (lua.State L, IntPtr s, size_t len);
		/// <summary>[-n, +1, m] Pushes a new C closure onto the stack.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_pushcclosure"     )] public static extern void pushcclosure     (lua.State L, lua.CFunction fn, int n);
		/// <summary>[-0, +1, -] Pushes a boolean value with value b onto the stack.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_pushboolean"      )] public static extern void pushboolean      (lua.State L, bool b);
		/// <summary>[-0, +1, -] Pushes a light userdata (IntPtr) onto the stack.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_pushlightuserdata")] public static extern void pushlightuserdata(lua.State L, IntPtr p);
		/// <summary>[-0, +1, -] Pushes a light userdata (IntPtr) onto the stack.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_pushthread"       )] public static extern bool pushthread       (lua.State L);
		/// <summary>[-0, +1, m] Pushes a C function onto the stack. This function receives a pointer to a C function and pushes onto the stack a Lua value of type function that, when called, invokes the corresponding C function.</summary>
		[MethodImpl(INLINE)] public static void pushcfunction(lua.State L, lua.CFunction f) { lua.pushcclosure(L, f, 0); }
		/// <summary>[-0, +0, e] Sets the C function f as the new value of global name.</summary>
		[MethodImpl(INLINE)] public static void register(lua.State L, string name, lua.CFunction f) { lua.pushcclosure(L, f, 0); lua.setfield(L, LUA.GLOBALSINDEX, name); }
		// omitted:
		//   lua_pushvfstring  (use lua.pushstring)
		//   lua_pushfstring   (use lua.pushstring)

		// /// <summary>[-0, +1, m] Pushes the zero-terminated string s onto the stack.</summary>
		// [DllImport(LUADLL,CallingConvention=LUACC,EntryPoint="lua_pushstring")] public static extern void pushstring(lua.State L, string s);

		/// <summary>[-0, +1, m] Pushes the string s onto the stack, which can contain embedded zeros.</summary>
		public static void pushstring(lua.State L, string s)
		{
			Dbg.Assert(s != null);
			IntPtr ptr = Marshal.StringToHGlobalAnsi(s);
			try { lua.pushlstring(L, ptr, s.Length.ToSizeType()); }
			finally { Marshal.FreeHGlobal(ptr); }
		}

		#endregion

		#region get functions (Lua -> stack)

		/// <summary>[-1, +1, e] Pushes onto the stack the value t[k], where t is the value at the given valid index and k is the value at the top of the stack. This function pops the key from the stack (putting the resulting value in its place). As in Lua, this function may trigger a metamethod.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_gettable"    )] public static extern void   gettable    (lua.State L, int index);
		/// <summary>[-0, +1, e] Pushes onto the stack the value t[k], where t is the value at the given valid index. As in Lua, this function may trigger a metamethod.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_getfield"    )] public static extern void   getfield    (lua.State L, int index, string k);
		/// <summary>[-1, +1, -] Similar to <see cref="lua.gettable"/> (pops the key and pushes the result from the table at <paramref name="index"/>) but does a raw access (i.e., without metamethods). (type MUST be a table)</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_rawget"      )] public static extern void   rawget      (lua.State L, int index);
		/// <summary>[-0, +1, -] Pushes onto the stack the value t[n], where t is the value at the given valid index. It does not invoke metamethods. (type MUST be a table)</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_rawgeti"     )] public static extern void   rawgeti     (lua.State L, int index, int n);
		/// <summary>[-0, +1, m] Creates a new empty table and pushes it onto the stack. The new table has space pre-allocated for <paramref name="narr"/> array elements and <paramref name="nrec"/> non-array elements.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_createtable" )] public static extern void   createtable (lua.State L, int narr, int nrec);
		/// <summary>[-0, +1, m] This function allocates a new block of memory with the given size, pushes onto the stack a new full userdata with the block address, and returns this address.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_newuserdata" )] public static extern IntPtr newuserdata (lua.State L, size_t size);
		/// <summary>[-0, +(0|1), -] Pushes onto the stack the metatable of the value at the given acceptable index. If the index is not valid, or if the value does not have a metatable, the function returns false and pushes nothing on the stack.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_getmetatable")] public static extern bool   getmetatable(lua.State L, int index);
		/// <summary>[-0, +1, -] Pushes onto the stack the environment table of the value at the given index.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_getfenv"     )] public static extern void   getfenv     (lua.State L, int index);

		/// <summary>[-0, +1, e] Pushes onto the stack the value of the global <paramref name="name"/>.</summary>
		[MethodImpl(INLINE)] public static void getglobal (lua.State L, string name) { lua.getfield(L, LUA.GLOBALSINDEX, name); }
		/// <summary>[-0, +1, m] Creates a new empty table and pushes it onto the stack.</summary>
		[MethodImpl(INLINE)] public static void newtable  (lua.State L) { lua.createtable(L, 0, 0); }

		#endregion

		#region set functions (stack -> Lua)

		/// <summary>[-2, +0, e] Does the equivalent to t[k] = v, where t is the value at the given valid index, v is the value at the top of the stack, and k is the value just below the top. This function pops both the key and the value from the stack. As in Lua, this function may trigger a metamethod.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_settable"    )] public static extern void settable    (lua.State L, int index);
		/// <summary>[-1, +0, e] Does the equivalent to t[k] = v, where t is the value at the given valid index and v is the value at the top of the stack. This function pops the value from the stack. As in Lua, this function may trigger a metamethod.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_setfield"    )] public static extern void setfield    (lua.State L, int index, string k);
		/// <summary>[-2, +0, m] Similar to <see cref="lua.settable"/> (pops the value then pops the key and modifies the table at <paramref name="index"/>) but does a raw assignment (i.e., without metamethods). (type MUST be a table)</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_rawset"      )] public static extern void rawset      (lua.State L, int index);
		/// <summary>[-1, +0, m] Does the equivalent of t[n] = v, where t is the value at the given valid index and v is the value at the top of the stack. This function pops the value from the stack. It does not invoke metamethods. (type MUST be a table)</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_rawseti"     )] public static extern void rawseti     (lua.State L, int index, int n);
		/// <summary>[-1, +0, -] Pops a table from the stack and sets it as the new metatable for the value at the given acceptable index.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_setmetatable")] public static extern void setmetatable(lua.State L, int index);
		/// <summary>[-1, +0, -] Pops a table from the stack and sets it as the new environment for the value at the given index. If the value at the given index is neither a function nor a thread nor a userdata, <see cref="lua.setfenv"/> returns false. Otherwise it returns true.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_setfenv"     )] public static extern bool setfenv     (lua.State L, int index);

		/// <summary>[-1, +0, e] Pops a value from the stack and sets it as the new value of global <paramref name="name"/>.</summary>
		[MethodImpl(INLINE)] public static void setglobal(lua.State L, string name) { lua.setfield(L, LUA.GLOBALSINDEX, name); }

		#endregion

		#region `load' and `call' functions (load and run Lua code)
	} // lua

	public static partial class LUA
	{
		/// <summary>
		/// Lua status codes returned by functions like <see cref="lua.call"/> and <see cref="lua.load"/>.
		/// Each of the functions can only return a subset of these values. See the function's documentation in the Lua manual for details.
		/// </summary>
		public enum ERR
		{
			/// <summary>0: no errors.</summary>
			Success = 0,
			/// <summary>LUA_YIELD: the thread is suspended</summary>
			YIELD = 1,
			/// <summary>LUA_ERRRUN: a runtime error.</summary>
			RUN = 2,
			/// <summary>LUA_ERRSYNTAX: syntax error during pre-compilation.</summary>
			SYNTAX = 3,
			/// <summary>LUA_ERRMEM: memory allocation error. For such errors, Lua does not call the error handler function.</summary>
			MEM = 4,
			/// <summary>LUA_ERRERR: error while running the error handler function.</summary>
			ERR = 5,
			/// <summary>LUA_ERRFILE: cannot open/read the file. (luaL)</summary>
			ERRFILE = LUA.ERR.ERR + 1,
		}
	}

	public static unsafe partial class lua
	{
		/// <summary>The reader function used by <see cref="lua.load"/>. Every time it needs another piece of the chunk, <see cref="lua.load"/> calls the reader, passing along its data parameter. The reader must return a pointer to a block of memory with a new piece of the chunk and set size to the block size. The block must exist until the reader function is called again. To signal the end of the chunk, the reader must return <see langword="null"/> or set size to zero. The reader function may return pieces of any size greater than zero.</summary>
		[UnmanagedFunctionPointer(CC)] public delegate char8* Reader(lua.State L, IntPtr data, out size_t size);

		/// <summary><para>The type of the writer function used by <see cref="dump"/>. Every time it produces another piece of chunk, lua.dump calls the writer, passing along the buffer to be written (<paramref name="p"/>), its size (<paramref name="sz"/>), and the data parameter supplied to lua.dump (<paramref name="ud"/>).</para><para>The writer returns an error code: 0 means no errors; any other value means an error and stops lua.dump from calling the writer again.</para></summary>
		[UnmanagedFunctionPointer(CC)] public delegate int Writer(lua.State L, void* p, size_t sz, IntPtr ud);

		/// <summary>[-(nargs + 1), +nresults, e] Calls a function.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_call"  )] public static extern void    call  (lua.State L, int nargs, int nresults);
		/// <summary>[-(nargs + 1), +(nresults|1), -] Calls a function in protected mode. If there is any error, <see cref="lua.pcall"/> catches it, pushes a single value on the stack (the error message), and returns an error code.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_pcall" )] public static extern LUA.ERR pcall (lua.State L, int nargs, int nresults, int errfunc);
		/// <summary>[-0, +(0|1), -] Calls the C function <paramref name="func"/> in protected mode. <paramref name="func"/> starts with only one element in its stack, a light userdata containing ud. In case of errors, <see cref="lua.cpcall(lua.State,lua.CFunction,IntPtr)"/> returns the same error codes as <see cref="lua.pcall"/>, plus the error object on the top of the stack; otherwise, it returns <see cref="LUA.ERR.Success"/>, and does not change the stack. All values returned by <paramref name="func"/> are discarded.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_cpcall")] public static extern LUA.ERR cpcall(lua.State L, lua.CFunction func, IntPtr ud);
		/// <summary>[-0, +(0|1), -] Calls the C function <paramref name="func"/> in protected mode. <paramref name="func"/> starts with only one element in its stack, a light userdata containing ud. In case of errors, <see cref="M:lua.cpcall(lua.State,void*,void*)"/> returns the same error codes as <see cref="lua.pcall"/>, plus the error object on the top of the stack; otherwise, it returns <see cref="LUA.ERR.Success"/>, and does not change the stack. All values returned by <paramref name="func"/> are discarded.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_cpcall")] public static extern LUA.ERR cpcall(lua.State L, void* func, void* ud);
		/// <summary>[-0, +1, -] Loads a Lua chunk. If there are no errors, <see cref="lua.load"/> pushes the compiled chunk as a Lua function on top of the stack. Otherwise, it pushes an error message.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_load"  )] public static extern LUA.ERR load  (lua.State L, lua.Reader reader, IntPtr data, string chunkname);
		/// <summary>[-0, +0, m] <para>Dumps a function as a binary chunk. Receives a Lua function on the top of the stack and produces a binary chunk that, if loaded again, results in a function equivalent to the one dumped. As it produces parts of the chunk, lua.dump calls function writer (see <see cref="Writer"/>) with the given data to write them.</para><para>The value returned is the error code returned by the last call to the writer; 0 means no errors.</para><para>This function does not pop the Lua function from the stack.</para></summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_dump"  )] public static extern int     dump  (lua.State L, lua.Writer writer, IntPtr data);

		#endregion

		#region coroutine functions

		/// <summary>[-?, +?, -] <para>Yields a coroutine.</para><para>This function should only be called as the return expression of a C function, as follows:</para><code>return lua.yield (L, nresults);</code><para>When a C function calls lua.yield in that way, the running coroutine suspends its execution, and the call to lua.resume that started this coroutine returns. The parameter <paramref name="nresults"/> is the number of values from the stack that are passed as results to lua.resume.</para></summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_yield" )] public static extern int yield(lua.State L, int nresults);
		/// <summary>[-?, +?, -] <para>Starts and resumes a coroutine in a given thread.</para><para>To start a coroutine, you first create a new thread (see <see cref="lua.newthread"/>); then you push onto its stack the main function plus any arguments; then you call lua.resume, with <paramref name="narg"/> being the number of arguments. This call returns when the coroutine suspends or finishes its execution. When it returns, the stack contains all values passed to <see cref="lua.yield"/>, or all values returned by the body function. lua.resume returns <see cref="LUA.ERR.YIELD"/> if the coroutine yields, 0 if the coroutine finishes its execution without errors, or an error code in case of errors (see <see cref="lua.pcall"/>). In case of errors, the stack is not unwound, so you can use the debug API over it. The error message is on the top of the stack. To restart a coroutine, you put on its stack only the values to be passed as results from yield, and then call lua.resume.</para></summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_resume")] public static extern LUA.ERR resume(lua.State L, int narg);
		/// <summary>[-0, +0, -] <para>Returns the status of the thread <paramref name="L"/>.</para><para>The status can be 0 for a normal thread, an error code if the thread finished its execution with an error, or <see cref="LUA.ERR.YIELD"/> if the thread is suspended.</para></summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_status")] public static extern LUA.ERR status(lua.State L);

		#endregion

		#region garbage-collection function and options
	} // lua

	public static partial class LUA
	{
		/// <summary>Options for the Lua API function <see cref="lua.gc"/></summary>
		public enum GC
		{
			/// <summary>Stops the garbage collector.</summary>
			STOP       = 0,
			/// <summary>Restarts the garbage collector.</summary>
			RESTART    = 1,
			/// <summary>Performs a full garbage-collection cycle.</summary>
			COLLECT    = 2,
			/// <summary>Returns the current amount of memory (in Kbytes) in use by Lua.</summary>
			COUNT      = 3,
			/// <summary>Returns the remainder of dividing the current amount of bytes of memory in use by Lua by 1024.</summary>
			COUNTB     = 4,
			/// <summary>Performs an incremental step of garbage collection. The step "size" is controlled by data (larger values mean more steps) in a non-specified way. If you want to control the step size you must experimentally tune the value of data. The function returns 1 if the step finished a garbage-collection cycle.</summary>
			STEP       = 5,
			/// <summary>Sets data as the new value for the pause of the collector (see §2.10). The function returns the previous value of the pause.</summary>
			SETPAUSE   = 6,
			/// <summary>Sets data as the new value for the step multiplier of the collector (see §2.10). The function returns the previous value of the step multiplier.</summary>
			SETSTEPMUL = 7,
		}
	}

	public static unsafe partial class lua
	{
		/// <summary>[-0, +0, e] Controls the garbage collector. This function performs several tasks, according to the value of the parameter <paramref name="what"/>.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_gc")] public static extern int gc(lua.State L, LUA.GC what, int data);

		#endregion

		#region miscellaneous functions

		/// <summary>[-1, +0, v] Generates a Lua error. The error message (which can actually be a Lua value of any type) must be on the stack top. This function throws an exception, and therefore never returns.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_error" )] public static extern int  error (lua.State L);
		/// <summary>[-1, +(2|0), e] Pops a key from the stack, and pushes a key-value pair from the table at the given index (the "next" pair after the given key). If there are no more elements in the table, then <see cref="lua.next"/> returns false (and pushes nothing). While traversing a table, do not call <see cref="lua.tostring"/> directly on a key, unless you know that the key is actually a string. Recall that <see cref="lua.tostring"/> changes the value at the given index; this confuses the next call to <see cref="lua.next"/>.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_next"  )] public static extern bool next  (lua.State L, int index);
		/// <summary>[-n, +1, e] Concatenates the n values at the top of the stack, pops them, and leaves the result at the top. If n is 1, the result is the single value on the stack (that is, the function does nothing); if n is 0, the result is the empty string. Concatenation is performed following the usual semantics of Lua.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_concat")] public static extern void concat(lua.State L, int n);

		#endregion

		#region Debug API
	}
	public static partial class LUA
	{
		public enum HOOK : int
		{
			/// <summary>The call hook is called when the interpreter calls a function. The hook is called just after Lua enters the new function, before the function gets its arguments.</summary>
			CALL    = 0,
			/// <summary>The return hook is called when the interpreter returns from a function. The hook is called just before Lua leaves the function. You have no access to the values to be returned by the function.</summary>
			RET     = 1,
			/// <summary>The line hook is called when the interpreter is about to start the execution of a new line of code, or when it jumps back in the code (even to the same line). (This event only happens while Lua is executing a Lua function.)</summary>
			LINE    = 2,
			/// <summary>The count hook is called after the interpreter executes every count instructions. (This event only happens while Lua is executing a Lua function.)</summary>
			COUNT   = 3,
			/// <summary>Lua is simulating a return from a function that did a tail call; in this case, it is useless to call <see cref="lua.getinfo"/>.</summary>
			TAILRET = 4,
		}
		[Flags] public enum MASK : int
		{
			/// <summary>The call hook is called when the interpreter calls a function. The hook is called just after Lua enters the new function, before the function gets its arguments.</summary>
			CALL  = 1 << LUA.HOOK.CALL,
			/// <summary>The return hook is called when the interpreter returns from a function. The hook is called just before Lua leaves the function. You have no access to the values to be returned by the function.</summary>
			RET   = 1 << LUA.HOOK.RET,
			/// <summary>The line hook is called when the interpreter is about to start the execution of a new line of code, or when it jumps back in the code (even to the same line). (This event only happens while Lua is executing a Lua function.)</summary>
			LINE  = 1 << LUA.HOOK.LINE,
			/// <summary>The count hook is called after the interpreter executes every count instructions. (This event only happens while Lua is executing a Lua function.)</summary>
			COUNT = 1 << LUA.HOOK.COUNT,
		}
	}
	public static unsafe partial class lua
	{
		/// <summary>A structure used to carry different pieces of information about an active function. <see cref="lua.getstack"/> fills only the private part of this structure, for later use. To fill the other fields of <see cref="lua.Debug"/> with useful information, call <see cref="lua.getinfo"/>.</summary>
		public struct Debug
		{
			/// <summary>Whenever a hook is called, its <see cref="lua.Debug"/> argument has its field <see cref="hook_event"/> set to the specific event that triggered the hook. (see <see cref="LUA.HOOK"/>) Moreover, for line events, the field <see cref="lua.Debug.currentline"/> is also set. To get the value of any other field in <see cref="lua.Debug"/>, the hook must call <see cref="lua.getinfo"/>. For return events, event can be <see cref="LUA.HOOK.RET"/>, the normal value, or <see cref="LUA.HOOK.TAILRET"/>.</summary>
			public LUA.HOOK hook_event;
			/// <summary>See <see cref="name"/>.</summary>
			public char8 *name_raw;
			/// <summary>See <see cref="namewhat"/>.</summary>
			public char8 *namewhat_raw;
			/// <summary>See <see cref="what"/>.</summary>
			public char8 *what_raw;
			/// <summary>See <see cref="source"/>.</summary>
			public char8 *source_raw;
			/// <summary>(l) The current line where the given function is executing. When no line information is available, <see cref="currentline"/> is set to -1.</summary>
			public int currentline;
			/// <summary>(u) The number of upvalues of the function.</summary>
			public int nups;
			/// <summary>(S) The line number where the definition of the function starts.</summary>
			public int linedefined;
			/// <summary>(S) The line number where the definition of the function ends.</summary>
			public int lastlinedefined;
			/// <summary>See <see cref="short_src"/>.</summary>
			public fixed char8 short_src_raw[LUA.IDSIZE];

			/// <summary>(n) A reasonable name for the given function. Because functions in Lua are first-class values, they do not have a fixed name: some functions can be the value of multiple global variables, while others can be stored only in a table field. The <see cref="lua.getinfo"/> function checks how the function was called to find a suitable name. If it cannot find a name, then name is set to <see langword="null"/>.</summary>
			public string name      { get { return Marshal.PtrToStringAnsi(new IntPtr(name_raw)); } }
			/// <summary>(n) Explains the <see cref="name"/> field. The value of <see cref="namewhat"/> can be "global", "local", "method", "field", "upvalue", or "" (the empty string), according to how the function was called. (Lua uses the empty string when no other option seems to apply.)</summary>
			public string namewhat  { get { return Marshal.PtrToStringAnsi(new IntPtr(namewhat_raw)); } }
			/// <summary>(S) The string "Lua" if the function is a Lua function, "C" if it is a C function, "main" if it is the main part of a chunk, and "tail" if it was a function that did a tail call. In the latter case, Lua has no other information about the function.</summary>
			public string what      { get { return Marshal.PtrToStringAnsi(new IntPtr(what_raw)); } }
			/// <summary>(S) If the function was defined in a string, then <see cref="source"/> is that string. If the function was defined in a file, then <see cref="source"/> starts with a '@' followed by the file name.</summary>
			public string source    { get { return Marshal.PtrToStringAnsi(new IntPtr(source_raw)); } }
			/// <summary>(S) A "printable" version of <see cref="source"/>, to be used in error messages.</summary>
			public string short_src { get { fixed (char8 *ptr = short_src_raw) return Marshal.PtrToStringAnsi(new IntPtr(ptr)); } }

			// private part
			#pragma warning disable 169
			int i_ci; // active function
			#pragma warning restore 169
		}

		/// <summary>
		/// <para>Type for debugging hook functions.</para>
		/// <para>Whenever a hook is called, its <paramref name="ar"/> argument has its field <see cref="lua.Debug.hook_event"/> set to the specific event that triggered the hook. (see <see cref="LUA.HOOK"/>) Moreover, for line events, the field <see cref="lua.Debug.currentline"/> is also set. To get the value of any other field in <paramref name="ar"/>, the hook must call <see cref="lua.getinfo"/>. For return events, event can be <see cref="LUA.HOOK.RET"/>, the normal value, or <see cref="LUA.HOOK.TAILRET"/>. In the latter case, Lua is simulating a return from a function that did a tail call; in this case, it is useless to call <see cref="lua.getinfo"/>.</para>
		/// <para>While Lua is running a hook, it disables other calls to hooks. Therefore, if a hook calls back Lua to execute a function or a chunk, this execution occurs without any calls to hooks.</para>
		/// </summary>
		[UnmanagedFunctionPointer(CC)] public delegate void Hook(lua.State L, ref lua.Debug ar);

		/// <summary>[-0, +0, -] <para>Get information about the interpreter runtime stack.</para><para>This function fills parts of a <see cref="lua.Debug"/> structure with an identification of the activation record of the function executing at a given level. Level 0 is the current running function, whereas level n+1 is the function that has called level n. When there are no errors, <see cref="lua.getstack"/> returns <see langword="true"/>; when called with a level greater than the stack depth, it returns <see langword="false"/>.</para></summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_getstack"    )] public static extern bool     getstack    (lua.State L, int level, ref lua.Debug ar);
		/// <summary>
		/// [-(0|1), +(0|1|2), m]
		/// <para>Returns information about a specific function or function invocation.</para>
		/// <para>To get information about a function invocation, the parameter <paramref name="ar"/> must be a valid activation record that was filled by a previous call to <see cref="lua.getstack"/> or given as argument to a hook (see <see cref="lua.Hook"/>).</para>
		/// <para>To get information about a function you push it onto the stack and start the what string with the character '>'. (In that case, <see cref="lua.getinfo"/> pops the function in the top of the stack.) For instance, to know in which line a function f was defined, you can write the following code:</para>
		/// <code>var ar = new lua.Debug();
		///lua.getfield(L, LUA.GLOBALSINDEX, "f");  // get global 'f'
		///lua.getinfo(L, ">S", ref ar);
		///Console.WriteLine(ar.linedefined);</code>
		/// <para>Each character in the string what selects some fields of the structure <paramref name="ar"/> to be filled or a value to be pushed on the stack:</para>
		/// <para>'n': fills in the field <see cref="lua.Debug.name"/> and <see cref="lua.Debug.namewhat"/>;</para>
		/// <para>'S': fills in the fields <see cref="lua.Debug.source"/>, <see cref="lua.Debug.short_src"/>, <see cref="lua.Debug.linedefined"/>, <see cref="lua.Debug.lastlinedefined"/>, and <see cref="lua.Debug.what"/>;</para>
		/// <para>'l': fills in the field <see cref="lua.Debug.currentline"/>;</para>
		/// <para>'u': fills in the field <see cref="lua.Debug.nups"/>;</para>
		/// <para>'f': pushes onto the stack the function that is running at the given level;</para>
		/// <para>'L': pushes onto the stack a table whose indices are the numbers of the lines that are valid on the function. (A valid line is a line with some associated code, that is, a line where you can put a break point. Non-valid lines include empty lines and comments.)</para>
		/// <para>This function returns 0 on error (for instance, an invalid option in <paramref name="what"/>).</para>
		/// </summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_getinfo"     )] public static extern int      getinfo     (lua.State L, string what, ref lua.Debug ar);
		/// <summary>[-0, +(0|1), -]<para>Gets information about a local variable of a given activation record. The parameter <paramref name="ar"/> must be a valid activation record that was filled by a previous call to <see cref="lua.getstack"/> or given as argument to a hook (see <see cref="lua.Hook"/>). The index <paramref name="n"/> selects which local variable to inspect (1 is the first parameter or active local variable, and so on, until the last active local variable). <see cref="lua.getlocal"/> pushes the variable's value onto the stack and returns its name.</para><para>Variable names starting with '(' (open parentheses) represent internal variables (loop control variables, temporaries, and C function locals).</para><para>Returns <see langword="null"/> (and pushes nothing) when the index is greater than the number of active local variables.</para></summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_getlocal"    )] public static extern string   getlocal    (lua.State L, ref lua.Debug ar, int n);
		/// <summary>[-(0|1), +0, -]<para>Sets the value of a local variable of a given activation record. Parameters <paramref name="ar"/> and n are as in lua_getlocal (see lua_getlocal). lua_setlocal assigns the value at the top of the stack to the variable and returns its name. It also pops the value from the stack.</para><para>Returns <see langword="null"/> (and pops nothing) when the index is greater than the number of active local variables.</para></summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_setlocal"    )] public static extern string   setlocal    (lua.State L, ref lua.Debug ar, int n);
		/// <summary>[-0, +(0|1), -]<para>Gets information about a closure's upvalue. (For Lua functions, upvalues are the external local variables that the function uses, and that are consequently included in its closure.) lua.getupvalue gets the index <paramref name="n"/> of an upvalue, pushes the upvalue's value onto the stack, and returns its name. <paramref name="funcindex"/> points to the closure in the stack. (Upvalues have no particular order, as they are active through the whole function. So, they are numbered in an arbitrary order.)</para><para>Returns <see langword="null"/> (and pushes nothing) when the index is greater than the number of upvalues. For C functions, this function uses the empty string "" as a name for all upvalues.</para></summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_getupvalue"  )] public static extern string   getupvalue  (lua.State L, int funcindex, int n);
		/// <summary>[-(0|1), +0, -]<para>Sets the value of a closure's upvalue. It assigns the value at the top of the stack to the upvalue and returns its name. It also pops the value from the stack. Parameters funcindex and n are as in the lua.getupvalue (see <see cref="lua.getupvalue"/>).</para><para>Returns <see langword="null"/> (and pops nothing) when the index is greater than the number of upvalues.</para></summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_setupvalue"  )] public static extern string   setupvalue  (lua.State L, int funcindex, int n);

		/// <summary>[-0, +0, -]<para>Sets the debugging hook function.</para><para>Argument <paramref name="f"/> is the hook function. mask specifies on which events the hook will be called: it is formed by a bitwise or of the <see cref="LUA.MASK"/> constants. The <paramref name="count"/> argument is only meaningful when the mask includes <see cref="LUA.MASK.COUNT"/>.</para><para>A hook is disabled by setting mask to zero.</para></summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_sethook"     )] public static extern int      sethook     (lua.State L, lua.Hook f, LUA.MASK mask, int count);
		/// <summary>[-0, +0, -]<para>Returns the current hook function.</para></summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_gethook"     )] public static extern lua.Hook gethook     (lua.State L);
		/// <summary>[-0, +0, -]<para>Returns the current hook mask.</para></summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_gethookmask" )] public static extern LUA.MASK gethookmask (lua.State L);
		/// <summary>[-0, +0, -]<para>Returns the current hook count.</para></summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_gethookcount")] public static extern int      gethookcount(lua.State L);

		#endregion
	}
}