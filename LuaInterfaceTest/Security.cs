using LuaInterface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LuaInterfaceTest
{
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
				Assert.IsInstanceOfType(result, typeof(string));
				// with access to the object returned by GetType() it is possible to use MethodInfo.Invoke on arbitrary methods
				// therefore, ObjectTranslator.push blacklists all types in the System.Reflection namespace and all types that inherit from them, which includes System.Type
				// blacklisted types are converted to strings (ToString) and the string is pushed instead.
			}
		}
	}
}