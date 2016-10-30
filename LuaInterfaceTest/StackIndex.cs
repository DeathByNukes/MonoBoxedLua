using System;
using System.Linq;
using LuaInterface;
using LuaInterface.LuaAPI;

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

	[TestClass] public class StackIndexTest
	{
		[TestCleanup] public void Cleanup()
		{
			GcUtil.GcAll();
		}

		[TestMethod] public void Test()
		{
			using (var l = new Lua())
			{
				var L = luanet.getstate(l);

				GC.KeepAlive(L[1].DebuggerValue);
				Assert.AreEqual(0, L.Length);
				Assert.AreEqual(2, L.Count);
				Assert.AreEqual(2, L.Sum(si=>si.Type == LUA.T.NONE ? 0 : 1));

				lua.createtable(L, 1, 0);
				Assert.AreEqual(1, L.Length);
				foreach (var x in L) {}
				StackIndex i1 = L[1];
				Assert.AreEqual(1, i1.Index);
				Assert.AreEqual(0UL, i1.Length);
				Assert.AreEqual(LUA.T.TABLE, i1.Type);
				GC.KeepAlive(i1.Value);

				lua.pushnumber(L, 12);
				lua.rawseti(L, -2, 1);
				StackIndexChild i11 = i1[1];
				Assert.AreEqual(i1, i11.Parent);
				Assert.AreEqual(LUA.T.NUMBER, i11.Type);
				Assert.AreEqual(0UL, i11.Length);
				GC.KeepAlive(i11.Value);

				lua.settop(L, 0);
				Assert.AreEqual(0, L.Length);

				try { GC.KeepAlive(i11.Type); Assert.Fail(); } catch (LuaScriptException) {}

				GC.KeepAlive(L[LUA.REGISTRYINDEX].Value);

				Assert.AreEqual(0, L.Length);
			}
		}
		[TestMethod] public void InFunction()
		{
			using (var l = new Lua())
			{
				var L = luanet.getstate(l);

				lua.cpcall(L, L2 =>
				{
					Assert.AreEqual(1, L2.Length);
					lua.pop(L2, 1);
					Assert.AreEqual(3, L.Sum(si=>si.Type == LUA.T.NONE ? 0 : 1));
					Assert.AreEqual(LUA.T.NONE, L2[lua.upvalueindex(1)].Type);
					return 0;
				}, default(IntPtr));
				Assert.AreEqual(0, L.Length);

				lua.CFunction cf = L2 =>
				{
					Assert.AreEqual(0, L2.Length);
					Assert.AreEqual(4, L2.Sum(si=>si.Type == LUA.T.NONE ? 0 : 1));
					Assert.AreEqual(LUA.T.BOOLEAN, L2[lua.upvalueindex(1)].Type);
					return 0;
				};
				lua.pushboolean(L, true);
				lua.pushcclosure(L, cf, 1);
				lua.call(L, 0, 0);

				GC.KeepAlive(cf);
				Assert.AreEqual(0, L.Length);
			}
		}
	}
}