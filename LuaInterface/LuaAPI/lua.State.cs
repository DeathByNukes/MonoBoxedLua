using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace LuaInterface.LuaAPI
{
	public static unsafe partial class lua
	{
		/// <summary>Pointer to an opaque structure that keeps the whole state of a Lua interpreter. The Lua library is fully reentrant: it has no global variables. All information about a state is kept in that structure. This must be passed as the first argument to every function in the library, except to lua_newstate, which creates a Lua state from scratch.</summary>
		public partial struct State
		{
			// the only legal way to get a non-null instance of this struct is to call one of the Lua APIs that return a lua_State
			#pragma warning disable 649
			private readonly void* _L;
			#pragma warning restore 649

			public static State Null { [MethodImpl(INLINE)] get{ return new State(); } }
			public bool IsNull       { [MethodImpl(INLINE)] get{ return _L == null; } }
			/// <exception cref="NullReferenceException"></exception>
			public void NullCheck() { if (_L == null) throw new NullReferenceException("Attempted to use a null lua.State."); }
			/// <exception cref="ArgumentNullException"></exception>
			public void NullCheck(string param_name) { if (_L == null) throw new ArgumentNullException(param_name); }

			#region Equality

			[MethodImpl(INLINE)] public static bool operator ==(State left, State right) { return left._L == right._L; }
			[MethodImpl(INLINE)] public static bool operator !=(State left, State right) { return left._L != right._L; }

			public override bool Equals(object obj)
			{
				return obj is State && ((State)obj)._L == _L
					|| obj is IntPtr && ((IntPtr)obj).ToPointer() == _L;
			}
			public bool Equals(State other) { return _L == other._L; }

			public override int GetHashCode() {
				return sizeof(State) == sizeof(int)
					? (int)_L
					: ((long)_L).GetHashCode();
			}

			#endregion

			public override string ToString() { return new UIntPtr(_L).ToString(); }
		}

		// debug extensions
		[DebuggerDisplay("{_L} Top = {_DebuggerTop}")]
		public partial struct State : IEnumerable<StackIndex>
		{
			[DebuggerBrowsable(DebuggerBrowsableState.Never)]
			object _DebuggerTop { get { return _L == null ? null : (object)lua.gettop(this); } }

			IEnumerable<StackIndex> _enumerate(bool empty_slots)
			{
				NullCheck();

				{
					int i;
					for (i = 1; i <= lua.gettop(this); ++i)
						yield return new StackIndex(this, i);
					for (; i <= LUA.MINSTACK; ++i)
						yield return new StackIndex(this, i);
				}

				yield return new StackIndex(this, LUA.REGISTRYINDEX);
				bool in_func = luanet.infunction(this);
				if (in_func) // accessing the environment or upvalues from outside of a function causes an access violation
					yield return new StackIndex(this, LUA.ENVIRONINDEX);
				yield return new StackIndex(this, LUA.GLOBALSINDEX);

				if (in_func)
				for (int i = 1; i <= 256; ++i)
				{
					int index = lua.upvalueindex(i);
					if (lua.type(this, index) == LUA.T.NONE)
						break;
					yield return new StackIndex(this, index);
				}
			}
			public IEnumerator<StackIndex> GetEnumerator() { return _enumerate(false).GetEnumerator(); }
			IEnumerator IEnumerable.GetEnumerator()        { return _enumerate(false).GetEnumerator(); }

			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			List<StackIndex> _DebuggerContents { get { return _enumerate(true).ToList(); } }

			public int Count { get
			{
				NullCheck();
				int count = lua.gettop(this) + 2;
				if (luanet.infunction(this))
				{
					++count; // ENVIRONINDEX
					for (int i = 1; i <= 256; ++i)
					{
						if (lua.type(this, lua.upvalueindex(i)) == LUA.T.NONE)
							break;
						++count;
					}
				}
				return count;
			}}

			public StackIndex this[int index] { get { return new StackIndex(this, index); } }

			public int Length { get { NullCheck(); return lua.gettop(this); } }
		}
	}
}