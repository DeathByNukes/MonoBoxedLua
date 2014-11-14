using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LuaInterface.LuaAPI
{
	using size_t = System.UIntPtr;

	/// <summary>LuaInterface library functions for working with the Lua API.</summary>
	public static unsafe partial class luanet
	{
		const string DLL = "lua51.dll";
		const CallingConvention CC = CallingConvention.Cdecl;
		/// <summary>.NET 4.5 AggressiveInlining. Should be auto discarded on older build targets or otherwise ignored.</summary>
		const MethodImplOptions INLINE = (MethodImplOptions) 0x0100;

		/// <summary>[-0, +0, -] Checks if the object at <paramref name="index"/> has a metatable containing a field with a light userdata key matching <see cref="luanet.gettag"/>.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luaL_checkmetatable")] public static extern bool   checkmetatable(IntPtr luaState, int index);
		/// <summary>[-0, +0, -] The address of a static variable in the luanet DLL. The variable's contents are never used. Rather, the address itself serves as a unique identifier for luanet metatables. (see <see cref="luanet.checkmetatable"/>)</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luanet_gettag"      )] public static extern IntPtr gettag();
		/// <summary>[-0, +0, -] Pushes a new luanet userdata object, which stores a single integer, onto the stack. The object does not have a metatable by default.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luanet_newudata"    )] public static extern int    newudata      (IntPtr luaState, int val);
		/// <summary>[-0, +0, -] Retrieves the int stored in a luanet userdata object. Returns -1 if <see cref="luanet.checkmetatable"/> fails and the object's metatable isn't luaNet_class, luaNet_searchbase, or luaNet_function.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luanet_tonetobject" )] public static extern int    tonetobject   (IntPtr luaState, int index);
		/// <summary>[-0, +0, -] Like <see cref="luanet.tonetobject"/>, but doesn't perform the safety checks. Only use this if you're completely sure the value is a luanet userdata.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luanet_rawnetobj"   )] public static extern int    rawnetobj     (IntPtr luaState, int index);
		/// <summary>[-0, +0, -] Checks if the specified userdata object uses the specified metatable. If so, it does the same thing as <see cref="luanet.rawnetobj"/>. Otherwise, returns -1.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luanet_checkudata"  )] public static extern int    checkudata    (IntPtr luaState, int index, string meta);

		/// <summary>[-1, +0, -] Converts the Lua value at the top of the stack to a boolean value and pops it. Like all tests in Lua, <see cref="luanet.popboolean"/> returns true for any Lua value different from false and nil; otherwise it returns false.</summary>
		[MethodImpl(INLINE)] public static bool popboolean(IntPtr luaState)
		{
			bool b = lua.toboolean(luaState,-1);
			lua.pop(luaState,1);
			return b;
		}
		/// <summary>[-0, +1, e] Navigates fields nested in an object at the specified index, pushing the value of the specified sub-field. If <paramref name="fields"/> is empty it pushes a copy of the main object.</summary>
		public static void getnestedfield(IntPtr luaState, int index, IEnumerable<string> fields)
		{
			Debug.Assert(fields != null);                StackAssert.Start(luaState);
			lua.pushvalue(luaState,index);
			foreach (string field in fields) {
				lua.getfield(luaState, -1, field);
				lua.remove(luaState,-2);
			}                                            StackAssert.End(1);
		}
		/// <summary>[-0, +1, e] Navigates fields nested in an object at the top of the stack, pushing the value of the specified sub-field</summary>
		/// <param name="index"></param><param name="luaState"></param>
		/// <param name="path">A string with field names separated by period characters.</param>
		[MethodImpl(INLINE)] public static void getnestedfield(IntPtr luaState, int index, string path)
		{
			Debug.Assert(path != null);
			luanet.getnestedfield(luaState, index, path.Split('.'));
		}

		/// <summary>[-0, +0, -] Gets a <see cref="Lua"/> instance's internal lua_State pointer.</summary>
		[MethodImpl(INLINE)] public static IntPtr getstate(Lua interpreter)
		{
			return interpreter.luaState;
		}
		/// <summary>[-0, +1, e] Pushes the referenced object onto the stack.</summary>
		[MethodImpl(INLINE)] public static void pushobject<T>(IntPtr luaState, T o)
		where T : LuaBase // generic method rather than just taking a LuaBase parameter probably makes it easier to do sealed class optimizations (todo: test this hypothesis)
		{
			Debug.Assert(o != null && luaState == o.Owner.luaState);
			o.push(luaState);
		}

		/// <summary>[-0, +1, m] Pushes a delegate onto the stack as a C function. http://www.lua.org/manual/5.1/manual.html#lua_CFunction </summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_pushstdcallcfunction")] public static extern void pushstdcallcfunction(IntPtr luaState, [MarshalAs(UnmanagedType.FunctionPtr)]LuaCSFunction function);
	}

	/// <summary>Delegate for functions passed to Lua as function pointers</summary>
	public delegate int LuaCSFunction(IntPtr luaState);
}