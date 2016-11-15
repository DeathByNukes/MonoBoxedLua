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
				string[] expressions =
				{
					"t.Assembly",
					"t.Module",
					"t:GetMethod('ToString')",
					"t:GetConstructors()", // this evaluates to the string "System.Reflection.ConstructorInfo[]"
					"t:GetMethod('ReferenceEquals')", // note that ReferenceEquals is a static method
				};
				foreach (var expr in expressions)
				{
					var result = lua.Eval(expr);
					#if NUNIT
						Assert.IsInstanceOf<string>(result, script);
					#else
						Assert.IsInstanceOfType(result, typeof(string), expr);
					#endif
				}
			}
		}
	}
}