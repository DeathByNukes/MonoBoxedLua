using System;
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

		/// <summary>Generates a brief human readable summary of the table's contents.</summary>
		public string Summarize() { return this.Summarize(256); }
		/// <summary>Generates a human readable summary of the table's contents.</summary>
		/// <param name="strings_limit">When the list of string keys grows larger than this it will stop adding more. This limit applies to the number of characters, not the number of keys.</param>
		public string Summarize(int strings_limit)
		{
			var L = Owner._L;
			push(L);
			var ret = luanet.summarizetable(L, -1, strings_limit);
			lua.pop(L,1);
			return ret;
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

		/// <summary><see cref="RawGet(object)"/> alternative that only fetches plain Lua value types and strings.</summary>
		public LuaValue RawGetValue(LuaValue field)
		{
			field.VerifySupport("field");
			var L = Owner._L;                         StackAssert.Start(L);
			push(L);
			field.push(L);
			lua.rawget(L, -2);
			var obj = LuaValue.read(L, -1, false);
			lua.pop(L,2);                             StackAssert.End();
			return obj;
		}

		/// <summary><see cref="RawGet(int)"/> alternative that only fetches plain Lua value types and strings.</summary>
		public LuaValue RawGetValue(int field)
		{
			var L = Owner._L;                         StackAssert.Start(L);
			push(L);
			lua.rawgeti(L, -1, field);
			var obj = LuaValue.read(L, -1, false);
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
			return (LuaType) type;
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

		/// <summary>
		/// Iterates over the table's integer keys with string values without making a copy.
		/// Like "ipairs()" in Lua, the iteration is ordered starting from 1 and ends before the first empty index.
		/// </summary>
		/// <param name="body">The first parameter is a key and the second is a value.</param>
		public void ForEachIV(Action<int, LuaValue> body)
		{
			var L = Owner._L;
			int oldTop = lua.gettop(L);
			push(L);
			try
			{
				for (int i = 1; ; ++i)
				{
					lua.rawgeti(L, -1, i);
					switch (lua.type(L, -1))
					{
					case LUA.T.NIL:
						return;
					case LUA.T.STRING:
						body(i, LuaValue.read(L, -1, false));
						break;
					}
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
					if (lua.type(L, -2) == LUA.T.STRING)
						body(lua.tostring(L, -2), translator.getObject(L, -1));
					lua.pop(L, 1);
				}
			}
			finally { lua.settop(L, oldTop); }
		}

		/// <summary>Iterates over the table's string keys with string values without making a copy. Like "pairs()" in Lua, the iteration is unordered.</summary>
		/// <param name="body">The first parameter is a key and the second is a value.</param>
		public void ForEachSV(Action<string, LuaValue> body)
		{
			var L = Owner._L;
			int oldTop = lua.gettop(L);
			push(L);
			try
			{
				lua.pushnil(L);
				while (lua.next(L, -2))
				{
					if (lua.type(L, -2) == LUA.T.STRING && lua.type(L, -1) == LUA.T.STRING)
						body(lua.tostring(L, -2), LuaValue.read(L, -1, false));
					lua.pop(L, 1);
				}
			}
			finally { lua.settop(L, oldTop); }
		}

		/// <summary>Iterates over the table without making a copy. Like "pairs()" in Lua, the iteration is unordered.</summary>
		/// <param name="body">The first parameter is a key and the second is a value.</param>
		public void ForEachV(Action<LuaValue, object> body)
		{
			var L = Owner._L; var translator = Owner.translator;
			int oldTop = lua.gettop(L);
			push(L);
			try
			{
				lua.pushnil(L);
				while (lua.next(L, -2))
				{
					body(LuaValue.read(L, -2, false), translator.getObject(L, -1));
					lua.pop(L, 1);
				}
			}
			finally { lua.settop(L, oldTop); }
		}

		/// <summary>Iterates over the table without making a copy. Like "pairs()" in Lua, the iteration is unordered.</summary>
		/// <param name="body">The first parameter is a key and the second is a value.</param>
		public void ForEachVV(Action<LuaValue, LuaValue> body)
		{
			var L = Owner._L;
			int oldTop = lua.gettop(L);
			push(L);
			try
			{
				lua.pushnil(L);
				while (lua.next(L, -2))
				{
					body(LuaValue.read(L, -2, false), LuaValue.read(L, -1, false));
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
		public Dictionary<object, object> ToDict() { return (Dictionary<object, object>) ToDict(null); }

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
		public Dictionary<string, object> ToSDict() { return (Dictionary<string, object>) ToSDict(null); }

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
		/// Shallow-copies the table to a new dictionary.
		/// Warning: The dictionary may contain <see cref="IDisposable"/> values, all of which must be disposed.
		/// </summary>
		/// <seealso cref="LuaHelpers.DisposeAll(System.Collections.Generic.IEnumerable{object})"/>
		public Dictionary<string, LuaValue> ToSVDict() { return (Dictionary<string, LuaValue>) ToSVDict(null); }

		/// <summary>
		/// Shallow-copies the table to a dictionary.
		/// Warning: The dictionary may contain <see cref="IDisposable"/> values, all of which must be disposed.
		/// </summary>
		/// <param name="dict">If not null, the table data will be assigned to that dictionary.</param>
		/// <returns>A new IDictionary or <paramref name="dict"/>.</returns>
		/// <seealso cref="LuaHelpers.DisposeAll(System.Collections.Generic.IEnumerable{object})"/>
		public IDictionary<string, LuaValue> ToSVDict(IDictionary<string, LuaValue> dict)
		{
			if (dict == null)
				dict = new Dictionary<string, LuaValue>();

			this.ForEachSV((k,v) =>
			{
				dict[k] = v;
			});
			return dict;
		}

		/// <summary>Shallow-copies the table to a new dictionary, omitting keys not supported by <see cref="LuaValue"/>.</summary>
		public Dictionary<LuaValue, object> ToVDict() { return (Dictionary<LuaValue, object>) ToVDict(null); }
		
		/// <summary>Shallow-copies the table to a new dictionary, omitting keys not supported by <see cref="LuaValue"/>.</summary>
		/// <param name="dict">If not null, the table data will be assigned to that dictionary.</param>
		/// <returns>A new IDictionary or <paramref name="dict"/>.</returns>
		public IDictionary<LuaValue, object> ToVDict(IDictionary<LuaValue, object> dict)
		{
			if (dict == null)
				dict = new Dictionary<LuaValue, object>(this.Count()); // pre-count in this one because LuaValue implies the user wants to avoid heap allocations

			this.ForEachV((k,v) =>
			{
				if (k.IsSupported)
					dict[k] = v;
			});
			return dict;
		}

		/// <summary>Shallow-copies the table to a new dictionary, omitting keys not supported by <see cref="LuaValue"/>.</summary>
		public Dictionary<LuaValue, LuaValue> ToVVDict() { return (Dictionary<LuaValue, LuaValue>) ToVVDict(null); }
		
		/// <summary>Shallow-copies the table to a new dictionary, omitting keys not supported by <see cref="LuaValue"/>.</summary>
		/// <param name="dict">If not null, the table data will be assigned to that dictionary.</param>
		/// <returns>A new IDictionary or <paramref name="dict"/>.</returns>
		public IDictionary<LuaValue, LuaValue> ToVVDict(IDictionary<LuaValue, LuaValue> dict)
		{
			if (dict == null)
				dict = new Dictionary<LuaValue, LuaValue>(this.Count()); // pre-count in this one because LuaValue implies the user wants to avoid heap allocations

			this.ForEachVV((k,v) =>
			{
				if (k.IsSupported)
					dict[k] = v;
			});
			return dict;
		}

		/// <summary>
		/// Shallow-copies the table's integer keyed entries to a new list.
		/// Warning: The list may contain <see cref="IDisposable"/> entries, all of which must be disposed.
		/// </summary>
		/// <seealso cref="LuaHelpers.DisposeAll(System.Collections.Generic.IEnumerable{object})"/>
		public List<object> ToList() { return (List<object>) ToList(null); }

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
		/// Shallow-copies the table's integer keyed entries to a new list.
		/// Warning: The list may contain <see cref="IDisposable"/> entries, all of which must be disposed.
		/// </summary>
		/// <seealso cref="LuaHelpers.DisposeAll(System.Collections.Generic.IEnumerable{object})"/>
		public List<LuaValue> ToVList() { return (List<LuaValue>) ToVList(null); }

		/// <summary>
		/// Shallow-copies the table's integer keyed entries to a list.
		/// Warning: The list may contain <see cref="IDisposable"/> entries, all of which must be disposed.
		/// </summary>
		/// <param name="list">If not null, the table data will be assigned to that list.</param>
		/// <returns>A new IList or <paramref name="list"/>.</returns>
		/// <seealso cref="LuaHelpers.DisposeAll(System.Collections.Generic.IEnumerable{object})"/>
		public IList<LuaValue> ToVList(IList<LuaValue> list)
		{
			if (list == null)
				list = new List<LuaValue>(this.Length);

			this.ForEachIV((i,v) =>
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

		/// <summary>Shallow-copies the table's integer keyed entries to a new array.</summary>
		public LuaValue[] ToVArray() { return ToVArray(null); }
		
		/// <summary>Shallow-copies the table's integer keyed entries to a new array.</summary>
		/// <param name="array">If not null, the table data will be assigned to that array.</param>
		/// <returns>A new LuaValue[] or <paramref name="array"/>.</returns>
		public LuaValue[] ToVArray(LuaValue[] array)
		{
			if (array == null)
				array = new LuaValue[this.Length];
			else
				Debug.Assert(array.Length <= this.Length);

			this.ForEachIV((i,o) =>
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
		: base(TryRef(L, interpreter, LUA.T.TABLE), interpreter)
		{
		}

		public LuaTable(int reference, Lua interpreter)
		: base(reference, interpreter)
		{
			CheckType(LUA.T.TABLE);
		}

		internal object rawgetFunction(string field)
		{
			object obj = this.RawGet(field);

			var f = obj as lua.CFunction;
			if (f != null)
				return new LuaFunction(f, Owner);
			else
				return obj;
		}

		protected internal override void push(lua.State L)
		{
			Debug.Assert(L == Owner._L);
			luaL.getref(Owner._L, Reference);
			CheckType(Owner._L, LUA.T.TABLE);
		}

		#endregion
	}
}