using System;
using System.Collections.Generic;
using System.Diagnostics;
using LuaInterface.Helpers;

namespace LuaInterface
{
	/// <summary>
	/// Wrapper class for a Lua table reference.
	/// Its implementation of IEnumerable is an alias to its <see cref="LuaTable.Pairs"/> property.
	/// </summary>
	public class LuaTable : LuaBase, IEnumerable<KeyValuePair<object, object>>
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
			get { return Owner.getLength(_Reference); }
		}

		/// <summary>Counts the number of entries in the table.</summary>
		public int Count()
		{
			return Owner.getCount(this);
		}

		/// <summary>Indexer for nested string fields of the table</summary>
		public object this[params string[] path]
		{
			get { return Owner.getObject(_Reference, path); }
			set { Owner.setObject(_Reference, path, value); }
		}
		/// <summary>Indexer for string fields of the table</summary>
		public object this[string field]
		{
			get { return Owner.getObject(_Reference, field); }
			set { Owner.setObject(_Reference, field, value); }
		}
		/// <summary>Indexer for numeric fields of the table</summary>
		public object this[object field]
		{
			get { return Owner.getObject(_Reference, field); }
			set { Owner.setObject(_Reference, field, value); }
		}

		/// <summary>Iterates over the table without making a copy.</summary>
		/// <param name="body">The first parameter is a key and the second is a value.</param>
		/// <remarks>
		/// Due to the underlying Lua API's stack-based nature, it isn't safe to expose an IEnumerable iterator for tables without making a complete shallow copy or killing performance.
		/// An IEnumerable could be paused and later resumed inside a different callback, which would have a completely different Lua stack. Memory corruption would likely ensue.
		/// In other words, the C# stack must conceptually resemble the Lua stack.
		/// This function is the best compromise that can be offered. If you need IEnumerable, see <see cref="ToDict"/> and <see cref="Pairs"/>.
		/// </remarks>
		public void ForEach(Action<object, object> body) { Owner.TableForEach(this, body); }

		/// <summary>Iterates over the table's integer keys without making a copy.</summary>
		/// <param name="body">The first parameter is a key and the second is a value.</param>
		public void ForEachI(Action<int, object> body) { Owner.TableForEachI(this, body); }

		/// <summary>Iterates over the table's string keys without making a copy.</summary>
		/// <param name="body">The first parameter is a key and the second is a value.</param>
		public void ForEachS(Action<string, object> body) { Owner.TableForEachS(this, body); }

		/// <summary>
		/// Shallow-copies the table to a new dictionary.
		/// Warning: The dictionary may contain <see cref="IDisposable"/> keys or values, all of which must be disposed.
		/// </summary>
		/// <param name="dict">If not null, the table data will be assigned to that dictionary instead of a new one.</param>
		/// <returns>A new IDictionary or <paramref name="dict"/>.</returns>
		/// <seealso cref="LuaHelpers.DisposeAll(System.Collections.Generic.IEnumerable{System.Collections.Generic.KeyValuePair{object,object}})"/>
		public IDictionary<object, object> ToDict(IDictionary<object, object> dict = null)
		{
			if (dict == null)
				dict = new Dictionary<object, object>();

			Owner.TableForEach(this, delegate(object k, object v)
			{
				dict[k] = v;
			});
			return dict;
		}
		/// <summary>
		/// Shallow-copies the table to a new dictionary.
		/// Warning: The dictionary may contain <see cref="IDisposable"/> values, all of which must be disposed.
		/// </summary>
		/// <param name="dict">If not null, the table data will be assigned to that dictionary instead of a new one.</param>
		/// <returns>A new IDictionary or <paramref name="dict"/>.</returns>
		/// <seealso cref="LuaHelpers.DisposeAll(System.Collections.Generic.IEnumerable{object})"/>
		public IDictionary<string, object> ToSDict(IDictionary<string, object> dict = null)
		{
			if (dict == null)
				dict = new Dictionary<string, object>();

			Owner.TableForEachS(this, delegate(string k, object v)
			{
				dict[k] = v;
			});
			return dict;
		}
		/// <summary>
		/// Shallow-copies the table's integer keyed entries to a new list.
		/// Warning: The list may contain <see cref="IDisposable"/> entries, all of which must be disposed.
		/// </summary>
		/// <param name="list">If not null, the table data will be assigned to that list instead of a new one.</param>
		/// <returns>A new IList or <paramref name="list"/>.</returns>
		/// <seealso cref="LuaHelpers.DisposeAll(System.Collections.Generic.IEnumerable{object})"/>
		public IList<object> ToList(IList<object> list = null)
		{
			if (list == null)
				list = new List<object>(this.Length);

			Owner.TableForEachI(this, delegate(int i, object o)
			{
				list.Add(o);
			});
			return list;
		}
		/// <summary>
		/// Shallow-copies the table's integer keyed entries to a new array.
		/// Warning: The list may contain <see cref="IDisposable"/> entries, all of which must be disposed.
		/// </summary>
		/// <param name="array">If not null, the table data will be assigned to that list instead of a new one.</param>
		/// <returns>A new Object[] or <paramref name="array"/>.</returns>
		public object[] ToArray(object[] array = null)
		{
			if (array == null)
				array = new object[this.Length];
			else
				Debug.Assert(array.Length <= this.Length);

			Owner.TableForEachI(this, delegate(int i, object o)
			{
				array[i-1] = o;
			});
			return array;
		}

		/// <summary>
		/// An easy way to iterate over the table.
		/// All Lua values will be auto disposed when the iteration finishes.
		/// Note: At the start of enumeration the entire table will be shallow copied to a newly allocated buffer. If this doesn't result in acceptable performance, use <see cref="ForEach"/>.
		/// </summary>
		public IEnumerable<KeyValuePair<object, object>> Pairs { get
		{
			var dict = this.ToDict();
			try { foreach (var pair in dict) yield return pair; }
			finally { dict.DisposeAll(); } // doing this at the end rather than inside the loop because that could leak if they break the loop early
		}}
		/// <summary>
		/// An easy way to iterate over the table's integer-keyed entries.
		/// All Lua values will be auto disposed when the iteration finishes.
		/// Note: At the start of enumeration the entire table will be shallow copied to a newly allocated buffer. If this doesn't result in acceptable performance, use <see cref="ForEach"/>.
		/// </summary>
		public IEnumerable<object> IPairs { get
		{
			var list = this.ToList();
			try { foreach (var item in list) yield return item; }
			finally { list.DisposeAll(); }
		}}
		/// <summary>
		/// An easy way to iterate over the table's string-keyed entries.
		/// All Lua values will be auto disposed when the iteration finishes.
		/// Note: At the start of enumeration the entire table will be shallow copied to a newly allocated buffer. If this doesn't result in acceptable performance, use <see cref="ForEach"/>.
		/// </summary>
		public IEnumerable<KeyValuePair<string, object>> SPairs { get
		{
			var dict = this.ToSDict();
			try { foreach (var pair in dict) yield return pair; }
			finally { dict.Values.DisposeAll(); }
		}}

		/// <summary>Iterates over <see cref="Pairs"/>.</summary>
		IEnumerator<KeyValuePair<object, object>> IEnumerable<KeyValuePair<object, object>>.GetEnumerator() {
			return this.Pairs.GetEnumerator();
		}
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
			return this.Pairs.GetEnumerator();
		}

		/// <summary>
		/// Shallow-copies the table to a new non-generic dictionary.
		/// Warning: The dictionary may contain <see cref="IDisposable"/> keys or values, all of which must be disposed.
		/// </summary>
		/// <param name="dict">If not null, the table data will be assigned to that dictionary instead of a new one.</param>
		/// <returns>A new IDictionary or <paramref name="dict"/>.</returns>
		public System.Collections.IDictionary ToLegacyDict(System.Collections.IDictionary dict = null)
		{
			if (dict == null)
				dict = new System.Collections.Specialized.ListDictionary();

			Owner.TableForEach(this, delegate(object k, object v)
			{
				dict[k] = v;
			});
			return dict;
		}
		// this had to be removed so "var" can be used in foreach with the new GetEnumerator
		//[Obsolete("Use ToLegacyDict(), ToDict(), or ForEach() instead.")]
		//public System.Collections.IDictionaryEnumerator GetEnumerator() { return ToLegacyDict().GetEnumerator(); }
		[Obsolete("Use ToLegacyDict(), ToDict(), or ForEach() instead.")]
		public System.Collections.ICollection Keys   { get { return ToLegacyDict().Keys;   } }
		[Obsolete("Use ToLegacyDict(), ToDict(), or ForEach() instead.")]
		public System.Collections.ICollection Values { get { return ToLegacyDict().Values; } }

		/// <summary>Gets a numeric field of a table ignoring its metatable, if it exists</summary>
		public object RawGet(int    field) { return Owner.rawGetObject(_Reference, field); }

		/// <summary>Gets a string field of a table ignoring its metatable, if it exists</summary>
		public object RawGet(string field) { return Owner.rawGetObject(_Reference, field); }

		/// <summary>Gets a field of a table ignoring its metatable, if it exists</summary>
		public object RawGet(object field) { return Owner.rawGetObject(_Reference, field); }


		/// <summary>Sets a numeric field of a table ignoring its metatable, if it exists</summary>
		public void RawSet(int    field, object value) { Owner.rawSetObject(_Reference, field, value); }

		/// <summary>Sets a string field of a table ignoring its metatable, if it exists</summary>
		public void RawSet(string field, object value) { Owner.rawSetObject(_Reference, field, value); }

		/// <summary>Sets a field of a table ignoring its metatable, if it exists</summary>
		public void RawSet(object field, object value) { Owner.rawSetObject(_Reference, field, value); }


		internal object rawgetFunction(string field)
		{
			object obj = Owner.rawGetObject(_Reference, field);

			if (obj is LuaCSFunction)
				return new LuaFunction((LuaCSFunction)obj, Owner);
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