using System;
using System.Diagnostics;
using LuaInterface.LuaAPI;

namespace LuaInterface
{
	public sealed class LuaFunction : LuaBase
	{
		internal readonly lua.CFunction function;

		/// <summary>[-1, +0, e] Pops a function from the top of the stack and creates a new reference. The value is discarded if a type exception is thrown.</summary>
		public LuaFunction(lua.State L, Lua interpreter)
		: base(TryRef(L, interpreter, LUA.T.FUNCTION), interpreter)
		{
		}
		public LuaFunction(lua.CFunction function, Lua interpreter)
		: base(LUA.NOREF, interpreter)
		{
			this.function = function;
		}

		/// <summary>Makes a new reference to the same function.</summary>
		public LuaFunction NewReference()
		{
			if (Reference == LUA.NOREF) return new LuaFunction(function, Owner);
			var L = Owner._L;
			rawpush(L);
			try { return new LuaFunction(L, Owner); } catch (InvalidCastException) { Dispose(); throw; }
		}


		protected internal override void push(lua.State L)
		{
			Debug.Assert(L == Owner._L);
			if (Reference == LUA.NOREF)
				Owner.translator.pushFunction(L, function);
			else
			{
				luaL.getref(L, Reference);
				CheckType(L, LUA.T.FUNCTION);
			}
		}

		protected override void rawpush(lua.State L)
		{
			Debug.Assert(L == Owner._L);
			if (Reference != LUA.NOREF)
				luaL.getref(Owner._L, Reference);
			else
				Owner.translator.pushFunction(Owner._L, function);
		}
		public override bool Equals(object o)
		{
			var l = o as LuaFunction;
			if (l == null) return false;
			if (this.Reference != LUA.NOREF && l.Reference != LUA.NOREF)
				return base.Equals(l);
			else
				return this.function == l.function;
		}

		public override int GetHashCode() {
			return Reference == LUA.NOREF
				? function.GetHashCode()
				: base.GetHashCode();
		}
	}

}