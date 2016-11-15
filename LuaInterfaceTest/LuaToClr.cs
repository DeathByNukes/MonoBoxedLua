using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using LuaInterface;
using LuaInterface.Helpers;

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

		static object SingleResult(object[] results)
		{
			if (results == null) return null;
			Assert.AreEqual(1, results.Length);
			return results[0];
		}

		static void TestAllowed(Lua lua, string chunk)
		{
			Assert.AreEqual(true, SingleResult(lua.DoString(chunk)));
		}
		static void TestNotAllowed(Lua lua, string chunk, string err)
		{
			try
			{
				TestAllowed(lua, chunk);
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
				TestAllowed   (lua, "return test_instance:InstanceMethod()");
				TestNotAllowed(lua, "return test_instance:StaticMethod()", "Accessed static method from instance.");
				TestAllowed   (lua, "return TestClass.StaticMethod()");
				TestNotAllowed(lua, "return test_instance:StaticMethod()", "Accessed static method from instance via cache.");

				// property
				TestAllowed   (lua, "return test_instance.InstanceProperty");
				TestNotAllowed(lua, "return test_instance.StaticProperty", "Accessed static property from instance.");
				TestAllowed   (lua, "return TestClass.StaticProperty");
				TestNotAllowed(lua, "return test_instance.StaticProperty", "Accessed static property from instance via cache.");

				// field
				TestAllowed   (lua, "return test_instance.InstanceField");
				TestNotAllowed(lua, "return test_instance.StaticField", "Accessed field method from instance.");
				TestAllowed   (lua, "return TestClass.StaticField");
				TestNotAllowed(lua, "return test_instance.StaticField", "Accessed field method from instance via cache.");
			}
		}

		[TestMethod] public void Constructor()
		{
			using (var lua = new Lua())
			{
				lua["test_instance"] = new TestClass();
				lua.RegisterType(typeof(TestClass));

				TestNotAllowed(lua, "return TestClass['.cctor'] ~= nil", "Got static constructor from type reference.");
				TestNotAllowed(lua, "return test_instance['.cctor'] ~= nil", "Got static constructor from instance.");

				TestNotAllowed(lua, "return TestClass['.ctor'] ~= nil", "Got constructor from type reference.");
				TestNotAllowed(lua, "return test_instance['.ctor'] ~= nil", "Got constructor from instance.");
			}
		}

		enum TestEnum { A, B, C }

		[TestMethod] public void Enum()
		{
			using (var lua = new Lua())
			{
				lua["a"] = TestEnum.A;
				lua.RegisterType(typeof(TestEnum));

				Assert.AreEqual(typeof(TestEnum), SingleResult(lua.DoString("return a")).GetType());
				Assert.AreEqual(TestEnum.A, SingleResult(lua.DoString("return a")));
				Assert.AreEqual(TestEnum.A, SingleResult(lua.DoString("return TestEnum.A")));
				Assert.AreEqual(TestEnum.C, SingleResult(lua.DoString("return TestEnum.C")));
			}
		}
	}
}