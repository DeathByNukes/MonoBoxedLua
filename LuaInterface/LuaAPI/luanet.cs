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

		/// <summary>[-(nargs + 1), +(nresults|0), -]</summary>
		[MethodImpl(INLINE)] public static Exception pcall(lua.State L, int nargs, int nresults)
		{
			if (lua.pcall(L, nargs, nresults, 0) == LUA.ERR.Success)
				return null;
			var message = lua.tostring(L,-1) ?? "Error message was a "+lua.type(L,-1).ToString();
			lua.pop(L,1);
			return new LuaScriptException(message, ""); // todo
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

		/// <summary>Checks what kind of index this is. <see cref="IndexTypes.Invalid"/> represents zero and very large negative numbers.</summary>
		public static IndexTypes indextype(int index)
		{
			if (index > 0)  return IndexTypes.Absolute;
			// comparisons with LUA_*INDEX is what Lua does internally
			if (index > LUA.REGISTRYINDEX) return index == 0 ? IndexTypes.Invalid : IndexTypes.Relative;
			if (index >= LUA.GLOBALSINDEX) return IndexTypes.Pseudo;
			if (index >= lua.upvalueindex(256)) return IndexTypes.Upvalue; // http://www.lua.org/manual/5.1/manual.html#3.4
			return IndexTypes.Invalid;
		}
		/// <summary>Returned by <see cref="luanet.indextype"/>.</summary>
		public enum IndexTypes { Invalid, Absolute, Relative, Pseudo, Upvalue }

		/// <summary>[-0, +0, -] Converts relative index to a normal (absolute) index. If the relative index goes out of the stack bounds, 0 is returned. If <paramref name="index"/> is not a relative index it is returned unchanged.</summary>
		public static int absoluteindex(lua.State L, int index)
		{
			if (index >= 0 || index <= LUA.REGISTRYINDEX)
				return index;
			int i = lua.gettop(L) + index + 1;
			return i < 0 ? 0 : i;
		}

		/// <summary>[-0, +0, -] Returns whether the currently executing code was called by Lua.</summary>
		public static bool infunction(lua.State L)
		{
			// this function is should resolve to the same thing as "L->ci != L->base_ci" in internal lua code
			var ar = new lua.Debug();
			return lua.getstack(L, 0, ref ar); // returns unsuccessful if there is no stack
		}

		/// <summary>[-0, +1, m] Pushes a delegate onto the stack as a callable userdata.</summary>
		[Obsolete("Use lua.pushcfunction")]
		[DllImport(DLL,CallingConvention=CC,EntryPoint="lua_pushstdcallcfunction")] public static extern void pushstdcallcfunction(lua.State L, luanet.CSFunction function);

		/// <summary>Delegate for functions passed to Lua as function pointers</summary>
		[Obsolete("Use lua.CFunction")]
		[UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CSFunction(lua.State L);
	}
}