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
				// with access to the object returned by GetType() it is possible to use MethodInfo.Invoke on arbitrary methods
				// therefore, ObjectTranslator.push blacklists all types in the System.Reflection namespace
				// blacklisted types are converted to strings (ToString) and the string is pushed instead.
				lua["object"] = new object();
				lua.DoString(@"t = object:GetType()");
				string[] scripts =
				{
					"return t.Assembly",
					"return t.Module",
					"return t:GetMethod('ToString')",
					"return t:GetConstructors()", // this evaluates to the string "System.Reflection.ConstructorInfo[]"
					"return t:GetMethod('ReferenceEquals')", // note that ReferenceEquals is a static method
				};
				foreach (var script in scripts)
				{
					var result = lua.DoString(script)[0];
					#if NUNIT
						Assert.IsInstanceOf<string>(result, script);
					#else
						Assert.IsInstanceOfType(result, typeof(string), script);
					#endif
				}
			}
		}
	}
}