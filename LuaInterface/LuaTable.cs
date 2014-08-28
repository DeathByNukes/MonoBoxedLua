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
		: base(reference, interpreter)
		{
		}

		/// <summary>The result of the Lua length operator ('#'). Note that this is the array length (string etc. keys aren't counted) and it doesn't work reliably on sparse arrays.</summary>
		/// <seealso href="http://www.lua.org/manual/5.1/manual.html#2.5.5"/>
		public int Length
		{
			get { return _Interpreter.getLength(_Reference); }
		}

		/// <summary>Indexer for nested string fields of the table</summary>
		public object this[params string[] path]
		{
			get { return _Interpreter.getObject(_Reference, path); }
			set { _Interpreter.setObject(_Reference, path, value); }
		}
		/// <summary>Indexer for string fields of the table</summary>
		public object this[string field]
		{
			get { return _Interpreter.getObject(_Reference, field); }
			set { _Interpreter.setObject(_Reference, field, value); }
		}
		/// <summary>Indexer for numeric fields of the table</summary>
		public object this[object field]
		{
			get { return _Interpreter.getObject(_Reference, field); }
			set { _Interpreter.setObject(_Reference, field, value); }
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


		/// <summary>Gets a numeric field of a table ignoring its metatable, if it exists</summary>
		public object RawGet(int    field) { return _Interpreter.rawGetObject(_Reference, field); }

		/// <summary>Gets a string field of a table ignoring its metatable, if it exists</summary>
		public object RawGet(string field) { return _Interpreter.rawGetObject(_Reference, field); }

		/// <summary>Gets a field of a table ignoring its metatable, if it exists</summary>
		public object RawGet(object field) { return _Interpreter.rawGetObject(_Reference, field); }


		/// <summary>Sets a numeric field of a table ignoring its metatable, if it exists</summary>
		public void RawSet(int    field, object value) { _Interpreter.rawSetObject(_Reference, field, value); }

		/// <summary>Sets a string field of a table ignoring its metatable, if it exists</summary>
		public void RawSet(string field, object value) { _Interpreter.rawSetObject(_Reference, field, value); }

		/// <summary>Sets a field of a table ignoring its metatable, if it exists</summary>
		public void RawSet(object field, object value) { _Interpreter.rawSetObject(_Reference, field, value); }


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