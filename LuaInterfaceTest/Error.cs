using System;
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

	[TestClass] public class Error
	{
		#region Tools

		[TestCleanup] public void Cleanup()
		{
			GcUtil.GcAll();
		}
		private static Lua NewTest()
		{
			var lua = new Lua();
			try
			{
				lua.DoString(@"
					local error = error
					function bad()
						error('"+ErrorMessage+@"', 0)
					end
					local bad = bad
					badmeta = {
						__index    = bad,
						__newindex = bad,
						__gc       = bad,
						__mode     = bad,
						__eq       = bad,
						__add      = bad,
						__sub      = bad,
						__mul      = bad,
						__div      = bad,
						__mod      = bad,
						__pow      = bad,
						__unm      = bad,
						__len      = bad,
						__lt       = bad,
						__le       = bad,
						__concat   = bad,
						__call     = bad,
					}
					badtable = setmetatable({}, badmeta)

					function doCallback()
						callback:Invoke()
					end
					function pDoCallback()
						return pcall(doCallback)
					end
				", "@Error.cs.NewTest.lua");
				lua["ErrorMessage"] = ErrorMessage;
			}
			catch
			{
				lua.Dispose();
				throw;
			}
			return lua;
		}

		const string ErrorMessage = "bad() called";

		static void CheckException(LuaScriptException ex) {
			Assert.AreEqual(ErrorMessage, ex.Message);
		}
		static void CheckUnprotectedException(LuaException ex) {
			Assert.AreEqual("unprotected error in call to Lua API ("+ErrorMessage+")", ex.Message);
		}
		static void CheckPCall(object expected, object[] results) {
			Assert.AreEqual(2, results.Length);
			Assert.AreEqual(false, results[0]);
			Assert.AreEqual(expected, results[1]);
		}
		static object GetPCallError(object[] results) {
			Assert.AreEqual(2, results.Length);
			Assert.AreEqual(false, results[0]);
			return results[1];
		}
		static T GetPCallError<T>(object[] results) {
			var err = GetPCallError(results);
			UAssert.IsInstanceOf<T>(err);
			return (T) err;
		}
		static LuaScriptException GetPCallException(object[] results) {
			return GetPCallError<LuaScriptException>(results);
		}
		class TestException : Exception {
			public TestException() {}
			public TestException(string message) : base(message) {}
			public TestException(string message, Exception innerException) : base(message, innerException) {}
		}
		static void ThrowTestException(string message) { throw new TestException(message); }

		#endregion

		[TestMethod] public void Call()
		{
			using (var lua = NewTest())
			{
				try
				{
					using (var f = lua.GetFunction("bad"))
						f.Call(null);
					Assert.Fail("didn't throw");
				}
				catch (LuaScriptException ex) { CheckException(ex); }
			}
		}
		[TestMethod] public void DoString()
		{
			using (var lua = NewTest())
			{
				try
				{
					lua.DoString("bad()");
					Assert.Fail("didn't throw");
				}
				catch (LuaScriptException ex) { CheckException(ex); }
			}
		}
		[TestMethod] public void MetamethodNaked()
		{
			using (var lua = NewTest())
			{
				try
				{
					using (var t = lua.GetTable("badtable"))
						GC.KeepAlive(t["foo"]);
					Assert.Fail("didn't throw");
				}
				catch (LuaException ex) { CheckUnprotectedException(ex); }
			}
		}
		[TestMethod] public void MetamethodCallback()
		{
			using (var lua = NewTest())
			{
				int hit_catch = 0, hit_finally = 0;
				lua["callback"] = new Action(() =>
				{
					try
					{
						using (var t = lua.GetTable("badtable"))
							GC.KeepAlive(t["foo"]);
					}
					catch (LuaInterface.LuaAPI.LuaInternalException) { ++hit_catch; throw; }
					catch { Assert.Fail("lua error caught as something other than LuaInternalException"); }
					finally { ++hit_finally; }
					Assert.Fail("didn't throw (inner)");
				});

				try
				{
					lua.DoString("callback:Invoke()");
					Assert.Fail("didn't throw (outer)");
				}
				catch (LuaScriptException ex) { CheckException(ex); }
				Assert.AreEqual(1, hit_catch, "catch{} wasn't called during Lua error or was called more than once");
				Assert.AreEqual(1, hit_finally, "finally{} wasn't called during Lua error or was called more than once");
			}
		}
		[TestMethod] public void MetamethodCPCall()
		{
			using (var lua = NewTest())
			{
				// this test shows how to catch metamethod errors by wrapping your usage in CPCall
				lua["callback"] = new Action(() =>
				{
					try
					{
						using (var t = lua.GetTable("badtable"))
						{
							lua.CPCall(() =>
							{
								GC.KeepAlive(t["foo"]);
							});
							Assert.Fail("didn't throw");
						}
					}
					catch (LuaScriptException ex) { CheckException(ex); }
				});

				lua.DoString("callback:Invoke()");
			}
		}
		[TestMethod] public void PCall()
		{
			using (var lua = NewTest())
			{
				// normal case, entirely-in-lua error
				CheckPCall(ErrorMessage, lua.DoString("return pcall(bad)"));
			}
		}
		[TestMethod] public void PCallClrError()
		{
			using (var lua = NewTest())
			using (var pcall = lua.GetFunction("pDoCallback"))
			{
				lua["callback"] = new Action(delegate
				{
					var L = LuaInterface.LuaAPI.luanet.getstate(lua);
					LuaInterface.LuaAPI.lua.pushstring(L, "success");
					LuaInterface.LuaAPI.lua.error(L);
				});
				CheckPCall("success", pcall.Call());
			}
		}
		[TestMethod] public void PCallClrException()
		{
			using (var lua = NewTest())
			using (var pcall = lua.GetFunction("pDoCallback"))
			{
				var exception = new TestException();
				lua["callback"] = new Action(() =>
				{
					throw exception;
				});
				Assert.IsTrue(object.ReferenceEquals(
					exception,
					GetPCallException(pcall.Call()).InnerException  ));
			}
		}
		[TestMethod] public void PCallClrNestedError()
		{
			using (var lua = NewTest())
			using (var pcall = lua.GetFunction("pDoCallback"))
			{
				lua["callback"] = new Action(() =>
				{
					lua.DoString("bad()");
				});
				Assert.AreEqual(ErrorMessage, GetPCallException(pcall.Call()).Message);
			}
		}
		[TestMethod] public void PCallRegisteredFunction()
		{
			using (var lua = NewTest())
			{
				// todo: make a RegisterFunction test that uses typeof(x).GetConstructor
				var ThrowTestException = typeof(LuaInterfaceTest.Error).GetMethod("ThrowTestException", BindingFlags.NonPublic | BindingFlags.Static);
				Assert.IsNotNull(ThrowTestException, "ThrowTestException not found");
				lua.RegisterFunction("throw", null, ThrowTestException);

				var ex = GetPCallException(lua.DoString(
					"return pcall(function() throw('success') end)"  ));
				var inner = (TestException) ex.InnerException;
				Assert.AreEqual("success", inner.Message);
			}
		}
		[TestMethod] public void CFunctionException()
		{
			using (var lua = NewTest())
			{
				lua["callback"] = new LuaInterface.LuaAPI.lua.CFunction(L =>
				{
					throw new Exception("unit test");
				});
				Console.WriteLine(GetPCallError<string>(lua.DoString("return pcall(callback)")));
			}
		}
		[TestMethod] public void StackOverflow()
		{
			const int LUAI_MAXCSTACK = 8000;
			using (var lua = new Lua())
			{
				var L = LuaInterface.LuaAPI.luanet.getstate(lua);
				Action[] bodies =
				{
					()=> {
						lua.CPCall(() => {
							LuaInterface.LuaAPI.luaL.checkstack(L, LUAI_MAXCSTACK+1, "luaL path 1");
						});
					},
					()=> {
						lua.CPCall(() => {
							LuaInterface.LuaAPI.lua.pushnil(L);
							LuaInterface.LuaAPI.luaL.checkstack(L, LUAI_MAXCSTACK, "luaL path 2");
						});
					},
					()=> {
						LuaInterface.LuaAPI.luaclr.checkstack(L, LUAI_MAXCSTACK+1, "luaclr path 1");
					},
					()=> {
						LuaInterface.LuaAPI.lua.pushnil(L);
						LuaInterface.LuaAPI.luaclr.checkstack(L, LUAI_MAXCSTACK, "luaclr path 2");
					},
				};
				foreach (var body in bodies)
				try
				{
					body();
					Assert.Fail("No error was thrown.");
				}
				catch (LuaScriptException ex)
				{
					Assert.IsTrue(ex.Message.Contains("stack overflow"), "Error was not a stack overflow.");
					LuaInterface.LuaAPI.lua.settop(L, 0);
				}
			}
		}
	}
}