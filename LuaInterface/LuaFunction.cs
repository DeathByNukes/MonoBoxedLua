using System;

namespace LuaInterface
{
	public class LuaFunction : LuaBase
	{
		internal LuaCSFunction function;

		public LuaFunction(int reference, Lua interpreter)
		{
			_Reference = reference;
			this.function = null;
			_Interpreter = interpreter;
		}

		public LuaFunction(LuaCSFunction function, Lua interpreter)
		{
			_Reference = 0;
			this.function = function;
			_Interpreter = interpreter;
		}


		/*
		 * Calls the function casting return values to the types
		 * in returnTypes
		 */
		internal object[] call(object[] args, Type[] returnTypes)
		{
			return _Interpreter.callFunction(this, args, returnTypes);
		}
		/*
		 * Calls the function and returns its return values inside
		 * an array
		 */
		public object[] Call(params object[] args)
		{
			return _Interpreter.callFunction(this, args);
		}
		/*
		 * Pushes the function into the Lua stack
		 */
		internal void push(IntPtr luaState)
		{
			if (_Reference != 0)
				LuaDLL.lua_getref(luaState, _Reference);
			else
				_Interpreter.pushCSFunction(function);
		}
		public override string ToString()
		{
			return "function";
		}
		public override bool Equals(object o)
		{
			if (o is LuaFunction)
			{
				LuaFunction l = (LuaFunction)o;
				if (this._Reference != 0 && l._Reference != 0)
					return _Interpreter.compareRef(l._Reference, this._Reference);
				else
					return this.function == l.function;
			}
			else return false;
		}

		public override int GetHashCode()
		{
			if (_Reference != 0)
				// elisee: Used to return _Reference
				// which doesn't make sense as you can have different refs
				// to the same function
				return 0;
			else
				return function.GetHashCode();
		}
	}

}
