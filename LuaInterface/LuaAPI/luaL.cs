using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LuaInterface.LuaAPI
{
	using size_t = System.UIntPtr;

	/// <summary>
	/// P/Invoke wrapper of the Lua Auxiliary Library.
	/// The auxiliary library provides several convenient functions to interface C with Lua. While the basic API provides the primitive functions for all interactions between C and Lua, the auxiliary library provides higher-level functions for some common tasks.
	/// All functions in the auxiliary library are built on top of the basic API, and so they provide nothing that cannot be done with this API.
	/// </summary>
	public static unsafe partial class luaL
	{
		const string DLL = "lua51.dll"; // lua aux lib
		const CallingConvention CC = CallingConvention.Cdecl;
		/// <summary>.NET 4.5 AggressiveInlining. Should be auto discarded on older build targets or otherwise ignored.</summary>
		const MethodImplOptions INLINE = (MethodImplOptions) 0x0100;


		/// <summary>[-0, +0, -] Creates a new Lua state. It calls lua_newstate with an allocator based on the standard C realloc function and then sets a panic function (see <see cref="lua.atpanic"/>) that prints an error message to the standard error output in case of fatal errors.</summary><returns>Returns the new state, or NULL if there is a memory allocation error.</returns>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luaL_newstate")] public static extern lua.State newstate();

		/// <summary>[-0, +0, -] Returns the name of the type of the value at the given index.</summary>
		public static string typename(lua.State L, int index) { return lua.typename(L, lua.type(L, index)); }

		/// <summary>[-0, +1, -] Pushes onto the stack the metatable associated with name tname in the registry (see <see cref="luaL.newmetatable"/>).</summary>
		[MethodImpl(INLINE)] public static void getmetatable(lua.State L, string tname) { lua.getfield(L, LUA.REGISTRYINDEX, tname); }
		/// <summary>[-0, +1, m] If the registry already has the key tname, returns false. Otherwise, creates a new table to be used as a metatable for userdata, adds it to the registry with key tname, and returns true. In both cases pushes onto the stack the final value associated with tname in the registry.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luaL_newmetatable")] public static extern bool    newmetatable(lua.State L, string tname);
		/// <summary>[-0, +(0|1), m] Pushes onto the stack the field e from the metatable of the object at index obj. If the object does not have a metatable, or if the metatable does not have this field, returns false and pushes nothing.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luaL_getmetafield")] public static extern bool    getmetafield(lua.State L, int index, string field);

		/// <summary>[-0, +1, m] Loads a buffer as a Lua chunk. This function uses <see cref="lua.load"/> to load the chunk in the buffer pointed to by buff with size sz. <paramref name="name"/> is the chunk name, used for debug information and error messages.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luaL_loadbuffer"  )] public static extern LUA.ERR loadbuffer  (lua.State L, IntPtr buff, int sz, string name);
		/// <summary>[-0, +1, m] Loads a string as a Lua chunk. This function uses <see cref="lua.load"/> to load the chunk in the zero-terminated string s.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luaL_loadstring"  )] public static extern LUA.ERR loadstring  (lua.State L, string s);
		/// <summary>[-0, +1, m] Loads a file as a Lua chunk. This function uses <see cref="lua.load"/> to load the chunk in the file named filename. If filename is NULL, then it loads from the standard input. The first line in the file is ignored if it starts with a #.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luaL_loadfile"    )] public static extern LUA.ERR loadfile    (lua.State L, string filename);
		/// <summary>[-0, +(0|1), e] Calls a metamethod. If the object at index <paramref name="obj"/> has a metatable and this metatable has a field e, this function calls this field and passes the object as its only argument. In this case this function returns true and pushes onto the stack the value returned by the call. If there is no metatable or no metamethod, this function returns false (without pushing any value on the stack).</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luaL_callmeta"    )] public static extern bool    callmeta    (lua.State L, int obj, string e);

		/// <summary>[-0, +1, m] Loads a buffer as a Lua chunk. This function uses <see cref="lua.load"/> to load the chunk in the string s, which can contain embedded zeros. <paramref name="name"/> is the chunk name, used for debug information and error messages.</summary>
		public static LUA.ERR loadbuffer(lua.State L, string s, string name)
		{
			Debug.Assert(s != null);
			IntPtr ptr = Marshal.StringToHGlobalAnsi(s);
			try { return luaL.loadbuffer(L, ptr, s.Length, name); }
			finally { Marshal.FreeHGlobal(ptr); }
		}
		/// <summary>[-0, +?, m] Loads and runs the given string, which can contain embedded zeros.</summary>
		[MethodImpl(INLINE)] public static LUA.ERR dostring(lua.State L, string str)
		{
			var result = luaL.loadstring(L, str);
			return result != LUA.ERR.Success ? result : lua.pcall(L, 0, LUA.MULTRET, 0);
		}
		/// <summary>[-0, +?, m] Loads and runs the given file.</summary>
		[MethodImpl(INLINE)] public static LUA.ERR dofile(lua.State L, string filename)
		{
			var result = luaL.loadfile(L, filename);
			return result != LUA.ERR.Success ? result : lua.pcall(L, 0, LUA.MULTRET, 0);
		}

		/// <summary>[-0, +0, m] Opens all standard Lua libraries into the given state.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luaL_openlibs")] public static extern void openlibs(lua.State L);
		/*
		[DllImport(DLL,CallingConvention=CC)] public static extern void luaopen_base   (lua.State L);
		[DllImport(DLL,CallingConvention=CC)] public static extern void luaopen_io     (lua.State L);
		[DllImport(DLL,CallingConvention=CC)] public static extern void luaopen_table  (lua.State L);
		[DllImport(DLL,CallingConvention=CC)] public static extern void luaopen_string (lua.State L);
		[DllImport(DLL,CallingConvention=CC)] public static extern void luaopen_math   (lua.State L);
		[DllImport(DLL,CallingConvention=CC)] public static extern void luaopen_debug  (lua.State L);
		[DllImport(DLL,CallingConvention=CC)] public static extern void luaopen_loadlib(lua.State L);
		*/

		/// <summary>[-0, +1, m] Pushes onto the stack a string identifying the current position of the control at level lvl in the call stack. Typically this string has the following format: <c>chunkname:currentline:</c> Level 0 is the running function, level 1 is the function that called the running function, etc. This function is used to build a prefix for error messages.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luaL_where")] public static extern void where(lua.State L, int level);
		// omitted:
		//   lua_getallocf (no C functions/pointers)
		//   lua_setallocf (no C functions/pointers)

		/// <summary>[-0, +0, v] Raises an error. The error message format is given by fmt plus any extra arguments, following the same rules of String.Format. It also adds at the beginning of the message the file name and the line number where the error occurred, if this information is available. This function never returns, but it is an idiom to use it in C functions as <c>return luaL.error(args)</c>.</summary>
		[MethodImpl(INLINE)] public static int error(lua.State L, string fmt, params object[] args) { return luaL.error(L, string.Format(fmt, args)); }
		/// <summary>[-0, +0, v] Raises an error. It adds at the beginning of the message the file name and the line number where the error occurred, if this information is available. This function never returns, but it is an idiom to use it in C functions as <c>return luaL.error(args)</c>.</summary>
		public static int error(lua.State L, string message)
		{
			// re-implemented to avoid headaches with native variable arguments and Lua's format strings
			lua.pushstring(L, luanet.where(L, 1) + message);
			return lua.error(L);
		}

		/// <summary>[-0, +0, v] Grows the stack size to top + <paramref name="sz"/> elements, raising an error if the stack cannot grow to that size.</summary>
		/// <param name="L"></param><param name="sz"></param><param name="mes">An additional text to go into the error message.</param>
		public static void checkstack(lua.State L, int sz, string mes)
		{
			// luaL_checkstack shouldn't be pinvoked. the message would be marshaled with each call but rarely used.
			Debug.Assert(!string.IsNullOrEmpty(mes));
			if (!lua.checkstack(L, sz))
				luaL.error(L, "stack overflow ({0})", mes);
		}
	}

	public static partial class LUA
	{
		/// <summary>Special references. (see <see cref="luaL.@ref(lua.State,int)"/>)</summary>
		public const int
		MinRef =  1, // see http://www.lua.org/source/5.1/lauxlib.c.html#luaL_ref, registry[0] is FREELIST_REF
		NOREF  = -2,
		REFNIL = -1;
	}

	public static unsafe partial class luaL
	{
		/// <summary>[-1, +0, m] Creates and returns a reference, in the table at index t, for the object at the top of the stack (and pops the object).</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luaL_ref"  )] public static extern int  @ref (lua.State L, int t);
		/// <summary>[-0, +0, -] Releases reference ref from the table at index t (see <see cref="@ref(lua.State,int)"/>). The entry is removed from the table, so that the referred object can be collected. The reference ref is also freed to be used again.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luaL_unref")] public static extern void unref(lua.State L, int t, int reference);

		/// <summary>[-1, +0, m] Creates and returns a reference for the object at the top of the stack (and pops the object).</summary>
		[MethodImpl(INLINE)] public static int  @ref  (lua.State L)                { return luaL.@ref(L,LUA.REGISTRYINDEX); }
		/// <summary>[-0, +0, -] Releases reference ref. The entry is removed from the registry, so that the referred object can be collected. The reference ref is also freed to be used again.</summary>
		[MethodImpl(INLINE)] public static void unref (lua.State L, int reference) {       luaL.unref(L,LUA.REGISTRYINDEX,reference); }
		/// <summary>[-0, +1, -] Pushes the referenced object onto the stack.</summary>
		[MethodImpl(INLINE)] public static void getref(lua.State L, int reference) {      lua.rawgeti(L,LUA.REGISTRYINDEX,reference); }

		/*
			Several functions in the auxiliary library are used to check C function arguments. Their names are
			always luaL_check* or luaL_opt*. All of these functions throw an error if the check is not satisfied.
			Because the error message is formatted for arguments (e.g., "bad argument #1"), you should not use
			these functions for other stack values.
		*/

		/// <summary>[-0, +0, v] Checks whether the function argument narg is a userdata of the type tname (see <see cref="luaL.newmetatable"/>).</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luaL_checkudata")] public static extern IntPtr checkudata(lua.State L, int narg, string meta);
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
		//
		//   luaL_add*       (use System.String concatenation or similar)
		//   luaL_argcheck   (function argument checking unnecessary)
		//   luaL_argerror   (function argument checking unnecessary)
		//   luaL_buffinit   (use System.String concatenation or similar)
		//   luaL_prepbuffer (use System.String concatenation or similar)
		//   luaL_pushresult (use System.String concatenation or similar)
		//   luaL_gsub       (use System.String concatenation or similar)
		//   luaL_register   (all libs already opened, use require in scripts for external libs)
		//   luaL_typerror   (function argument checking unnecessary)
	}
}