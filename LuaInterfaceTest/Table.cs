using System;
using System.Collections.Generic;
using System.Linq;
using LuaInterface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LuaInterfaceTest
{
	[TestClass] public class Table
	{
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
							six = 6,
							[1/2] = 'half', -- double key
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

		const int TestArrayEnd = 6;
		readonly IList<string> TestKeys = Array.AsReadOnly(new [] { "one", "two", "three", "four", "five", "six" });
		const double TestDouble = 1.0/2;
		const string TestDoubleValue = "half";

		readonly IList<string> TestEmptyKeys = Array.AsReadOnly(new [] { "foo", "bar", "baz", "qux" });
		const double TestEmptyDouble = 1.0 / 4;
		const string TestMetaRead = "metatable read";
		const string TestMetaWrite = "metatable write";


		[TestMethod] public void Get()
		{
			using (var lua = NewTest())
			using (var table = (LuaTable) lua.DoString("return newTestTable(true, false)", "@Table.cs.Get.lua")[0])
			{
				// - try reading normal values, which the metatable can't intercept -

				// array
				for (double i = 1; i <= TestArrayEnd; ++i)
					Assert.AreEqual(i, (double)table[i]);

				// associative array
				for (int i = 0; i < TestKeys.Count; ++i)
					Assert.AreEqual((double)(i + 1), (double)table[TestKeys[i]]);

				// double key
				Assert.AreEqual(TestDoubleValue, (string)table[TestDouble]);

				// - try reading missing values, which the metatable will intercept -

				for (double i = TestArrayEnd+1; i <= TestArrayEnd*2; ++i)
					Assert.AreEqual(TestMetaRead, table[i]);

				foreach (string k in TestEmptyKeys)
					Assert.AreEqual(TestMetaRead, table[k]);
			}
		}

		[TestMethod] public void Set()
		{
			using (var lua = NewTest())
			using (var table = (LuaTable)lua.DoString("return newTestTable(false, true)", "@Table.cs.Set.lua")[0])
			{
				// - try overwriting values, which the metatable can't intercept -

				// array
				for (double i = 1; i <= TestArrayEnd; ++i)
				{
					Assert.AreEqual(i, (double)table[i]);
					table[i] = 12;
					Assert.AreEqual(12.0, (double)table[i]);
				}

				// associative array
				for (int i = 0; i < TestKeys.Count; ++i)
				{
					var k = TestKeys[i];
					Assert.AreEqual((double)(i + 1), (double)table[k]);
					table[k] = 12;
					Assert.AreEqual(12.0, (double)table[k]);
				}

				// double key
				Assert.AreEqual(TestDoubleValue, (string)table[TestDouble]);
				table[TestDouble] = 12;
				Assert.AreEqual(12.0, (double)table[TestDouble]);

				// - try adding values, which the metatable will intercept -

				for (double i = TestArrayEnd + 1; i <= TestArrayEnd * 2; ++i)
				{
					Assert.AreEqual(null, table[i]);
					table[i] = 12;
					Assert.AreEqual(TestMetaWrite, table[i]);
				}

				foreach (string k in TestEmptyKeys)
				{
					Assert.AreEqual(null, table[k]);
					table[k] = 12;
					Assert.AreEqual(TestMetaWrite, table[k]);
				}
			}
		}

		[TestMethod] public void RawGet()
		{
			using (var lua = NewTest())
			using (var table = (LuaTable) lua.DoString("return newTestTable(true, false)", "@Table.cs.RawGet.lua")[0])
			{
				// - try reading normal values, which the metatable can't intercept either way -

				// array
				for (int i = 1; i <= TestArrayEnd; ++i)
					Assert.AreEqual((double)i, (double)table.RawGet(i));
				for (double i = 1; i <= TestArrayEnd; ++i)
					Assert.AreEqual(i, (double)table.RawGet(i));

				// associative array
				for (int i = 0; i < TestKeys.Count; ++i)
					Assert.AreEqual((double)(i + 1), (double)table.RawGet(TestKeys[i]));

				// double key
				Assert.AreEqual(TestDoubleValue, (string)table.RawGet(TestDouble));

				// - try reading missing values, which the metatable would intercept if we weren't using RawGet -

				for (int i = TestArrayEnd+1; i <= TestArrayEnd*2; ++i)
					Assert.IsNull(table.RawGet(i));

				foreach (string k in TestEmptyKeys)
					Assert.IsNull(table.RawGet(k));

				for (double i = TestArrayEnd+1; i <= TestArrayEnd*2; ++i)
					Assert.IsNull(table.RawGet(i));
			}
		}

		[TestMethod] public void RawSet()
		{
			using (var lua = NewTest())
			using (var table = (LuaTable)lua.DoString("return newTestTable(false, true)", "@Table.cs.RawSet.lua")[0])
			{
				// array
				for (int i = 1; i <= TestArrayEnd*2; ++i)
				{
					if (i <= TestArrayEnd)
						Assert.AreEqual((double)i, (double)table[i]);
					else
						Assert.IsNull(table[i]);
					table.RawSet(i, 12);
					Assert.AreEqual(12.0, (double)table[i]);
					table.RawSet((double)i, 1212);
					Assert.AreEqual(1212.0, (double)table[i]);
				}

				// associative array
				var keys = Enumerable.Concat(TestKeys, TestEmptyKeys).ToArray();
				for (int i = 0; i < keys.Length; ++i)
				{
					string k = keys[i];
					if (i < TestKeys.Count)
						Assert.AreEqual((double)(i + 1), (double)table[k]);
					else
						Assert.IsNull(table[k]);
					table.RawSet(k, 12);
					Assert.AreEqual(12.0, (double)table[k]);
				}

				// double key
				Assert.AreEqual(TestDoubleValue, (string)table[TestDouble]);
				table.RawSet(TestDouble, 12);
				Assert.AreEqual(12.0, (double)table[TestDouble]);

				Assert.IsNull(table[TestEmptyDouble]);
				table.RawSet(TestEmptyDouble, 12);
				Assert.AreEqual(12.0, table[TestEmptyDouble]);
			}
		}
	}
}
