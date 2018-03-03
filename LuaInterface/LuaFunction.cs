using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using LuaInterface.LuaAPI;

namespace LuaInterface
{
	public sealed class LuaFunction : LuaBase
	{
		/// <summary>Dumps a binary chunk that, if loaded again, results in a function equivalent to the one dumped.</summary>
		/// <exception cref="InvalidOperationException">The function is not implemented in Lua.</exception>
		public byte[] Dump()
		{
			var ms = new MemoryStream();
			Dump(ms);
			return ms.ToArray();
		}
		/// <summary>Dumps a binary chunk that, if loaded again, results in a function equivalent to the one dumped.</summary>
		/// <exception cref="InvalidOperationException">The function is not implemented in Lua.</exception>
		public unsafe void Dump(Stream output)
		{
			var L = Owner._L;
			luanet.checkstack(L, 1, "LuaFunction.Dump");
			int oldTop = lua.gettop(L);
			push(L);
			int err;
			try
			{
				byte[] buffer = null;
				int buffer_sz = -1;
				err = lua.dump(L, (L2, p, sz, ud) =>
				{
					var sz32 = sz.ToInt32();
					Debug.Assert(sz32 >= 0);
					if (sz32 > buffer_sz)
					{
						buffer = new byte[sz32];
						buffer_sz = sz32;
					}
					Marshal.Copy(new IntPtr(p), buffer, 0, sz32);
					output.Write(buffer, 0, sz32);
					return 0;
				}, default(IntPtr));
			}
			catch (LuaInternalException) { oldTop = -1; throw; }
			finally { lua.settop(L, oldTop); }
			if (err == 0)
				return;
			Debug.Assert(err == 1);
			throw new InvalidOperationException("Only native Lua functions can be dumped.");
		}

		#region Implementation

		/// <summary>Makes a new reference the same function.</summary>
		public LuaFunction NewReference()
		{
			var L = Owner._L;
			rawpush(L);
			return new LuaFunction(L, Owner);
		}

		protected override LUA.T Type { get { return LUA.T.FUNCTION; } }

		/// <summary>[-1, +0, v] Pops a function from the top of the stack and creates a new reference. Raises a Lua error if the value isn't a function.</summary>
		public LuaFunction(lua.State L, Lua interpreter) : base(L, interpreter) {}
		/// <summary>[-0, +0, v] Creates a new reference to the function at <paramref name="index"/>. Raises a Lua error if the value isn't a function.</summary>
		public LuaFunction(lua.State L, Lua interpreter, int index) : base(L, interpreter, index) {}

		#endregion
	}

}