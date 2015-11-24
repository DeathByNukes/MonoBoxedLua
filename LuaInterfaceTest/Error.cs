using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuaInterface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LuaInterfaceTest
{
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
		static LuaScriptException GetPCallException(object[] results) {
			Assert.AreEqual(2, results.Length);
			Assert.AreEqual(false, results[0]);
			Assert.IsInstanceOfType(results[1], typeof(LuaScriptException));
			return (LuaScriptException) results[1];
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
				// this test shows that if your code is running as a callback from a lua protected call, you are completely incapable of catching metamethod errors
				// the error will always jump directly back to the lua VM. all you can reliably do is cleanup code in finally blocks
				int hit_finally = 0;
				lua["callback"] = new Action(() =>
				{
					try
					{
						using (var t = lua.GetTable("badtable"))
							GC.KeepAlive(t["foo"]);
					}
					// one does not simply catch a lua error
					catch (SEHException)            { Assert.Fail("lua protected error caught as SEH exception"); }
					catch (ExternalException)       { Assert.Fail("lua protected error caught as external exception"); }
					catch (RuntimeWrappedException) { Assert.Fail("lua protected error caught as object"); }
					catch                           { Assert.Fail("lua protected error caught in general catch"); }
					// yet, through some magic, 'finally' still works
					finally { ++hit_finally; lua["hit_finally"] = true; }
					Assert.Fail("didn't throw (inner)");
				});

				try
				{
					lua.DoString("callback:Invoke()");
					Assert.Fail("didn't throw (outer)");
				}
				catch (LuaScriptException ex) { CheckException(ex); }
				Assert.AreEqual(1, hit_finally);
				Assert.AreEqual(true, (bool)lua["hit_finally"]);

				// suspicious that the "magic finally" might involve the CLR backtracking once it regains control much later
				// so added this test to confirm the side-effects entirely within Lua

				hit_finally = 0;
				lua["hit_finally"] = null;
				Assert.AreEqual(null, lua["hit_finally"]);

				Assert.AreEqual("success", lua.DoString(@"
					local status, msg = pcall(function() callback:Invoke() end)

					if status then                  error(""didn't throw"")
					elseif msg ~= ErrorMessage then error('wrong error: '..msg)
					elseif not hit_finally then     error(""didn't hit finally block"")
					else return 'success' end
				")[0]);
				Assert.AreEqual(1, hit_finally);
				Assert.AreEqual(true, (bool)lua["hit_finally"]);
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
			using (var pcall = lua.GetFunction("pDoCallback"))
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
	}
}