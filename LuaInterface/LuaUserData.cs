﻿using System;

namespace LuaInterface
{
	public class LuaUserData : LuaBase
	{
		public LuaUserData(int reference, Lua interpreter)
		{
			_Reference = reference;
			_Interpreter = interpreter;
		}
		/// <summary>Indexer for string fields of the userdata</summary>
		public object this[string field]
		{
			get
			{
				return _Interpreter.getObject(_Reference, field);
			}
			set
			{
				_Interpreter.setObject(_Reference, field, value);
			}
		}
		/// <summary>Indexer for numeric fields of the userdata</summary>
		public object this[object field]
		{
			get
			{
				return _Interpreter.getObject(_Reference, field);
			}
			set
			{
				_Interpreter.setObject(_Reference, field, value);
			}
		}
		/// <summary>Calls the userdata and returns its return values inside an array</summary>
		public object[] Call(params object[] args)
		{
			return _Interpreter.callFunction(this, args);
		}
		/// <summary>Pushes the userdata into the Lua stack</summary>
		internal void push(IntPtr luaState)
		{
			LuaDLL.lua_getref(luaState, _Reference);
		}
		public override string ToString()
		{
			return "userdata";
		}
	}
}