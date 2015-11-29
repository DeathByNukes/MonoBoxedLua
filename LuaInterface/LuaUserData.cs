using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LuaInterface.LuaAPI;

namespace LuaInterface
{
	public sealed class LuaUserData : LuaBase
	{
		/// <summary>Length + Pointer</summary>
		public unsafe struct LPtr
		{
			public void* Address;
			public UIntPtr Length;
		}
		/// <summary>The address and size of the block of memory allocated for the userdata.</summary>
		public unsafe LPtr Block
		{
			get
			{
				var L = Owner._L;
				push(L);
				var ret = new LPtr {
					Address = lua.touserdata(L,-1),
					Length  = lua.objlen(L,-1),
				};
				lua.pop(L,1);
				return ret;
			}
		}

		/// <summary>Copies a byte array to/from the block of memory allocated for the userdata.</summary>
		public unsafe byte[] Contents
		{
			get
			{
				var block = this.Block;
				var len = block.Length.ToInt32();
				var array = new byte[len];
				Marshal.Copy(new IntPtr(block.Address), array, 0, len);
				return array;
			}
			set
			{
				var block = this.Block;
				int len = block.Length.ToMaxInt32();
				Marshal.Copy(value, 0, new IntPtr(block.Address), len);
			}
		}

		#region Implementation

		/// <summary>Makes a new reference the same userdata.</summary>
		public LuaUserData NewReference()
		{
			var L = Owner._L;
			rawpush(L);
			try { return new LuaUserData(L, Owner); } catch (InvalidCastException) { Dispose(); throw; }
		}

		/// <summary>[-1, +0, e] Pops a userdata from the top of the stack and creates a new reference. The value is discarded if a type exception is thrown.</summary>
		public LuaUserData(lua.State L, Lua interpreter)
		: base(TryRef(L, interpreter, LUA.T.USERDATA), interpreter)
		{
		}
		public LuaUserData(int reference, Lua interpreter)
		: base(reference, interpreter)
		{
			CheckType(LUA.T.USERDATA);
		}

		protected internal override void push(lua.State L)
		{
			Debug.Assert(L == Owner._L);
			luaL.getref(L, Reference);
			CheckType(L, LUA.T.USERDATA);
		}

		#endregion
	}
}