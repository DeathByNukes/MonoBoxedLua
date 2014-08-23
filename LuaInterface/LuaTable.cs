using System;
using System.Collections;

namespace LuaInterface
{
	/// <summary>Wrapper class for Lua tables</summary>
	/// <remarks>
	/// Author: Fabio Mascarenhas
	/// Version: 1.0
	/// </remarks>
	public class LuaTable : LuaBase
	{
		public bool IsOrphaned;

		public LuaTable(int reference, Lua interpreter)
		{
			_Reference = reference;
			_Interpreter = interpreter;
		}

		/// <summary>Indexer for string fields of the table</summary>
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
		/// <summary>Indexer for numeric fields of the table</summary>
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


		public System.Collections.IDictionaryEnumerator GetEnumerator()
		{
			return _Interpreter.GetTableDict(this).GetEnumerator();
		}

		public ICollection Keys
		{
			get { return _Interpreter.GetTableDict(this).Keys; }
		}

		public ICollection Values
		{
			get { return _Interpreter.GetTableDict(this).Values; }
		}

		/// <summary>Gets an string fields of a table ignoring its metatable, if it exists</summary>
		internal object rawget(string field)
		{
			return _Interpreter.rawGetObject(_Reference, field);
		}

		internal object rawgetFunction(string field)
		{
			object obj = _Interpreter.rawGetObject(_Reference, field);

			if (obj is LuaCSFunction)
				return new LuaFunction((LuaCSFunction)obj, _Interpreter);
			else
				return obj;
		}

		/// <summary>Pushes this table into the Lua stack</summary>
		internal void push(IntPtr luaState)
		{
			LuaDLL.lua_getref(luaState, _Reference);
		}
		public override string ToString()
		{
			return "table";
		}
	}
}