using System;
using System.Diagnostics;

namespace LuaInterface
{
	public sealed class LuaFunction : LuaBase
	{
		internal readonly LuaCSFunction function;

		/// <summary>[-1, +0, e] Pops a function from the top of the stack and creates a new reference. The value is discarded if a type exception is thrown.</summary>
		public LuaFunction(IntPtr luaState, Lua interpreter)
		: base(TryRef(luaState, interpreter, LuaType.Function), interpreter)
		{
		}
		public LuaFunction(int reference, Lua interpreter)
		: base(reference, interpreter)
		{
			this.function = null;
			CheckType(LuaType.Function);
		}

		public LuaFunction(LuaCSFunction function, Lua interpreter)
		: base(LuaRefs.None, interpreter)
		{
			this.function = function;
		}

		/// <summary>Makes a new reference to the same function.</summary>
		public LuaFunction NewReference()
		{
			if (Reference == LuaRefs.None) return new LuaFunction(function, Owner);
			var L = Owner.luaState;
			rawpush(L);
			try { return new LuaFunction(L, Owner); } catch (InvalidCastException) { Dispose(); throw; }
		}


		protected internal override void push(IntPtr luaState)
		{
			Debug.Assert(luaState == Owner.luaState);
			if (Reference == LuaRefs.None)
				Owner.translator.pushFunction(luaState, function);
			else
			{
				LuaDLL.lua_getref(luaState, Reference);
				CheckType(luaState, LuaType.Function);
			}
		}

		protected override void rawpush(IntPtr luaState)
		{
			Debug.Assert(luaState == Owner.luaState);
			if (Reference != LuaRefs.None)
				LuaDLL.lua_getref(Owner.luaState, Reference);
			else
				Owner.translator.pushFunction(Owner.luaState, function);
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