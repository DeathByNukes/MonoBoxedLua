using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using LuaInterface;
using LuaInterface.Helpers;
using LuaInterface.LuaAPI;

namespace LuaInterfaceTest
{
	#if NUNIT
	using NUnit.Framework;
	using TestClassAttribute = NUnit.Framework.TestFixtureAttribute;
	using TestMethodAttribute = NUnit.Framework.TestAttribute;
	using TestCleanupAttribute = NUnit.Framework.TearDownAttribute;
	#else
	using Microsoft.VisualStudio.TestTools.UnitTesting;
	#endif

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
				Assert.AreEqual(_ArrayEnd, table.Length);
				Assert.AreEqual(15, table.Count());

				// - try reading normal values, which the metatable can't intercept -

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
		[TestMethod] public void GetValue()
		{
			using (var lua = NewTest())
			using (var table = (LuaTable) lua.DoString("return newTestTable(true, false)", "@Table.cs.GetValue.lua")[0])
			{
				Assert.AreEqual(_ArrayEnd, table.Length);
				Assert.AreEqual(15, table.Count());

				// - try reading normal values, which the metatable can't intercept -

				// array
				for (double i = 1; i <= _ArrayEnd; ++i)
					Assert.AreEqual(i, (double)table.GetValue(i));

				// associative array
				for (int i = 0; i < _Keys.Count; ++i)
					Assert.AreEqual((double)(i + 1), (double)table.GetValue(_Keys[i]));

				// double key
				Assert.AreEqual(_DoubleValue, (string)table.GetValue(_Double));

				// - try reading missing values, which the metatable will intercept -

				for (double i = _ArrayEnd+1; i <= _ArrayEnd*2; ++i)
					Assert.AreEqual(_MetaRead, (string)table.GetValue(i));

				foreach (string k in _EmptyKeys)
					Assert.AreEqual(_MetaRead, (string)table.GetValue(k));
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

		[TestMethod] public void RawGetValue()
		{
			using (var lua = NewTest())
			using (var table = (LuaTable) lua.DoString("return newTestTable(true, false)", "@Table.cs.RawGetValue.lua")[0])
			{
				// - try reading normal values, which the metatable can't intercept either way -

				// array
				for (int i = 1; i <= _ArrayEnd; ++i)
					Assert.AreEqual((double)i, (double)table.RawGetValue(i));
				for (double i = 1; i <= _ArrayEnd; ++i)
					Assert.AreEqual(i, (double)table.RawGetValue(i));

				// associative array
				for (int i = 0; i < _Keys.Count; ++i)
					Assert.AreEqual((double)(i + 1), (double)table.RawGetValue(_Keys[i]));

				// double key
				Assert.AreEqual(_DoubleValue, (string)table.RawGetValue(_Double));

				// - try reading missing values, which the metatable would intercept if we weren't using RawGet -

				for (int i = _ArrayEnd+1; i <= _ArrayEnd*2; ++i)
					Assert.AreEqual(LuaType.Nil, table.RawGetValue(i).Type);

				foreach (string k in _EmptyKeys)
					Assert.AreEqual(LuaType.Nil, table.RawGetValue(k).Type);

				for (double i = _ArrayEnd+1; i <= _ArrayEnd*2; ++i)
					Assert.AreEqual(LuaType.Nil, table.RawGetValue(i).Type);
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

		static string TestFormat(object o)
		{
			if (o is string) return string.Format("'{0}'", o);
			if (o is LuaTable) return "table";
			return o.ToString();
		}
		private static string TestFormatPair<K,V>(KeyValuePair<K,V> pair)
		{
			return string.Format("{0}={1}", TestFormat(pair.Key), TestFormat(pair.Value));
		}

		private static string TestFormat(IEnumerable<object> source)
		{
			return string.Join(", ", source.Select<object, string>(TestFormat).ToArray());
		}
		private static string TestFormatPair<K,V>(IEnumerable<KeyValuePair<K,V>> source)
		{
			return string.Join(", ", source.Select<KeyValuePair<K,V>, string>(TestFormatPair).ToArray());
		}



		[TestMethod] public void ToDict()
		{
			using (var lua = new Lua())
			using (var table = (LuaTable)lua.DoString("return {'1', 2, 'three', nil, 5, 2*3, seven=7, eight={8}}", "@Table.cs.ToDict.lua")[0])
			{
				// just a quick hardcoded test based on its own output
				const string expected = "1='1', 2=2, 3='three', 5=5, 6=6, 'eight'=table, 'seven'=7";

				var dict = table.ToDict();
				using (dict.DeferDisposeAll())
					Assert.AreEqual(expected, TestFormatPair(dict));

				Assert.AreEqual(expected, TestFormatPair(table.Pairs));

				Assert.AreEqual(expected, TestFormatPair(table));

				using (var t2 = table.NewReference())
					Assert.AreEqual(expected, TestFormatPair(t2));
				Assert.AreEqual(expected, TestFormatPair(table));
			}
		}

		[TestMethod] public void ToSDict()
		{
			using (var lua = new Lua())
			using (var table = (LuaTable)lua.DoString("return {'1', 2, 'three', nil, 5, 2*3, seven=7, eight={8}}", "@Table.cs.ToDict.lua")[0])
			{
				const string expected = "'eight'=table, 'seven'=7";

				var dict = table.ToSDict();
				using (dict.Values.DeferDisposeAll())
					Assert.AreEqual(expected, TestFormatPair(dict));

				Assert.AreEqual(expected, TestFormatPair(table.SPairs));
			}
		}

		[TestMethod] public void ToList()
		{
			using (var lua = new Lua())
			using (var table = (LuaTable)lua.DoString("return {'1', 2, 'three', 4, 5, 2*3, {7}}", "@Table.cs.ToList.lua")[0])
			{
				const string expected = "'1', 2, 'three', 4, 5, 6, table";

				var list = table.ToList();
				using (list.DeferDisposeAll())
					Assert.AreEqual(expected, TestFormat(list));
				list = null;

				var array = table.ToArray();
				using (array.DeferDisposeAll())
					Assert.AreEqual(expected, TestFormat(array));
				array = null;

				Assert.AreEqual(expected, TestFormat(table.IPairs));
			}
		}

		[TestMethod] public void Summarize()
		{
			using (var l = NewTest())
			{
				var L = luanet.getstate(l);
				Assert.AreEqual(LUA.ERR.Success, luaL.dostring(L, @"
return _G,newTestTable(nil,nil),{
	'1', 2, 'three', 4, 5, 2*3, {7}
},{
	'1', 2, 'three', nil, 5, 2*3, seven=7, eight={8}
},{
	aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = 1,
	bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = function() return 1 end,
	cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc = {},
	['ddddddddddddddddddddddddddddddddddddddddd)(*&^(*&%^ dddddddddddddddddddddddddddddddddddddddddddddddddd'] = {},
},{
	true,true,true,true,
},{
	[true] = false,
	[false] = true,
},{
	true,true,true,true,
	aaaaaa = 1,
	bbbbbb = function() return 1 end,
	cccccc = {},
	['ddd)(*&^(*&%^ ddd'] = {},
},{
	aaaaaa = 1,
	bbbbbb = function() return 1 end,
	cccccc = {},
	['ddd)(*&^(*&%^ ddd'] = {},
	[true] = false,
	[false] = true,
},{
	true,true,true,true,
	[true] = false,
	[false] = true,
},{
	true,true,true,true,
	aaaaaa = 1,
	bbbbbb = function() return 1 end,
	cccccc = {},
	['ddd)(*&^(*&%^ ddd'] = {},
	[true] = false,
	[false] = true,
}
				"));

				// luanet.summarizetable must not throw any exceptions!
				int top = lua.gettop(L);
				for (int i = 1; i <= top; ++i)
				{
					luanet.summarizetable(L,i, 256);          Assert.AreEqual(top, lua.gettop(L));
					luanet.summarizetable(L,i, 0);            Assert.AreEqual(top, lua.gettop(L));
					luanet.summarizetable(L,i, 10);           Assert.AreEqual(top, lua.gettop(L));
					luanet.summarizetable(L,i, int.MinValue); Assert.AreEqual(top, lua.gettop(L));
					luanet.summarizetable(L,i, int.MaxValue); Assert.AreEqual(top, lua.gettop(L));
				}
				luanet.summarizetable(L, -1, 256);
				Assert.AreEqual(top, lua.gettop(L));
			}
		}

		[TestMethod] public void BigTable()
		{
			using (var lua = new Lua())
			{
				LuaRegistrationHelper.TaggedStaticMethods(lua, this.GetType());
				lua["lua"] = lua;
				lua.DoString("a = MakeBigTable(1, lua)");
				lua.DoString("b = MakeBigTable(256, lua)");
				lua.DoString("ConsumeBigTable(b)");
				lua.DoString("c = MakeBigTable(65536, lua)");
				lua.DoString("ConsumeBigTable(a)");
				lua.DoString("ConsumeBigTable(c)");
			}
		}
		[LuaGlobal] public static LuaTable MakeBigTable(int n, Lua lua)
		{
			var r = new Random(n);
			var t = lua.NewTable(n, 0);
			for (int i = 1; i <= n; ++i)
			{
				var row = lua.NewTable();

				var field = lua.NewTable(3, 0);
				field[1] = r.Next();
				field[2] = r.NextDouble();
				field[3] = r.NextDouble().ToString();
				row["vector"] = field;
				field.Dispose();
				
				field = lua.NewTable(0, 3);
				field["a"] = r.Next();
				field["b"] = r.NextDouble();
				field["c"] = r.NextDouble().ToString();
				row["obj"] = field;
				field.Dispose();
				
				row["int"] = r.Next();
				row["double"] = r.NextDouble();
				row["string"] = r.NextDouble().ToString();
				row["bool"] = r.Next(0,1) == 1;
				row["userdata"] = System.Text.Encoding.UTF8;
				row.RawSet("rawset", r.Next());

				t[i] = row;
				row.Dispose();
			}
			return t;
		}
		[LuaGlobal] public static void ConsumeBigTable(LuaTable t)
		{
			t.ForEachI((i, value) =>
			{
				var row = (LuaTable) value;
				var field = (LuaTable) row["vector"];
				Eat((double) field[1]); Eat((double) field.RawGet(1)); Eat((int)   field.GetValue(1)); Eat((int)   field.RawGetValue(1));
				Eat((double) field[2]); Eat((double) field.RawGet(2)); Eat<double>(field.GetValue(2)); Eat<double>(field.RawGetValue(2));
				Eat((string) field[3]); Eat((string) field.RawGet(3)); Eat<string>(field.GetValue(3)); Eat<string>(field.RawGetValue(3));
				field.Dispose();
				
				field = (LuaTable) row["obj"];
				Eat((double) field["a"]); Eat((double) field.RawGet("a")); Eat((int)   field.GetValue("a")); Eat((int)   field.RawGetValue("a"));
				Eat((double) field["b"]); Eat((double) field.RawGet("b")); Eat<double>(field.GetValue("b")); Eat<double>(field.RawGetValue("b"));
				Eat((string) field["c"]); Eat((string) field.RawGet("c")); Eat<string>(field.GetValue("c")); Eat<string>(field.RawGetValue("c"));
				field.Dispose();
				
				Eat((double)               row["int"     ]); Eat((double)               row.RawGet("int"     )); Eat<double>(               row.GetValue("int"     )); Eat<double>(               row.RawGetValue("int"     ));
				Eat((double)               row["double"  ]); Eat((double)               row.RawGet("double"  )); Eat<double>(               row.GetValue("double"  )); Eat<double>(               row.RawGetValue("double"  ));
				Eat((string)               row["string"  ]); Eat((string)               row.RawGet("string"  )); Eat<string>(               row.GetValue("string"  )); Eat<string>(               row.RawGetValue("string"  ));
				Eat((bool)                 row["bool"    ]); Eat((bool)                 row.RawGet("bool"    )); Eat<bool>(                 row.GetValue("bool"    )); Eat<bool>(                 row.RawGetValue("bool"    ));
				Eat((System.Text.Encoding) row["userdata"]); Eat((System.Text.Encoding) row.RawGet("userdata")); Assert.AreEqual(LuaType.UserData, row.GetValue("userdata").Type); Assert.AreEqual(LuaType.UserData, row.RawGetValue("userdata").Type);
				Eat((double)               row["rawset"  ]); Eat((double)               row.RawGet("rawset"  )); Eat((int)                  row.GetValue("rawset"  )); Eat((int)                  row.RawGetValue("rawset"  ));

				row.Dispose();
			});
			t.Dispose();
		}
		[MethodImpl(MethodImplOptions.NoInlining)] static void Eat<T>(T value) {}
	}
}