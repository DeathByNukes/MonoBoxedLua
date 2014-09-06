using System;
using System.Collections.Generic;
using System.Linq;
using LuaInterface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LuaInterfaceTest
{
	[TestClass] public class Table
	{
		[TestCleanup] public void Cleanup()
		{
			GcUtil.GcAll();
		}
		private Lua NewTest()
		{
			var lua = new Lua();
			try
			{
				lua.DoString(@"
					function newTestTable(meta_read, meta_write)
						local meta = {}
						if meta_read then meta.__index = function(t,k) return 'metatable read' end end
						if meta_write then meta.__newindex = function(t,k,v) rawset(t,k, 'metatable write') end end
						return setmetatable({
							1, 2, 3, 4, 5, 6, -- array
							one = 1, -- associative array
							two = 2,
							three = 3,
							four = 4,
							five = 5,
							SIX = 6,
							six = 'wrong',
							[1/2] = 'half', -- double key
							nested = {a={b= setmetatable({c='hi'},meta) }},
						}, meta  )
					end
				", "@Table.cs.NewTest.lua"); // chunk names beginning with @ are filenames
			}
			catch (Exception)
			{
				lua.Dispose();
				throw;
			}
			return lua;
		}

		const int _ArrayEnd = 6;
		readonly IList<string> _Keys = Array.AsReadOnly(new [] { "one", "two", "three", "four", "five", "SIX" });
		const double _Double = 1.0/2;
		const string _DoubleValue = "half";
		string[] _Nested { get { return "nested.a.b.c".Split('.'); } }
		const string _NestedValue = "hi";

		readonly IList<string> _EmptyKeys = Array.AsReadOnly(new [] { "foo", "bar", "baz", "qux", "ONE", "One" });
		const double _EmptyDouble = 1.0 / 4;
		string[] _EmptyNested { get { return "nested.a.b.C".Split('.'); } }
		const string _MetaRead = "metatable read";
		const string _MetaWrite = "metatable write";


		[TestMethod] public void Get()
		{
			using (var lua = NewTest())
			using (var table = (LuaTable) lua.DoString("return newTestTable(true, false)", "@Table.cs.Get.lua")[0])
			{
				// - try reading normal values, which the metatable can't intercept -

				Assert.AreEqual(_ArrayEnd, table.Length);

				// array
				for (double i = 1; i <= _ArrayEnd; ++i)
					Assert.AreEqual(i, (double)table[i]);

				// associative array
				for (int i = 0; i < _Keys.Count; ++i)
					Assert.AreEqual((double)(i + 1), (double)table[_Keys[i]]);

				// double key
				Assert.AreEqual(_DoubleValue, (string)table[_Double]);

				// nested
				Assert.AreEqual(_NestedValue, (string)table[_Nested]);

				// - try reading missing values, which the metatable will intercept -

				for (double i = _ArrayEnd+1; i <= _ArrayEnd*2; ++i)
					Assert.AreEqual(_MetaRead, table[i]);

				foreach (string k in _EmptyKeys)
					Assert.AreEqual(_MetaRead, table[k]);

				Assert.AreEqual(_MetaRead, (string)table[_EmptyNested]);
			}
		}

		[TestMethod] public void Set()
		{
			using (var lua = NewTest())
			using (var table = (LuaTable)lua.DoString("return newTestTable(false, true)", "@Table.cs.Set.lua")[0])
			{
				// - try overwriting values, which the metatable can't intercept -

				// array
				for (double i = 1; i <= _ArrayEnd; ++i)
				{
					Assert.AreEqual(i, (double)table[i]);
					table[i] = 12;
					Assert.AreEqual(12.0, (double)table[i]);
				}

				// associative array
				for (int i = 0; i < _Keys.Count; ++i)
				{
					var k = _Keys[i];
					Assert.AreEqual((double)(i + 1), (double)table[k]);
					table[k] = 12;
					Assert.AreEqual(12.0, (double)table[k]);
				}

				// double key
				Assert.AreEqual(_DoubleValue, (string)table[_Double]);
				table[_Double] = 12;
				Assert.AreEqual(12.0, (double)table[_Double]);

				// nested
				Assert.AreEqual(_NestedValue, (string)table[_Nested]);
				table[_Nested] = 12;
				Assert.AreEqual(12.0, (double)table[_Nested]);

				// - try adding values, which the metatable will intercept -

				for (double i = _ArrayEnd + 1; i <= _ArrayEnd * 2; ++i)
				{
					Assert.AreEqual(null, table[i]);
					table[i] = 12;
					Assert.AreEqual(_MetaWrite, table[i]);
				}

				foreach (string k in _EmptyKeys)
				{
					Assert.AreEqual(null, table[k]);
					table[k] = 12;
					Assert.AreEqual(_MetaWrite, table[k]);
				}

				Assert.AreEqual(null, table[_EmptyNested]);
				table[_EmptyNested] = 12;
				Assert.AreEqual(_MetaWrite, (string)table[_EmptyNested]);
			}
		}

		[TestMethod] public void RawGet()
		{
			using (var lua = NewTest())
			using (var table = (LuaTable) lua.DoString("return newTestTable(true, false)", "@Table.cs.RawGet.lua")[0])
			{
				// - try reading normal values, which the metatable can't intercept either way -

				// array
				for (int i = 1; i <= _ArrayEnd; ++i)
					Assert.AreEqual((double)i, (double)table.RawGet(i));
				for (double i = 1; i <= _ArrayEnd; ++i)
					Assert.AreEqual(i, (double)table.RawGet(i));

				// associative array
				for (int i = 0; i < _Keys.Count; ++i)
					Assert.AreEqual((double)(i + 1), (double)table.RawGet(_Keys[i]));

				// double key
				Assert.AreEqual(_DoubleValue, (string)table.RawGet(_Double));

				// - try reading missing values, which the metatable would intercept if we weren't using RawGet -

				for (int i = _ArrayEnd+1; i <= _ArrayEnd*2; ++i)
					Assert.IsNull(table.RawGet(i));

				foreach (string k in _EmptyKeys)
					Assert.IsNull(table.RawGet(k));

				for (double i = _ArrayEnd+1; i <= _ArrayEnd*2; ++i)
					Assert.IsNull(table.RawGet(i));
			}
		}

		[TestMethod] public void RawSet()
		{
			using (var lua = NewTest())
			using (var table = (LuaTable)lua.DoString("return newTestTable(false, true)", "@Table.cs.RawSet.lua")[0])
			{
				// array
				for (int i = 1; i <= _ArrayEnd*2; ++i)
				{
					if (i <= _ArrayEnd)
						Assert.AreEqual((double)i, (double)table[i]);
					else
						Assert.IsNull(table[i]);
					table.RawSet(i, 12);
					Assert.AreEqual(12.0, (double)table[i]);
					table.RawSet((double)i, 1212);
					Assert.AreEqual(1212.0, (double)table[i]);
				}

				// associative array
				var keys = Enumerable.Concat(_Keys, _EmptyKeys).ToArray();
				for (int i = 0; i < keys.Length; ++i)
				{
					string k = keys[i];
					if (i < _Keys.Count)
						Assert.AreEqual((double)(i + 1), (double)table[k]);
					else
						Assert.IsNull(table[k]);
					table.RawSet(k, 12);
					Assert.AreEqual(12.0, (double)table[k]);
				}

				// double key
				Assert.AreEqual(_DoubleValue, (string)table[_Double]);
				table.RawSet(_Double, 12);
				Assert.AreEqual(12.0, (double)table[_Double]);

				Assert.IsNull(table[_EmptyDouble]);
				table.RawSet(_EmptyDouble, 12);
				Assert.AreEqual(12.0, table[_EmptyDouble]);
			}
		}
	}
}
