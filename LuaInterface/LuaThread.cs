using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LuaInterface.LuaAPI;

namespace LuaInterface
{
	/// <summary>A Lua thread.</summary>
	public sealed class LuaThread : LuaBase
	{
		// see the assert in consistent()
		// /// <summary>Whether this thread is the Lua instance's main thread.</summary>
		// public bool IsMainThread { get { return _co == this.Owner._mainthread; } }

		/// <summary>The status can be 0 for a normal thread, an error code if the thread finished its execution with an error, or <see cref="LuaStatus.Yield"/> if the thread is suspended.</summary>
		public LuaStatus LuaStatus { get { return (LuaStatus)(int) lua.status(_co); } }

		/// <summary>Returns the same kind of results as the Lua "coroutine.status" function.</summary>
		public LuaCoStatus Status { get { return CoStatus(this.Owner._L, _co); } }

		// copied from Lua lbaselib.c costatus()
		static LuaCoStatus CoStatus(lua.State L, lua.State co)
		{
			if (L == co) return LuaCoStatus.Running;
			switch (lua.status(co))
			{
			case LUA.YIELD:
				return LuaCoStatus.Suspended;
			case 0: {
				var ar = default(lua.Debug);
				if (lua.getstack(co, 0, ref ar))  // does it have frames?
					return LuaCoStatus.Normal;  // it is running
				else if (lua.gettop(co) == 0)
					return LuaCoStatus.Dead;
				else
					return LuaCoStatus.Suspended;  // initial state
			}
			default:  // some error occured
				return LuaCoStatus.Dead;
			}
		}

		/// <summary>Starts or continues the execution of the coroutine. The first time you resume a coroutine, it starts running its body. The values in <paramref name="args"/> are passed as the arguments to the body function. If the coroutine has yielded, resume restarts it; the values in <paramref name="args"/> are passed as the results from the yield.</summary>
		public object[] Resume(params object[] args)
		{
			// copied from Lua lbaselib.c auxresume()
			var owner = this.Owner;
			var translator = owner.translator;
			var L = owner._L;
			var co = _co;
			int narg = args == null ? 0 : args.Length;
			luanet.checkstack(L, narg, "LuaThread.Resume");
			if (!lua.checkstack(co, narg))
				throw new LuaException("too many arguments to resume");
			var costatus = CoStatus(L, co);
			if (costatus != LuaCoStatus.Suspended)
				throw new LuaException(string.Format("cannot resume {0} coroutine", costatus.ToString().ToLowerInvariant()));

			// we can't just push directly to the target thread because that could throw errors on the target thread
			for(int i = 0; i < narg; ++i)
				translator.push(L, args[i]);
			lua.xmove(L, co, narg);
			lua.setlevel(L, co); // ¯\_(ツ)_/¯
			var status = lua.resume(co, narg);
			if (status == 0 || status == LUA.YIELD) {
				int nres = lua.gettop(co);
				if (!lua.checkstack(L, nres + 1))
				{
					lua.settop(co, 0);
					throw new LuaException("too many results to resume");
				}
				var oldtop = lua.gettop(L);
				lua.xmove(co, L, nres);  // move yielded values
				return translator.popValues(L, oldtop);
			}
			else {
				lua.xmove(co, L, 1);  // move error message
				throw owner.ExceptionFromError(L, -2);
			}
		}

		#region Implementation

		/// <summary>Makes a new reference the same thread.</summary>
		public LuaThread NewReference()
		{
			var L = Owner._L;
			rawpush(L);
			return new LuaThread(L, _co, Owner);
		}

		protected override LUA.T Type { get { return LUA.T.THREAD; } }

		/// <summary>[-1, +0, v] Pops a thread from the top of the stack and creates a new reference. Raises a Lua error if the value isn't a thread. <paramref name="co"/> must be the value returned by <see cref="lua.newthread"/> or <see cref="lua.tothread"/> for this thread.</summary>
		public LuaThread(lua.State L, lua.State co, Lua interpreter) : base(L, interpreter)
		{
			Debug.Assert(consistent(L, co));
			_co = co;
		}
		/// <summary>[-0, +0, v] Creates a new reference to the thread at <paramref name="index"/>. Raises a Lua error if the value isn't a thread. <paramref name="co"/> must be the value returned by <see cref="lua.newthread"/> or <see cref="lua.tothread"/> for this thread.</summary>
		public LuaThread(lua.State L, lua.State co, Lua interpreter, int index) : base(L, interpreter, index)
		{
			Debug.Assert(consistent(L, co));
			_co = co;
		}
		/// <summary>[-1, +0, v] Pops a thread from the top of the stack and creates a new reference. Raises a Lua error if the value isn't a thread.</summary>
		public LuaThread(lua.State L, Lua interpreter) : this(L, lua.tothread(L, -1), interpreter) {}
		/// <summary>[-0, +0, v] Creates a new reference to the thread at <paramref name="index"/>. Raises a Lua error if the value isn't a thread.</summary>
		public LuaThread(lua.State L, Lua interpreter, int index) : this(L, lua.tothread(L, index), interpreter, index) {}
		lua.State _co;
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			_co = default(lua.State);
		}

		bool consistent(lua.State L, lua.State co)
		{
			Debug.Assert(luaclr.mainthread(co) != co, "The Lua coroutine library never gives Lua scripts a reference to the main thread. Are you sure it is safe and useful to do that?");
			rawpush(L);
			bool ret = lua.tothread(L, -1) == co;
			lua.pop(L, 1);
			return ret;
		}

		#endregion
	}
	
	/// <summary>Lua status codes.</summary>
	public enum LuaStatus
	{
		/// <summary>No errors. (0)</summary>
		Success = 0,
		/// <summary>LUA_YIELD: the thread is suspended</summary>
		Yield   = LUA.YIELD,
		/// <summary>LUA_ERRRUN: a runtime error.</summary>
		Run     = LUA.ERR.RUN,
		/// <summary>LUA_ERRSYNTAX: syntax error during pre-compilation.</summary>
		Syntax  = LUA.ERR.SYNTAX,
		/// <summary>LUA_ERRMEM: memory allocation error. For such errors, Lua does not call the error handler function.</summary>
		Mem     = LUA.ERR.MEM,
		/// <summary>LUA_ERRERR: error while running the error handler function.</summary>
		Err     = LUA.ERR.ERR,
		/// <summary>LUA_ERRFILE: cannot open/read the file.</summary>
		ErrFile = LUA.ERR.ERRFILE,
	}

	/// <summary>Current status of a coroutine. (see <see cref="LuaThread.Status"/>)</summary>
	public enum LuaCoStatus
	{
		/// <summary>The coroutine is running (that is, it called <see cref="LuaThread.Status"/>).</summary>
		Running,
		/// <summary>The coroutine is suspended in a call to "yield", or it has not started running yet.</summary>
		Suspended,
		/// <summary>The coroutine is active but not running (that is, it has resumed another coroutine).</summary>
		Normal,
		/// <summary>The coroutine has finished its body function, or if it has stopped with an error.</summary>
		Dead
	}
}