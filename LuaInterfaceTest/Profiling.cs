using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using LuaInterface;
using LuaInterface.LuaAPI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LuaInterfaceTest
{
	[TestClass]
	public class Profiling
	{
		[MethodImpl(MethodImplOptions.NoInlining)] static int Noop(lua.State L) { return 0; }

		static void Display(Stopwatch timer, string description)
		{
			Trace.WriteLine(string.Format("{0}: {1}ms", description, timer.ElapsedMilliseconds));
		}

		[TestMethod]
		public void CFunctions()
		{
			const uint iterations = 2048*2048;
			using (var li = new Lua())
			{
				var L = luanet.getstate(li);
				Trace.WriteLine(iterations.ToString("N0") + " iterations");
				Noop(L); // jit


				var timer = Stopwatch.StartNew();
				for (uint i = 0; i < iterations; ++i)
					Noop(L);
				timer.Stop();
				Display(timer, "CLR -> CLR");


				luaL.loadstring(L, @"
					local Noop = Noop
					for i=1,"+iterations+@" do
						Noop()
					end
				");

				luanet.CSFunction cs_function = Noop;
				luanet.pushstdcallcfunction(L, cs_function);
				lua.setglobal(L, "Noop");

				lua.pushvalue(L, -1);
				timer.Restart();
				lua.call(L, 0, 0);
				timer.Stop();
				Display(timer, "Lua -> stdcall -> CLR");

				GC.KeepAlive(cs_function);


				lua.CFunction c_function = Noop;
				lua.pushcfunction(L, c_function);
				lua.setglobal(L, "Noop");

				lua.pushvalue(L, -1);
				timer.Restart();
				lua.call(L, 0, 0);
				timer.Stop();
				Display(timer, "Lua -> cdecl -> CLR");

				GC.KeepAlive(c_function);


				luaL.dostring(L, "function Noop() end");

				lua.pushvalue(L, -1);
				timer.Restart();
				lua.call(L, 0, 0);
				timer.Stop();
				Display(timer, "Lua -> Lua");

				/*
					4,194,304 iterations
					CLR -> CLR: 12ms
					Lua -> stdcall -> CLR: 731ms
					Lua -> cdecl -> CLR: 420ms
					Lua -> Lua: 468ms
				*/
			}
		}
	}
}
