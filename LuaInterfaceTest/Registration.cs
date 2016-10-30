using System.Reflection;
using LuaInterface;

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

	[TestClass] public class Registration
	{
		[TestCleanup] public void Cleanup()
		{
			GcUtil.GcAll();
		}

		class RegisterFunction_Target
		{
			string _first = "First";
			public string First() { return _first; }
			public static string Second() { return "Second"; }
			private static string Third() { return "Third"; }
			public static string Fourth() { return "Fourth"; }
			public static string Fifth() { return "Fifth"; }
		}
		[TestMethod] public void RegisterFunction()
		{
			using (var lua = new Lua())
			{
				var target = new RegisterFunction_Target();
				lua.RegisterFunction("First", target, typeof(RegisterFunction_Target).GetMethod("First"));
				lua.DoString("assert(First() == 'First')");

				lua.RegisterFunction("Second", null, typeof(RegisterFunction_Target).GetMethod("Second"));
				lua.DoString("assert(Second() == 'Second')");

				lua.RegisterFunction("Third", null, typeof(RegisterFunction_Target).GetMethod("Third", BindingFlags.NonPublic | BindingFlags.Static));
				lua.DoString("assert(Third() == 'Third')");

				lua["Fourth"] = lua.NewFunction(null, typeof(RegisterFunction_Target).GetMethod("Fourth"));
				lua.DoString("assert(Fourth() == 'Fourth')");

				lua.NewTable("Util");
				lua.RegisterFunction("Util.Fifth", null, typeof(RegisterFunction_Target).GetMethod("Fifth"));
				lua.DoString("assert(Util.Fifth() == 'Fifth')");

				lua.RegisterFunction("Sixth", EzDelegate.Func(() => "Sixth"));
				lua.DoString("assert(Sixth() == 'Sixth')");
			}
		}
		#if !ENABLE_MONO || !(UNITY_5 || UNITY_4) // unity's compiler can't do type inference on method groups...
		[TestMethod] public void RegisterFunctionEzDelegate()
		{
			using (var lua = new Lua())
			{
				var target = new RegisterFunction_Target();
				lua.RegisterFunction("First", EzDelegate.Func(target.First));
				lua.DoString("assert(First() == 'First')");

				lua.RegisterFunction("Second", EzDelegate.Func(RegisterFunction_Target.Second));
				lua.DoString("assert(Second() == 'Second')");

				lua.RegisterFunction("Third", null, typeof(RegisterFunction_Target).GetMethod("Third", BindingFlags.NonPublic | BindingFlags.Static));
				lua.DoString("assert(Third() == 'Third')");

				lua["Fourth"] = lua.NewFunction(EzDelegate.Func(RegisterFunction_Target.Fourth));
				lua.DoString("assert(Fourth() == 'Fourth')");

				lua.NewTable("Util");
				lua.RegisterFunction("Util.Fifth", EzDelegate.Func(RegisterFunction_Target.Fifth));
				lua.DoString("assert(Util.Fifth() == 'Fifth')");

				lua.RegisterFunction("Sixth", EzDelegate.Func(() => "Sixth"));
				lua.DoString("assert(Sixth() == 'Sixth')");
			}
		}
		#endif

		class TaggedInstanceMethods_Target
		{
			public void Register(Lua lua)
			{
				lua.NewTable("Util");
				LuaRegistrationHelper.TaggedInstanceMethods(lua, this);
			}

			string _first = "First";
			[LuaGlobal]
			public string First() { return _first; }

			[LuaGlobal(Name = "Second")]
			public string Second_Impl() { return "Second"; }

			[LuaGlobal(Name = "Util.Third", Description = @"LuaInterface ignores this description; it is only here for your convenience.
			                                              It might be useful for implementing an Intellisense-like tool or an automatic documentation system.")]
			public string Util_Third_Impl() { return "Third"; }
		}
		[TestMethod] public void TaggedInstanceMethods()
		{
			using (var lua = new Lua())
			{
				var target = new TaggedInstanceMethods_Target();
				target.Register(lua);
				lua.DoString("assert(First() == 'First')");
				lua.DoString("assert(Second() == 'Second')");
				lua.DoString("assert(Util.Third() == 'Third')");
			}
		}

		static class TaggedStaticMethods_Target
		{
			public static void Register(Lua lua)
			{
				lua.NewTable("Util");
				LuaRegistrationHelper.TaggedStaticMethods(lua, typeof(TaggedStaticMethods_Target));
			}

			[LuaGlobal]
			public static string First() { return "First"; }

			[LuaGlobal(Name = "Second")]
			public static string Second_Impl() { return "Second"; }

			[LuaGlobal(Name = "Util.Third", Description = "3rd test function.")]
			public static string Util_Third_Impl() { return "Third"; }
		}
		[TestMethod] public void TaggedStaticMethods()
		{
			using (var lua = new Lua())
			{
				TaggedStaticMethods_Target.Register(lua);
				lua.DoString("assert(First() == 'First')");
				lua.DoString("assert(Second() == 'Second')");
				lua.DoString("assert(Util.Third() == 'Third')");
			}
		}

		enum Enumeration_Target1 { A,B,C,D }
		enum Enumeration_Target2 : byte { A=5,S,D,F }

		[TestMethod] public void Enumeration()
		{
			using (var lua = new Lua())
			{
				lua.DoString(
					@"function test1(table)
						assert(table.A == 0)
						assert(table.B == 1)
						assert(table.C == 2)
						assert(table.D == 3)
						assert_count(table, 4)
					end
					function assert_count(table, expected_count)
						local count = 0
						for k in pairs(table) do count = count + 1 end
						assert(count == expected_count)
					end
				");
				LuaRegistrationHelper.Enumeration<Enumeration_Target1>(lua);
				using (var table = LuaRegistrationHelper.NewEnumeration<Enumeration_Target1>(lua))
					lua["Target1"] = table;
				lua.DoString("test1(Enumeration_Target1); test1(Target1)");

				lua.DoString(
					@"function test2(table)
						assert(table.A == 5)
						assert(table.S == 6)
						assert(table.D == 7)
						assert(table.F == 8)
						assert_count(table, 4)
					end
				");
				LuaRegistrationHelper.Enumeration<Enumeration_Target2>(lua);
				using (var table = LuaRegistrationHelper.NewEnumeration<Enumeration_Target2>(lua))
					lua["Target2"] = table;
				lua.DoString("test2(Enumeration_Target2); test2(Target2)");
			}
		}
	}
}