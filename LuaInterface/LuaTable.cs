﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using LuaInterface.Helpers;
using LuaInterface.LuaAPI;

namespace LuaInterface
{
	/// <summary>
	/// Wrapper class for a Lua table reference.
	/// Its implementation of IEnumerable is an alias to its <see cref="LuaTable.Pairs"/> property.
	/// </summary>
	public sealed class LuaTable : LuaBase, IEnumerable<KeyValuePair<object, object>>
	{
		#region Basics

		/// <summary>Returning an orphaned reference from a function that was called by Lua will automatically dispose that reference at a safe time.</summary>
		/// <remarks>Defaults to <see langword="false"/> except for references returned by <see cref="Lua.NewTable()"/>.</remarks>
		/// <seealso cref="Lua.NewTable()"/>
		public bool IsOrphaned;

		/// <summary>Makes a new reference the same table.</summary>
		public LuaTable NewReference()
		{
			var L = Owner._L;
			rawpush(L);
			try { return new LuaTable(L, Owner); } catch (InvalidCastException) { Dispose(); throw; }
		}

		/// <summary>The result of the Lua length operator ('#'). Note that this is the array length (string etc. keys aren't counted) and it doesn't work reliably on sparse arrays.</summary>
		/// <seealso href="http://www.lua.org/manual/5.1/manual.html#2.5.5"/>
		public int Length { get { return this.LongLength.ToInt32(); } }

		/// <summary>The result of the Lua length operator ('#'). Note that this is the array length (string etc. keys aren't counted) and it doesn't work reliably on sparse arrays.</summary>
		/// <seealso href="http://www.lua.org/manual/5.1/manual.html#2.5.5"/>
		public UIntPtr LongLength
		{
			get
			{
				var L = Owner._L;
				push(L);
				var len = lua.objlen(L, -1);
				lua.pop(L,1);
				return len;
			}
		}

		/// <summary>Counts the number of entries in the table.</summary>
		public int Count() { return checked((int)this.LongCount()); }
		/// <summary>Counts the number of entries in the table.</summary>
		public ulong LongCount()
		{
			var L = Owner._L;                         StackAssert.Start(L);
			ulong count = 0;
			push(L);
			lua.pushnil(L);
			while (lua.next(L, -2))
			{
				++count;
				lua.pop(L,1);
			}
			lua.pop(L,1);                             StackAssert.End();
			return count;
		}

		#endregion

		#region Raw Access

		/// <summary>Gets a numeric field of a table ignoring its metatable, if it exists</summary>
		public object RawGet(int field)
		{
			var L = Owner._L;                         StackAssert.Start(L);
			push(L);
			lua.rawgeti(L, -1, field);
			var obj = Owner.translator.getObject(L, -1);
			lua.pop(L,2);                             StackAssert.End();
			return obj;
		}

		/// <summary>Gets a string field of a table ignoring its metatable, if it exists</summary>
		public object RawGet(string field)
		{
			var L = Owner._L;                         StackAssert.Start(L);
			push(L);
			lua.pushstring(L, field);
			lua.rawget(L, -2);
			var obj = Owner.translator.getObject(L, -1);
			lua.pop(L,2);                             StackAssert.End();
			return obj;
		}

		/// <summary>Gets a field of a table ignoring its metatable, if it exists</summary>
		public object RawGet(object field)
		{
			var L = Owner._L;                         StackAssert.Start(L);
			push(L);
			Owner.translator.push(L, field);
			lua.rawget(L, -2);
			var obj = Owner.translator.getObject(L, -1);
			lua.pop(L,2);                             StackAssert.End();
			return obj;
		}


		/// <summary>Sets a numeric field of a table ignoring its metatable, if it exists</summary>
		public void RawSet(int field, object value)
		{
			var L = Owner._L;                         StackAssert.Start(L);
			push(L);
			Owner.translator.push(L, value);
			lua.rawseti(L, -2, field);
			lua.pop(L,1);                            StackAssert.End();
		}

		/// <summary>Sets a string field of a table ignoring its metatable, if it exists</summary>
		public void RawSet(string field, object value)
		{
			var L = Owner._L;                         StackAssert.Start(L);
			push(L);
			lua.pushstring(L, field);
			Owner.translator.push(L, value);
			lua.rawset(L, -3);
			lua.pop(L,1);                             StackAssert.End();
		}

		/// <summary>Sets a field of a table ignoring its metatable, if it exists</summary>
		public void RawSet(object field, object value)
		{
			var L = Owner._L;                         StackAssert.Start(L);
			push(L);
			Owner.translator.push(L, field);
			Owner.translator.push(L, value);
			lua.rawset(L, -3);
			lua.pop(L,1);                             StackAssert.End();
		}

		/// <summary>Looks up the field, ignoring metatables, and checks for a nil result.</summary>
		public bool RawContainsKey(object field) {
			return this.RawFieldType(field) != LuaType.Nil;
		}

		/// <summary>Looks up the field, ignoring metatables, and gets its Lua type.</summary>
		public LuaType RawFieldType(object field)
		{
			var L = Owner._L;                         StackAssert.Start(L);
			push(L);
			Owner.translator.push(L, field);
			lua.rawget(L,-2);
			var type = lua.type(L, -1);
			lua.pop(L,2);                             StackAssert.End();
			return type;
		}

		#endregion

		#region ForEach

		/// <summary>Iterates over the table without making a copy. Like "pairs()" in Lua, the iteration is unordered.</summary>
		/// <param name="body">The first parameter is a key and the second is a value.</param>
		/// <remarks>
		/// Due to the underlying Lua API's stack-based nature, it isn't safe to expose an IEnumerable iterator for tables without making a complete shallow copy or killing performance.
		/// An IEnumerable could be paused and later resumed inside a different callback, which would have a completely different Lua stack. Memory corruption would likely ensue.
		/// In other words, the C# stack must conceptually resemble the Lua stack.
		/// This function is the best compromise that can be offered. If you need IEnumerable, see <see cref="ToDict()"/> and <see cref="Pairs"/>.
		/// </remarks>
		public void ForEach(Action<object, object> body)
		{
			var L = Owner._L; var translator = Owner.translator;
			int oldTop = lua.gettop(L);
			push(L);
			try
			{
				lua.pushnil(L);
				while (lua.next(L, -2))
				{
					body(translator.getObject(L, -2), translator.getObject(L, -1));
					lua.pop(L, 1);
				}
			}
			finally { lua.settop(L, oldTop); }
		}

		/// <summary>
		/// Iterates over the table's integer keys without making a copy.
		/// Like "ipairs()" in Lua, the iteration is ordered starting from 1 and ends before the first empty index.
		/// </summary>
		/// <param name="body">The first parameter is a key and the second is a value.</param>
		public void ForEachI(Action<int, object> body)
		{
			var L = Owner._L; var translator = Owner.translator;
			int oldTop = lua.gettop(L);
			push(L);
			try
			{
				for (int i = 1; ; ++i)
				{
					lua.rawgeti(L, -1, i);
					object obj = translator.getObject(L, -1);
					if (obj == null) break;
					body(i, obj);
					lua.pop(L, 1);
				}
			}
			finally { lua.settop(L, oldTop); }
		}

		/// <summary>Iterates over the table's string keys without making a copy. Like "pairs()" in Lua, the iteration is unordered.</summary>
		/// <param name="body">The first parameter is a key and the second is a value.</param>
		public void ForEachS(Action<string, object> body)
		{
			var L = Owner._L; var translator = Owner.translator;
			int oldTop = lua.gettop(L);
			push(L);
			try
			{
				lua.pushnil(L);
				while (lua.next(L, -2))
				{
					if (lua.type(L, -2) == LuaType.String)
						body(lua.tostring(L, -2), translator.getObject(L, -1));
					lua.pop(L, 1);
				}
			}
			finally { lua.settop(L, oldTop); }
		}

		#endregion

		#region Copiers

		/// <summary>
		/// Shallow-copies the table to a new dictionary.
		/// Warning: The dictionary may contain <see cref="IDisposable"/> keys or values, all of which must be disposed.
		/// </summary>
		/// <seealso cref="LuaHelpers.DisposeAll(System.Collections.Generic.IEnumerable{System.Collections.Generic.KeyValuePair{object,object}})"/>
		public IDictionary<object, object> ToDict() { return ToDict(null); }

		/// <summary>
		/// Shallow-copies the table to a dictionary.
		/// Warning: The dictionary may contain <see cref="IDisposable"/> keys or values, all of which must be disposed.
		/// </summary>
		/// <param name="dict">If not null, the table data will be assigned to that dictionary.</param>
		/// <returns>A new IDictionary or <paramref name="dict"/>.</returns>
		/// <seealso cref="LuaHelpers.DisposeAll(System.Collections.Generic.IEnumerable{System.Collections.Generic.KeyValuePair{object,object}})"/>
		public IDictionary<object, object> ToDict(IDictionary<object, object> dict)
		{
			if (dict == null)
				dict = new Dictionary<object, object>();

			this.ForEach((k,v) =>
			{
				dict[k] = v;
			});
			return dict;
		}

		/// <summary>
		/// Shallow-copies the table to a new dictionary.
		/// Warning: The dictionary may contain <see cref="IDisposable"/> values, all of which must be disposed.
		/// </summary>
		/// <seealso cref="LuaHelpers.DisposeAll(System.Collections.Generic.IEnumerable{object})"/>
		public IDictionary<string, object> ToSDict() { return ToSDict(null); }

		/// <summary>
		/// Shallow-copies the table to a dictionary.
		/// Warning: The dictionary may contain <see cref="IDisposable"/> values, all of which must be disposed.
		/// </summary>
		/// <param name="dict">If not null, the table data will be assigned to that dictionary.</param>
		/// <returns>A new IDictionary or <paramref name="dict"/>.</returns>
		/// <seealso cref="LuaHelpers.DisposeAll(System.Collections.Generic.IEnumerable{object})"/>
		public IDictionary<string, object> ToSDict(IDictionary<string, object> dict)
		{
			if (dict == null)
				dict = new Dictionary<string, object>();

			this.ForEachS((k,v) =>
			{
				dict[k] = v;
			});
			return dict;
		}

		/// <summary>
		/// Shallow-copies the table's integer keyed entries to a new list.
		/// Warning: The list may contain <see cref="IDisposable"/> entries, all of which must be disposed.
		/// </summary>
		/// <seealso cref="LuaHelpers.DisposeAll(System.Collections.Generic.IEnumerable{object})"/>
		public IList<object> ToList() { return ToList(null); }

		/// <summary>
		/// Shallow-copies the table's integer keyed entries to a list.
		/// Warning: The list may contain <see cref="IDisposable"/> entries, all of which must be disposed.
		/// </summary>
		/// <param name="list">If not null, the table data will be assigned to that list.</param>
		/// <returns>A new IList or <paramref name="list"/>.</returns>
		/// <seealso cref="LuaHelpers.DisposeAll(System.Collections.Generic.IEnumerable{object})"/>
		public IList<object> ToList(IList<object> list)
		{
			if (list == null)
				list = new List<object>(this.Length);

			this.ForEachI((i,v) =>
			{
				list.Add(v);
			});
			return list;
		}

		/// <summary>
		/// Shallow-copies the table's integer keyed entries to a new array.
		/// Warning: The list may contain <see cref="IDisposable"/> entries, all of which must be disposed.
		/// </summary>
		public object[] ToArray() { return ToArray(null); }

		/// <summary>
		/// Shallow-copies the table's integer keyed entries to an array.
		/// Warning: The list may contain <see cref="IDisposable"/> entries, all of which must be disposed.
		/// </summary>
		/// <param name="array">If not null, the table data will be assigned to that array.</param>
		/// <returns>A new Object[] or <paramref name="array"/>.</returns>
		public object[] ToArray(object[] array)
		{
			if (array == null)
				array = new object[this.Length];
			else
				Debug.Assert(array.Length <= this.Length);

			this.ForEachI((i,o) =>
			{
				array[i-1] = o;
			});
			return array;
		}

		#endregion

		#region Iterators

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

		#endregion

		#region Legacy Support

		/// <summary>
		/// Shallow-copies the table to a new non-generic dictionary.
		/// Warning: The dictionary may contain <see cref="IDisposable"/> keys or values, all of which must be disposed.
		/// </summary>
		public System.Collections.IDictionary ToLegacyDict() { return ToLegacyDict(null); }

		/// <summary>
		/// Shallow-copies the table to a non-generic dictionary.
		/// Warning: The dictionary may contain <see cref="IDisposable"/> keys or values, all of which must be disposed.
		/// </summary>
		/// <param name="dict">If not null, the table data will be assigned to that dictionary.</param>
		/// <returns>A new IDictionary or <paramref name="dict"/>.</returns>
		public System.Collections.IDictionary ToLegacyDict(System.Collections.IDictionary dict)
		{
			if (dict == null)
				dict = new System.Collections.Specialized.ListDictionary();

			this.ForEach((k,v) =>
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

		#endregion


		#region Implementation

		/// <summary>[-1, +0, e] Pops a table from the top of the stack and creates a new reference. The value is discarded if a type exception is thrown.</summary>
		public LuaTable(lua.State L, Lua interpreter)
		: base(TryRef(L, interpreter, LuaType.Table), interpreter)
		{
		}

		public LuaTable(int reference, Lua interpreter)
		: base(reference, interpreter)
		{
			CheckType(LuaType.Table);
		}

		internal object rawgetFunction(string field)
		{
			object obj = this.RawGet(field);

			var f = obj as LuaCSFunction;
			if (f != null)
				return new LuaFunction(f, Owner);
			else
				return obj;
		}

		protected internal override void push(lua.State L)
		{
			Debug.Assert(L == Owner._L);
			luaL.getref(Owner._L, Reference);
			CheckType(Owner._L, LuaType.Table);
		}

		#endregion
	}
}