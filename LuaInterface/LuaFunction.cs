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
		: base(TryRef(L, interpreter, LuaType.Function), interpreter)
		{
		}
		public LuaFunction(lua.CFunction function, Lua interpreter)
		: base(LuaRefs.None, interpreter)
		{
			this.function = function;
		}

		/// <summary>Makes a new reference to the same function.</summary>
		public LuaFunction NewReference()
		{
			if (Reference == LuaRefs.None) return new LuaFunction(function, Owner);
			var L = Owner._L;
			rawpush(L);
			try { return new LuaFunction(L, Owner); } catch (InvalidCastException) { Dispose(); throw; }
		}


		protected internal override void push(lua.State L)
		{
			Debug.Assert(L == Owner._L);
			if (Reference == LuaRefs.None)
				Owner.translator.pushFunction(L, function);
			else
			{
				luaL.getref(L, Reference);
				CheckType(L, LuaType.Function);
			}
		}

		protected override void rawpush(lua.State L)
		{
			Debug.Assert(L == Owner._L);
			if (Reference != LuaRefs.None)
				luaL.getref(Owner._L, Reference);
			else
				Owner.translator.pushFunction(Owner._L, function);
		}
		public override bool Equals(object o)
		{
			var l = o as LuaFunction;
			if (l == null) return false;
			if (this.Reference != LuaRefs.None && l.Reference != LuaRefs.None)
				return base.Equals(l);
			else
				return this.function == l.function;
		}

		public override int GetHashCode() {
			return Reference == LuaRefs.None
				? function.GetHashCode()
				: base.GetHashCode();
		}
	}

}