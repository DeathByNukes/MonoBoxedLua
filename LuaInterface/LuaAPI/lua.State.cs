using System;
using System.Runtime.CompilerServices;

namespace LuaInterface.LuaAPI
{
	public static unsafe partial class lua
	{
		/// <summary>Pointer to an opaque structure that keeps the whole state of a Lua interpreter. The Lua library is fully reentrant: it has no global variables. All information about a state is kept in that structure. This must be passed as the first argument to every function in the library, except to lua_newstate, which creates a Lua state from scratch.</summary>
		public struct State
		{
			// the only legal way to get a non-null instance of this struct is to call one of the Lua APIs that return a lua_State
			#pragma warning disable 649
			private readonly void* _L;
			#pragma warning restore 649

			public static State Null { [MethodImpl(INLINE)] get{ return new State(); } }
			public bool IsNull       { [MethodImpl(INLINE)] get{ return _L == null; } }

			#region Equality

			[MethodImpl(INLINE)] public static bool operator ==(State left, State right) { return left._L == right._L; }
			[MethodImpl(INLINE)] public static bool operator !=(State left, State right) { return left._L != right._L; }

			public override bool Equals(object obj)
			{
				if (ReferenceEquals(null, obj)) return false;
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
		}
	}
}
