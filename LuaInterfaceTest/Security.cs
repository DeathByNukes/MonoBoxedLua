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

	[TestClass] public class Security
	{
		[TestCleanup] public void Cleanup()
		{
			GcUtil.GcAll();
		}

		[TestMethod] public void Reflection()
		{
			using (var lua = new Lua())
			{
				lua["object"] = new object();
				object result = lua.DoString(@"return object:GetType()")[0];
				#if NUNIT
				Assert.IsInstanceOf<string>(result);
				#else
				Assert.IsInstanceOfType(result, typeof(string));
				#endif
				// with access to the object returned by GetType() it is possible to use MethodInfo.Invoke on arbitrary methods
				// therefore, ObjectTranslator.push blacklists all types in the System.Reflection namespace and all types that inherit from them, which includes System.Type
				// blacklisted types are converted to strings (ToString) and the string is pushed instead.
			}
		}
	}
}