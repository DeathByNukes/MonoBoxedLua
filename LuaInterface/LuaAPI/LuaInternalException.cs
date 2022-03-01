using System;
using System.Diagnostics;

namespace LuaInterface.LuaAPI
{
	/// <summary>This exception is for internal Lua use and should not be caught. (Catch and rethrow is ok though.)</summary>
	[DebuggerDisplay("LuaInternalException({ErrCode}) DO NOT CATCH")]
	public sealed class LuaInternalException : Exception
	{
		const string _Message = "A Lua error was thrown. This exception is for internal Lua use and should not be caught.";
		private LuaInternalException(lua.State L, LUA.ERR errcode)
		: base(_Message)
		{
			this.L = L;
			this.ErrCode = errcode;
		}

		/// <summary>Accessing this property will terminate the process.</summary>
		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		public override string Message { get { return _badCall("Message"); } }

		/// <summary>Calling this function will terminate the process. If you really need the string prior to rethrowing, call <see cref="ToStringSafe"/>.</summary>
		public override string ToString() { return _badCall("ToString()"); }

		/// <summary>Convert the exception to a string.</summary>
		public string ToStringSafe() { return base.ToString(); }

		string _badCall(string fn)
		{
			#if DEBUG
			Debug.Assert(false, "This exception is for internal Lua use and should not be caught.");
			#else
			if (Debugger.IsAttached)
				Debugger.Break();
			#endif
			Environment.FailFast(string.Format("LuaInternalException.{0} was called. This might mean it was accidentally caught by a general catch, which can result in memory corruption.", fn));
			throw null;
		}

		public readonly lua.State L;

		public readonly LUA.ERR ErrCode;

		public Lua Owner { get { return Lua.GetOwner(this.L); } }

		/// <summary>Sets <paramref name="L"/> to implement Lua errors using CLR exceptions. This should only be used when when Lua code is not running on any Lua thread; it is dangerous to change the error system while a pcall might already be running. (This function calls <see cref="luaclr.settrythrowf"/>)</summary>
		public static void install(lua.State L)
		{
			luaclr.settrythrowf(L, _FTry, _FThrow);
		}

		static readonly luaclr.Throw _FThrow = (L, errcode) =>
		{
			throw new LuaInternalException(L, errcode);
		};
		static readonly luaclr.Try _FTry = (L, f, ud) =>
		{
			try { f(L, ud); return 0; }
			catch (LuaInternalException ex)
			{
				if (!luaclr.issamelua(L, ex.L))
					Environment.FailFast("Lua error hit another Lua instance's protected call.");
				return -1;
			}
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
		: base(_Message, ex)
		{
			this.L = ex.L;
			this.ErrCode = ex.ErrCode;
		}
	}
}
