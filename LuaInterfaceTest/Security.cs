using System;
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
					UAssert.IsInstanceOf<string>(result, expr);
				}
			}
		}

		[TestMethod] public void LoadAssembly()
		{
			using (var lua = new Lua())
			{
				foreach (var assembly in new [] {"mscorlib", "System", "LuaInterface"})
				try
				{
					lua.Eval("load_assembly(...)", assembly);
					Assert.Fail("Succeeded in loading assembly: "+assembly);
				}
				catch (LuaScriptException) {}
			}
		}
		[TestMethod] public void ImportType()
		{
			using (var lua = new Lua())
			{
				foreach (var assembly in new [] {"mscorlib", "System", "LuaInterface"})
				try
				{
					lua.Eval("load_assembly(...)", assembly);
				}
				catch (LuaScriptException) {}

				Assert.IsNull(lua.Eval("import_type('System.Diagnostics.Process')"));
				Assert.IsNull(lua.Eval("import_type('System.IO.File')"));
				Assert.IsNull(lua.Eval("import_type('LuaInterface.Lua')"));
			}
		}
		[TestMethod] public void LoadType()
		{
			using (var lua = new Lua())
			{
				lua.LoadType(typeof(System.DateTime));

				lua.DoString("DateTime = import_type 'System.DateTime'");
				Assert.AreEqual(new DateTime(12), lua.Eval("DateTime(12)"));
				Assert.AreEqual((double) new DateTime[12].Length, lua.Eval("DateTime[12].Length"));
				Assert.AreEqual(new DateTime(12).Subtract(new DateTime(6)), lua.Eval("DateTime(12):Subtract(DateTime(6))"));

				Assert.IsNull(lua.Eval("import_type('System.TimeSpan')"));

				Assert.IsNull(lua.Eval("import_type('System.Diagnostics.Process')"));
				Assert.IsNull(lua.Eval("import_type('System.IO.File')"));
				Assert.IsNull(lua.Eval("import_type('LuaInterface.Lua')"));
			}
		}
		[TestMethod] public void Elevate()
		{
			using (var lua = new Lua())
			{
				// this is an example of what you should NOT do for untrusted scripts
				lua.LoadAssembly(typeof(System.Uri).Assembly); // loads System.dll
				lua.DoString("Process = import_type 'System.Diagnostics.Process'"); // is also in System.dll
				Assert.IsNotNull(lua["Process"]);
				//lua.DoString("Process.Start('calc.exe')"); // pwned
			}
		}
		[TestMethod] public void Metatable()
		{
			using (var lua = new Lua())
			{
				lua["o"] = new object();
				Assert.AreNotEqual("table", lua.Eval("type(getmetatable(o))"));
			}
		}

		[TestMethod] public void Bytecode()
		{
			using (var lua = new Lua(true))
			{
				Assert.IsTrue(lua.BytecodeEnabled);

				const string retval = "pwned";
				Action<LuaFunction> verify = func =>
				{
					Assert.IsNotNull(func);
					var rets = func.Call();
					func.Dispose();
					Assert.IsNotNull(rets);
					Assert.AreEqual(retval, rets[0]);
				};
				var function = lua.Eval<LuaFunction>("function() return '"+retval+"' end");
				var dump = function.Dump();
				function.Dispose(); function = null;

				Assert.AreEqual((byte)Lua.BytecodePrefix, dump[0]);
				var dump_str = lua.NewString(dump);

				verify(lua.Eval<LuaFunction>("assert(loadstring(...))", dump_str));

				lua.SecureLuaFunctions(); // ---------------------------------
				Assert.IsFalse(lua.BytecodeEnabled);

				// SecureLuaFunctions() doesn't directly secure the C# API but it does disable Lua.BytecodeEnabled which affects everything
				try
				{
					lua.LoadBuffer(dump, "bytecode");
					Assert.Fail();
				}
				catch (LuaScriptException ex)
				{
					Assert.AreEqual("[string \"bytecode\"]:1: unexpected symbol near 'char(27)'", ex.Message);
				}

				// safest way to load trusted bytecode
				{
					LuaFunction func;
					using (lua.BytecodeRegion(true))
						func = lua.LoadBuffer(dump, "bytecode");
					verify(func);
				}

				// be very careful about this pattern because all code in verify() will be running in the unsafe context
				//using (lua.BytecodeRegion(true))
				//	verify(lua.LoadBuffer(dump, "bytecode"));

				// Lua script functions should be incapable of loading bytecode regardless of the state of BytecodeEnabled
				// if you need Lua functions that can load bytecode, save them in closures or other private locations before calling SecureLuaFunctions()
				for (int i = 0; i < 2; ++i)
				{
					function = lua.Eval<LuaFunction>("loadstring(...)", dump_str);
					if (function != null)
					{
						verify(function);
						Assert.Fail("bytecode loaded and executed successfully");
					}
					lua.BytecodeEnabled = true;
				}

				dump_str.Dispose();
			}
		}

		public class ExposedClass
		{
			#pragma warning disable CS0414 // The field is assigned but its value is never used
			public static bool PublicS = true;
			public        bool Public = true;
			internal static bool InternalS = true;
			internal        bool Internal = true;
			protected static bool ProtectedS = true;
			protected        bool Protected = true;
			private static bool PrivateS = true;
			private        bool Private = true;
			#pragma warning restore CS0414
		}
		[TestMethod] public void MemberAccess()
		{
			using (var lua = new Lua(true))
			{
				lua.RegisterType(typeof(ExposedClass));
				lua.DoString(@"
					assert(ExposedClass.PublicS)
					assert(ExposedClass().Public)
				");
				UAssert.Throws<LuaScriptException>(() =>
				{
					lua.DoString(@"
						InternalS = ExposedClass.InternalS
						Internal  = ExposedClass().Internal
					");
				});
				UAssert.Throws<LuaScriptException>(() =>
				{
					lua.DoString(@"
						ProtectedS = ExposedClass.ProtectedS
						Protected  = ExposedClass().Protected
					");
				});
				UAssert.Throws<LuaScriptException>(() =>
				{
					lua.DoString(@"
						PrivateS = ExposedClass.PrivateS
						Private  = ExposedClass().Private
					");
				});
				CollectionAssert.DoesNotContain(lua.DoString("return InternalS,Internal,ProtectedS,Protected,PrivateS,Private"), true);
			}
		}

		[TestMethod] public void StdLib()
		{
			using (var lua = new Lua())
			{
				using (var t = lua.GetTable("debug"))
				{
					Assert.AreEqual(2, t.Count());
					Assert.IsTrue(t.ContainsKey("getinfo"));
					Assert.IsTrue(t.ContainsKey("traceback"));
				}
				using (var t = lua.GetTable("os"))
				{
					Assert.AreEqual(4, t.Count());
					Assert.IsTrue(t.ContainsKey("clock"));
					Assert.IsTrue(t.ContainsKey("date"));
					Assert.IsTrue(t.ContainsKey("difftime"));
					Assert.IsTrue(t.ContainsKey("time"));
				}
				using (var t = lua.GetGlobals())
				{
					Assert.IsTrue(!t.ContainsKey("io"));
					Assert.IsTrue(!t.ContainsKey("package"));
					Assert.IsTrue(!t.ContainsKey("module"));
					Assert.IsTrue(!t.ContainsKey("require"));
					Assert.IsTrue(!t.ContainsKey("load_assembly"));
					Assert.IsTrue(!t.ContainsKey("make_object"));
					Assert.IsTrue(!t.ContainsKey("free_object"));
				}
			}
		}
	}
}