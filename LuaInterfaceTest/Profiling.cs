#if false
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

	[TestClass] public class Profiling
	{
		[MethodImpl(MethodImplOptions.NoInlining)] static int Noop(lua.State L) { return 0; }

		static void Display(Stopwatch timer, string description)
		{
			Trace.WriteLine(string.Format("{0}: {1}ms", description, timer.ElapsedMilliseconds));
		}

		[TestMethod] public void CFunctions()
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

				#if false
				#pragma warning disable 618
				luanet.CSFunction cs_function = Noop;
				luanet.pushstdcallcfunction(L, cs_function);
				lua.setglobal(L, "Noop");

				lua.pushvalue(L, -1);
				timer.Reset();timer.Start();
				lua.call(L, 0, 0);
				timer.Stop();
				Display(timer, "Lua -> stdcall -> CLR");

				GC.KeepAlive(cs_function);
				#pragma warning restore 618
				#endif


				lua.CFunction c_function = Noop;
				lua.pushcfunction(L, c_function);
				lua.setglobal(L, "Noop");

				lua.pushvalue(L, -1);
				timer.Reset();timer.Start();
				lua.call(L, 0, 0);
				timer.Stop();
				Display(timer, "Lua -> cdecl -> CLR");

				GC.KeepAlive(c_function);


				luaL.dostring(L, "function Noop() end");

				lua.pushvalue(L, -1);
				timer.Reset();timer.Start();
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

		[TestMethod] public void GCHandles()
		{
			const uint iterations = 2048*2048;
			Trace.WriteLine(iterations.ToString("N0") + " iterations");

			const int pool_size = 100;

			var pool = new Dictionary<int, object>();
			var indices = new int[pool_size];
			for (int i = 0; i < indices.Length; ++i)
				pool[indices[i] = i] = new object();

			var handles = new IntPtr[pool_size];
			for (int i = 0; i < handles.Length; ++i)
				handles[i] = GCHandle.ToIntPtr(GCHandle.Alloc(new object()));


			var timer = Stopwatch.StartNew();
			for (uint n = 0; n < iterations; ++n)
			for (int i = 0; i < indices.Length; ++i)
				GC.KeepAlive(pool[indices[i]]);
			timer.Stop();
			Display(timer, "Dictionary");


			timer.Reset();timer.Start();
			for (uint n = 0; n < iterations; ++n)
			for (int i = 0; i < handles.Length; ++i)
				GC.KeepAlive(GCHandle.FromIntPtr(handles[i]).Target);
			timer.Stop();
			Display(timer, "GCHandle");

				
			for (int i = 0; i < handles.Length; ++i)
				GCHandle.FromIntPtr(handles[i]).Free();

			/*
				4,194,304 iterations
				Dictionary: 6266ms
				GCHandle: 5090ms

				release mode:
				4,194,304 iterations
				Dictionary: 5580ms
				GCHandle: 4731ms

				(each iteration is a pool of 100)
			*/
		}

		[TestMethod] public void NewUserdata()
		{
			using (var luainterface = new Lua())
			{
				const int iterations = 2048*2048;
				Trace.WriteLine(iterations.ToString("N0") + " iterations");

				var o = new object();
				var L = luanet.getstate(luainterface);
				lua.createtable(L, iterations, 0);

				var timer = Stopwatch.StartNew();
				for (int n = 0; n < iterations; ++n)
				{
					luaclr.newref(L, o);
					luaclr.newrefmeta(L, "new userdata test object", 0);
					lua.setmetatable(L, -2);
					lua.rawseti(L, -2, n+1);
				}
				timer.Stop();
				Display(timer, "new refs added to table");

				/*
					4,194,304 iterations
					new refs added to table: 5169ms

					release mode:
					4,194,304 iterations
					new refs added to table: 2049ms
				*/
			}
		}

		[TestMethod] public unsafe void Callback()
		{
			const uint iterations = 2048*2048;
			using (var test = new CallbackTest())
			{
				Trace.WriteLine(iterations.ToString("N0") + " iterations");

				// jit
				CallbackTest.MakeNoop(0)();
				test.CallViaThunk(CallbackTest.MakeNoop(0));
				test.CallViaGchandle(CallbackTest.MakeNoop(0));

				var timer = Stopwatch.StartNew();
				for (uint i = 0; i < iterations; ++i)
					CallbackTest.MakeNoop(i)();
				timer.Stop();
				Display(timer, "bare call");


				timer.Reset();timer.Start();
				for (uint i = 0; i < iterations; ++i)
					test.CallViaThunk(CallbackTest.MakeNoop(i));
				timer.Stop();
				Display(timer, "CallViaThunk");


				timer.Reset();timer.Start();
				for (uint i = 0; i < iterations; ++i)
					test.CallViaGchandle(CallbackTest.MakeNoop(i));
				timer.Stop();
				Display(timer, "CallViaGchandle");

				/*
					4,194,304 iterations
					bare call: 51ms
					CallViaThunk: 4468ms
					CallViaGchandle: 1911ms
				*/
			}
		}
		class CallbackTest : IDisposable
		{
			[MethodImpl(MethodImplOptions.NoInlining)] public static Action MakeNoop(uint i) { return () => { Noop(i); }; }
			[MethodImpl(MethodImplOptions.NoInlining)] public static void Noop(uint i) {}

			public CallbackTest()
			{
				var interpreter = _interpreter = new Lua();
				_L = luanet.getstate(interpreter);
				_call_via_gchandle_f = CallViaGchandleF;
			}
			readonly Lua _interpreter;
			lua.State _L;
			public void Dispose() { _interpreter.Dispose(); _L = default(lua.State); }

			public void CallViaThunk(Action function)
			{
				if (function == null) throw new ArgumentNullException("function");
				// todo: is it more performant to actually pass a GCHandle in through the ud parameter versus creating and GCing delegate thunks?
				lua.CFunction wrapper = L =>
				{
					lua.settop(L, 0);
					// no need for luanet.entercfunction here because it's guaranteed to be the same state
					try { function(); }
					catch (LuaInternalException) { throw; }
					catch (Exception ex) { luanet.error(L, _interpreter, ex); }
					return 0;
				};
				{
					var L = _L;
					if (lua.cpcall(L, wrapper, default(IntPtr)) != LUA.ERR.Success)
						throw luanet.exceptionfromerror(L, _interpreter, -2);
				}
			}

			public unsafe void CallViaGchandle(Action function)
			{
				if (function == null) throw new ArgumentNullException("function");
				var L = _L;
				var handle = GCHandle.Alloc(function);
				var status = lua.cpcall(L, _call_via_gchandle_f, GCHandle.ToIntPtr(handle));
				handle.Free();
				if (status != 0)
					throw luanet.exceptionfromerror(L, _interpreter, -2);
			}
			readonly lua.CFunction _call_via_gchandle_f;
			unsafe int CallViaGchandleF(lua.State L)
			{
				Debug.Assert(luanet.infunction(L) && L == this._L);
				var function = (Action) GCHandle.FromIntPtr(new IntPtr(lua.touserdata(L, 1))).Target;
				lua.settop(L, 0);
				// no need for luanet.entercfunction here because it's guaranteed to be the same state
				try { function(); }
				catch (LuaInternalException) { throw; }
				catch (Exception ex) { luanet.error(L, _interpreter, ex); }
				return 0;
			}
		}

		[TestMethod] public void Indexer()
		{
			const uint iterations = 128*1024;
			using (var li = new Lua())
			{
				var L = luanet.getstate(li);
				Trace.WriteLine(iterations.ToString("N0") + " iterations");

				const int pool_size = 100;

				lua.createtable(L, pool_size, 0);
				lua.createtable(L, 0, pool_size);
				lua.createtable(L, 0, pool_size);
				var i_int = new int[pool_size];
				var i_str = new string[pool_size];
				var i_lud = new IntPtr[pool_size];
				for (int i = 0; i < pool_size; ++i)
				{
					lua.pushboolean(L, true);
					lua.rawseti(L, 1, i_int[i] = i);

					lua.pushboolean(L, true);
					lua.setfield(L, 2, i_str[i] = i.ToString());

					lua.pushboolean(L, true);
					lua.pushlightuserdata(L, i_lud[i] = new IntPtr(i));
					lua.settable(L, 3);
				}


				var timer = Stopwatch.StartNew();
				for (uint n = 0; n < iterations; ++n)
				for (int i = 0; i < i_int.Length; ++i)
				{
					lua.rawgeti(L, 1, i_int[i]);
					lua.pop(L, 1);
				}
				timer.Stop();
				Display(timer, "int");

				
				timer.Reset();timer.Start();
				for (uint n = 0; n < iterations; ++n)
				for (int i = 0; i < i_str.Length; ++i)
				{
					lua.getfield(L, 2, i_str[i]);
					lua.pop(L, 1);
				}
				timer.Stop();
				Display(timer, "string");

				
				timer.Reset();timer.Start();
				for (uint n = 0; n < iterations; ++n)
				for (int i = 0; i < i_lud.Length; ++i)
				{
					lua.pushlightuserdata(L, i_lud[i]);
					lua.rawget(L, 3);
					lua.pop(L, 1);
				}
				timer.Stop();
				Display(timer, "lightuserdata");

				/*
					131,072 iterations
					int: 1824ms
					string: 4082ms
					lightuserdata: 3005ms

					release mode:
					131,072 iterations
					int: 471ms
					string: 1316ms
					lightuserdata: 729ms
				*/
			}
		}
	}
}
#endif