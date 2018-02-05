using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LuaInterface.LuaAPI;

namespace LuaInterface
{
	/// <summary>Normally, Lua strings get automatically converted to CLR strings. You can demand the original by making your function arguments of this type.</summary>
	public sealed class LuaString : LuaBase
	{
		/// <summary>Copies the string's value to a CLR string.</summary>
		public string Value
		{
			get
			{
				var L = Owner._L;
				luanet.checkstack(L, 1, "LuaString.Value");
				push(L);
				var ret = lua.tostring(L, -1);
				lua.pop(L, 1);
				return ret;
			}
		}

		/// <summary>Copies the string's value to a byte array.</summary>
		public unsafe byte[] RawValue
		{
			get
			{
				var L = Owner._L;
				luanet.checkstack(L, 1, "LuaString.RawValue");
				push(L);
				try
				{
					UIntPtr len;
					void* ptr = lua.tolstring(L, -1, out len);
					var ret = new byte[len.ToInt32()];
					Marshal.Copy(new IntPtr(ptr), ret, 0, ret.Length);
					return ret;
				}
				finally { lua.pop(L, 1); }
			}
		}

		#region Implementation

		/// <summary>Makes a new reference the same string.</summary>
		public LuaString NewReference()
		{
			var L = Owner._L;
			rawpush(L);
			try { return new LuaString(L, Owner); } catch (InvalidCastException) { Dispose(); throw; }
		}

		/// <summary>[-1, +0, e] Pops a userdata from the top of the stack and creates a new reference. The value is discarded if a type exception is thrown.</summary>
		public LuaString(lua.State L, Lua interpreter)
		: base(TryRef(L, interpreter, LUA.T.STRING), interpreter)
		{
		}
		public LuaString(int reference, Lua interpreter)
		: base(reference, interpreter)
		{
			CheckType(LUA.T.STRING);
		}

		protected internal override void push(lua.State L)
		{
			Debug.Assert(L == Owner._L);
			luaL.getref(L, Reference);
			CheckType(L, LUA.T.STRING);
		}

		#endregion
	}
}