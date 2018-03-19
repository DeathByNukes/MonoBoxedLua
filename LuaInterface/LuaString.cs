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
				luaclr.checkstack(L, 1, "LuaString.Value");
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
				luaclr.checkstack(L, 1, "LuaString.RawValue");
				push(L);
				int oldTop = -2;
				try
				{
					UIntPtr len;
					void* ptr = lua.tolstring(L, -1, out len);
					var ret = new byte[len.ToInt32()];
					Marshal.Copy(new IntPtr(ptr), ret, 0, ret.Length);
					return ret;
				}
				catch (LuaInternalException) { oldTop = -1; throw; }
				finally { lua.settop(L, oldTop); }
			}
		}

		#region Implementation

		/// <summary>Makes a new reference the same string.</summary>
		public LuaString NewReference()
		{
			var L = Owner._L;
			rawpush(L);
			return new LuaString(L, Owner);
		}

		protected override LUA.T Type { get { return LUA.T.STRING; } }

		/// <summary>[-1, +0, v] Pops a string from the top of the stack and creates a new reference. Raises a Lua error if the value isn't a string.</summary>
		public LuaString(lua.State L, Lua interpreter) : base(L, interpreter) {}
		/// <summary>[-0, +0, v] Creates a new reference to the string at <paramref name="index"/>. Raises a Lua error if the value isn't a string.</summary>
		public LuaString(lua.State L, Lua interpreter, int index) : base(L, interpreter, index) {}

		#endregion
	}
}