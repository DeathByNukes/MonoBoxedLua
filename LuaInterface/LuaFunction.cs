using System;

namespace LuaInterface
{
	public class LuaFunction : LuaBase
	{
		internal readonly LuaCSFunction function;

		public LuaFunction(int reference, Lua interpreter)
		: base(reference, interpreter)
		{
			this.function = null;
		}

		public LuaFunction(LuaCSFunction function, Lua interpreter)
		: base(LuaRefs.None, interpreter)
		{
			this.function = function;
		}

		/// <summary>Makes a new reference the same function.</summary>
		public LuaFunction NewReference() {
			return _Reference != LuaRefs.None
				? new LuaFunction(Owner.newReference(_Reference), Owner)
				: new LuaFunction(function, Owner);
		}


		/// <summary>Calls the function casting return values to the types in returnTypes</summary>
		internal object[] call(object[] args, Type[] returnTypes)
		{
			return Owner.callFunction(this, args, returnTypes);
		}
		/// <summary>Calls the function and returns its return values inside an array</summary>
		public object[] Call(params object[] args)
		{
			return Owner.callFunction(this, args);
		}
		/// <summary>Pushes the function into the Lua stack</summary>
		#if EXPOSE_STATE
		public
		#else
		internal
		#endif
		void push(IntPtr luaState)
		{
			if (_Reference != LuaRefs.None)
				LuaDLL.lua_getref(luaState, _Reference);
			else
				Owner.pushCSFunction(function);
		}
		public override string ToString()
		{
			return "function";
		}
		public override bool Equals(object o)
		{
			var l = o as LuaFunction;
			if (l == null) return false;
			if (this._Reference != LuaRefs.None && l._Reference != LuaRefs.None)
				return Owner.compareRef(l._Reference, this._Reference);
			else
				return this.function == l.function;
		}

		public override int GetHashCode()
		{
			if (_Reference != LuaRefs.None)
				// elisee: Used to return _Reference
				// which doesn't make sense as you can have different refs
				// to the same function
				return 0;
			else
				return function.GetHashCode();
		}
	}

}