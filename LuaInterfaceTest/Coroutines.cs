using System;
using System.Collections.Generic;
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

	[TestClass] public class Coroutines
	{
		[TestMethod] public void CallbackInsideCoroutine()
		{
			using (var lua = new Lua())
			{
				var calls = new List<string>(4);
				lua.RegisterFunction("callback", new Action<string>(calls.Add));
				lua.DoString(@"
					callback('A')
					co = coroutine.create(function()
						callback('B')
						coroutine.yield()
						callback('D')
					end)
					assert(coroutine.resume(co))
					callback('C')
					assert(coroutine.resume(co))
					assert(coroutine.status(co) == 'dead')
					callback('E')
				");
				Assert.AreEqual("ABCDE", string.Concat(calls.ToArray()));
			}
		}
		[TestMethod] public void StateTracking()
		{
			using (var lua = new Lua())
			{
				lua.RegisterFunction("outside", new Action(() => Assert.IsFalse(lua.IsThreadRunning)));
				lua.RegisterFunction("inside", new Action(() => Assert.IsTrue(lua.IsThreadRunning)));
				lua.DoString(@"
					outside()
					co = coroutine.create(function()
						inside()
						coroutine.yield()
						inside()
						error('success')
					end)
					assert(coroutine.resume(co))
					outside()
					assert(not coroutine.resume(co))
					outside()
				");
			}
		}
		[TestMethod] public void Native()
		{
			using (var lua = new Lua())
			{
				var calls = new List<string>(4);
				LuaThread thread = null;
				lua.RegisterFunction("callback", new Action<string>(s => {
					calls.Add(s);
					Assert.AreEqual(LuaCoStatus.Running, thread.Status);
				}));
				using (var body = lua.LoadString(@"
					assert(... == 'start')
					callback('B')
					assert(coroutine.yield('yield') == 'resume')
					callback('D')
					return 'return'
				"))
					thread = lua.NewThread(body);
				using (thread)
				{
					Assert.AreEqual(LuaCoStatus.Suspended, thread.Status);
					calls.Add("A");
					CollectionAssert.AreEqual(new object[]{"yield"}, thread.Resume("start"));
					Assert.AreEqual(LuaCoStatus.Suspended, thread.Status);
					calls.Add("C");
					CollectionAssert.AreEqual(new object[]{"return"}, thread.Resume("resume"));
					Assert.AreEqual(LuaCoStatus.Dead, thread.Status);
					calls.Add("E");
				}
				Assert.AreEqual("ABCDE", string.Concat(calls.ToArray()));
			}
		}
	}
}