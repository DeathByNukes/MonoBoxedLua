using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LuaInterface.LuaAPI
{
	using size_t = System.UIntPtr;

	/// <summary>LuaInterface library functions for working with the Lua API.</summary>
	public static class luanet
	{
		const string DLL = "lua51.dll";
		const CallingConvention CC = CallingConvention.Cdecl;
		/// <summary>.NET 4.5 AggressiveInlining. Should be auto discarded on older build targets or otherwise ignored.</summary>
		const MethodImplOptions INLINE = (MethodImplOptions) 0x0100;

		/// <summary>[-0, +0, -] Checks if the object at <paramref name="index"/> has a metatable containing a field with a light userdata key matching <see cref="luanet.gettag"/>.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luaL_checkmetatable")] public static extern bool   checkmetatable(lua.State L, int index);
		/// <summary>[-0, +0, -] The address of a static variable in the luanet DLL. The variable's contents are never used. Rather, the address itself serves as a unique identifier for luanet metatables. (see <see cref="luanet.checkmetatable"/>)</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luanet_gettag"      )] public static extern IntPtr gettag();
		/// <summary>[-0, +0, -] Pushes a new luanet userdata object, which stores a single integer, onto the stack. The object does not have a metatable by default.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luanet_newudata"    )] public static extern int    newudata      (lua.State L, int val);
		/// <summary>[-0, +0, -] Retrieves the int stored in a luanet userdata object. Returns -1 if <see cref="luanet.checkmetatable"/> fails and the object's metatable isn't luaNet_class, luaNet_searchbase, or luaNet_function.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luanet_tonetobject" )] public static extern int    tonetobject   (lua.State L, int index);
		/// <summary>[-0, +0, -] Like <see cref="luanet.tonetobject"/>, but doesn't perform the safety checks. Only use this if you're completely sure the value is a luanet userdata.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luanet_rawnetobj"   )] public static extern int    rawnetobj     (lua.State L, int index);
		/// <summary>[-0, +0, -] Checks if the specified userdata object uses the specified metatable. If so, it does the same thing as <see cref="luanet.rawnetobj"/>. Otherwise, returns -1.</summary>
		[DllImport(DLL,CallingConvention=CC,EntryPoint="luanet_checkudata"  )] public static extern int    checkudata    (lua.State L, int index, string meta);

		/// <summary>[-1, +0, -] Converts the Lua value at the top of the stack to a boolean value and pops it. Like all tests in Lua, <see cref="luanet.popboolean"/> returns true for any Lua value different from false and nil; otherwise it returns false.</summary>
		[MethodImpl(INLINE)] public static bool popboolean(lua.State L)
		{
			bool b = lua.toboolean(L,-1);
			lua.pop(L,1);
			return b;
		}
		/// <summary>
		/// [-0, +1, e] Navigates fields nested in an object at the specified index, pushing the value of the specified sub-field. If <paramref name="fields"/> is empty it pushes a copy of the main object.
		/// <para>WARNING: If the IEnumerable throws an exception during enumeration the stack will be left in a +1 state. You must ensure that no exceptions can be thrown or you must catch them and clean up the stack.</para>
		/// </summary>
		public static void getnestedfield(lua.State L, int index, IEnumerable<string> fields)
		{
			Debug.Assert(fields != null);                StackAssert.Start(L);
			lua.pushvalue(L,index);
			foreach (string field in fields) {
				lua.getfield(L, -1, field);
				lua.remove(L,-2);
			}                                            StackAssert.End(1);
		}
		/// <summary>[-0, +1, e] Navigates fields nested in an object at the top of the stack, pushing the value of the specified sub-field</summary>
		/// <param name="index"></param><param name="L"></param>
		/// <param name="path">A string with field names separated by period characters.</param>
		[MethodImpl(INLINE)] public static void getnestedfield(lua.State L, int index, string path)
		{
			Debug.Assert(path != null);
			luanet.getnestedfield(L, index, path.Split('.'));
		}

		/// <summary>[-0, +0, -] Gets a <see cref="Lua"/> instance's internal lua_State pointer.</summary>
		[MethodImpl(INLINE)] public static lua.State getstate(Lua interpreter)
		{
			return interpreter._L;
		}
		/// <summary>[-0, +1, e] Pushes the referenced object onto its owner's stack.</summary>
		[MethodImpl(INLINE)] public static void pushobject<T>(T o)
		where T : LuaBase // generic method rather than just taking a LuaBase parameter probably makes it easier to do sealed class optimizations (todo: test this hypothesis)
		{
			o.push(o.Owner._L);
		}
		/// <summary>[-0, +1, e] Pushes an arbitrary CLR object onto the stack.</summary>
		[MethodImpl(INLINE)] public static void pushobject(Lua lua, object o)
		{
			lua.translator.push(lua._L, o);
		}
		/// <summary>[-0, +0, m] Gets a Lua object from the stack and translates it to a CLR object.</summary>
		[MethodImpl(INLINE)] public static object getobject(Lua lua, int index)
		{
			return lua.translator.getObject(lua._L, index);
		}

		/// <summary>[-0, +1, m] Pushes a delegate onto the stack as a callable userdata.</summary>
		[Obsolete("Use lua.pushcfunction")]
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_pushstdcallcfunction")] public static extern void pushstdcallcfunction(lua.State L, luanet.CSFunction function);

		/// <summary>Delegate for functions passed to Lua as function pointers</summary>
		[Obsolete("Use lua.CFunction")]
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CSFunction(lua.State L);
	}
}