using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using LuaInterface;
using LuaInterface.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LuaInterfaceTest
{
	[TestClass] public class References
	{
		[TestCleanup] public void Cleanup()
		{
			GcUtil.GcAll();
		}

		[TestMethod] public void NewReference()
		{
			using (var lua = new Lua())
			{
				var types = (
					from assembly in AppDomain.CurrentDomain.GetAssemblies()
					from type in assembly.GetTypes()
					where !type.IsAbstract && typeof(LuaBase).IsAssignableFrom(type)
					select type  )
					.ToArray();

				Trace.WriteLine("Types: " + string.Join(", ", (object[])types));
				foreach (var type in types)
				{
					var method = type.GetMethod("NewReference", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
					Assert.IsTrue(method != null && type == method.ReturnType, string.Format("{0} inherits from LuaBase but has no NewReference() method.", type.FullName));
				}

				using (var one = lua.NewTable())
				using (var two = lua.NewTable())
				using (var one_newref = one.NewReference())
					Assert.IsTrue(one == one_newref && one != two && one_newref != two);

				using (var one = lua.LoadString("-- noop"))
				using (var two = lua.LoadString("-- nop"))
				using (var one_newref = one.NewReference())
					Assert.IsTrue(one == one_newref && one != two && one_newref != two);

				using (var one = lua.NewUserData(0, null))
				using (var two = lua.NewUserData(0, null))
				using (var one_newref = one.NewReference())
					Assert.IsTrue(one == one_newref && one != two && one_newref != two);

				if (types.Length != 3)
					Assert.Inconclusive("Not all LuaBase implementations are covered by this test.");
			}
		}
		[TestMethod] public void Equality()
		{
			using (var lua = new Lua())
			{
				var tables = lua.DoString("return {'one'}, {'two'}, {__eq = function(a,b) return true end}");
				using (tables.DeferDisposeAll())
				{
					LuaTable one = (LuaTable)tables[0], two = (LuaTable)tables[1], friendly_meta = (LuaTable)tables[2];
					Assert.AreEqual("one", one.RawGet(1));
					Assert.AreEqual("two", two.RawGet(1));
					using (var one_ref = one.NewReference())
					{
						Assert.IsTrue(one == one_ref && one != two && one_ref != two, "lua equality failed");
						Assert.IsTrue(one.Equals(one_ref) && !one.Equals(two) && !one_ref.Equals(two), "raw equality failed");
						one.Metatable = friendly_meta;
						two.Metatable = friendly_meta;
						Assert.IsTrue(one == one_ref && one == two && one_ref == two, "lua equality failed with metatable");
						Assert.IsTrue(one.Equals(one_ref) && !one.Equals(two) && !one_ref.Equals(two), "raw equality failed with metatable");
					}
				}
			}
		}

		[TestMethod] public void ClrInLua()
		{
			using (var lua = new Lua())
			{
				var test = GcUtil.NewTest(() =>
				{
					var o = new object();
					lua["o"] = o;
					Assert.AreEqual(o, lua["o"]);
					return o;
				});
				GcUtil.GcAll();
				test.AssertAlive();

				lua["o"] = null;
				lua.CollectGarbage();
				GcUtil.GcAll();
				test.AssertDead();
			}
		}
	}
}