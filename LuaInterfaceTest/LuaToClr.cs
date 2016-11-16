﻿using LuaInterface;

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

	[TestClass] public class LuaToClr
	{
		[TestCleanup] public void Cleanup()
		{
			GcUtil.GcAll();
		}

		class TestClass
		{
			public TestClass() {}
			public TestClass(bool test) {}
			static TestClass() {}

			public static bool   StaticMethod() { return true; }
			public        bool InstanceMethod() { return true; }

			public static bool   StaticProperty { get { return true; } }
			public        bool InstanceProperty { get { return true; } }

			public static readonly bool   StaticField = true;
			public        readonly bool InstanceField = true;
		}

		static void TestAllowed(Lua lua, string expr)
		{
			Assert.AreEqual(true, lua.Eval(expr));
		}
		static void TestNotAllowed(Lua lua, string expr, string err)
		{
			try
			{
				TestAllowed(lua, expr);
				Assert.Fail(err);
			}
			catch (LuaScriptException) {}
		}

		[TestMethod] public void StaticFromInstance()
		{
			using (var lua = new Lua())
			{
				lua["test_instance"] = new TestClass();
				lua.RegisterType(typeof(TestClass));

				// method
				TestAllowed   (lua, "test_instance:InstanceMethod()");
				TestNotAllowed(lua, "test_instance:StaticMethod()", "Accessed static method from instance.");
				TestAllowed   (lua, "TestClass.StaticMethod()");
				TestNotAllowed(lua, "test_instance:StaticMethod()", "Accessed static method from instance via cache.");

				// property
				TestAllowed   (lua, "test_instance.InstanceProperty");
				TestNotAllowed(lua, "test_instance.StaticProperty", "Accessed static property from instance.");
				TestAllowed   (lua, "TestClass.StaticProperty");
				TestNotAllowed(lua, "test_instance.StaticProperty", "Accessed static property from instance via cache.");

				// field
				TestAllowed   (lua, "test_instance.InstanceField");
				TestNotAllowed(lua, "test_instance.StaticField", "Accessed field method from instance.");
				TestAllowed   (lua, "TestClass.StaticField");
				TestNotAllowed(lua, "test_instance.StaticField", "Accessed field method from instance via cache.");
			}
		}

		[TestMethod] public void Constructor()
		{
			using (var lua = new Lua())
			{
				lua["test_instance"] = new TestClass();
				lua.RegisterType(typeof(TestClass));

				TestNotAllowed(lua, "TestClass['.cctor'] ~= nil", "Got static constructor from type reference.");
				TestNotAllowed(lua, "test_instance['.cctor'] ~= nil", "Got static constructor from instance.");

				TestNotAllowed(lua, "TestClass['.ctor'] ~= nil", "Got constructor from type reference.");
				TestNotAllowed(lua, "test_instance['.ctor'] ~= nil", "Got constructor from instance.");
			}
		}

		enum TestEnum { A, B, C }

		[TestMethod] public void Enum()
		{
			using (var lua = new Lua())
			{
				lua["a"] = TestEnum.A;
				lua.RegisterType(typeof(TestEnum));

				Assert.AreEqual(typeof(TestEnum), lua.Eval("a").GetType());
				Assert.AreEqual(TestEnum.A, lua.Eval("a"));
				Assert.AreEqual(TestEnum.A, lua.Eval("TestEnum.A"));
				Assert.AreEqual(TestEnum.B, lua.Eval("...", TestEnum.B));
				Assert.AreEqual(TestEnum.C, lua.Eval("TestEnum.C"));
			}
		}
	}
}