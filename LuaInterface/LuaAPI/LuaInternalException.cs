using System;
using System.Diagnostics;

namespace LuaInterface.LuaAPI
{
	/// <summary>This exception is for internal Lua use and should not be caught. (Catch and rethrow is ok though.)</summary>
	public sealed class LuaInternalException : Exception
	{
		private LuaInternalException(LUA.ERR errcode)
		: base("A Lua error (LUA_ERR" + errcode.ToString() + ") was thrown. This exception is for internal Lua use and should not be caught.")
		{
			this.ErrCode = errcode;
		}

		public readonly LUA.ERR ErrCode;

		/// <summary>Sets <paramref name="L"/> to implement Lua errors using CLR exceptions. This should only be used when when Lua code is not running on any Lua thread; it is dangerous to change the error system while a pcall might already be running. (This function calls <see cref="luaclr.settrythrowf"/>)</summary>
		public static void install(lua.State L)
		{
			luaclr.settrythrowf(L, _FTry, _FThrow);
		}

		static readonly luaclr.Throw _FThrow = (L, errcode) =>
		{
			throw new LuaInternalException((LUA.ERR) errcode);
		};
		static readonly luaclr.Try _FTry = (L, f, ud) =>
		{
			try { f(L, ud); return 0; }
			catch (LuaInternalException) { return -1; }
			catch (Exception ex) // Lua's C++ exception system uses a general catch, so let's assume that's the safest choice
			{
				Debug.Assert(ex.GetType() == typeof(Exception) && ex.Message == "unit test", "A CLR exception was not properly translated to a Lua error.");
				if (ex is OutOfMemoryException == false && luaclr.getfreestack(L) >= 1)
				{
					// let's try retconning this into a real error
					string s = null;
					try
					{
						// todo: try safely pushing an actual exception userdata
						lua.pushstring(L, s = ex.ToString());
						return (int) LUA.ERR.RUN;
					}
					catch
					{
						string msg = (s == null)
							? "Exception.ToString() threw an exception."
							: "Lua ran out of memory while handling an exception: " + s  ;
						Environment.FailFast(msg);
						return 1; // never reached
					}
				}
				return -1;
			}
		};

		/// <summary>Re-throw this exception in contexts where "throw;" isn't available. (<see cref="System.Reflection.TargetInvocationException"/>, etc.)</summary>
		public void Rethrow()
		{
			// we could do a lot of gymnastics to preserve the original stack trace without using .NET 4.5
			// but it's unnecessary since we're the only ones that should be catching this exception and we don't use that info
			throw new LuaInternalException(this);
		}
		private LuaInternalException(LuaInternalException ex)
		: base(ex.Message, ex)
		{
			this.ErrCode = ex.ErrCode;
		}
	}
}
