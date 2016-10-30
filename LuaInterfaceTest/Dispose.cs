using System;
using System.Runtime.CompilerServices;
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

	static class GcUtil
	{
		/// <summary>Ask the GC to collect and verify that it did something. Asserts that there were no Lua leaks.</summary>
		public static void GcAll()
		{
			ExecuteAndKill(() => {
				var o = new object();
				while (GC.GetGeneration(o) < GC.MaxGeneration)
					GcCollect();
				var gc = new GCTest(o);
				unref(ref o);
				return gc;
			});
			#if DEBUG
			Assert.AreEqual(0u, Lua.PopLeakCount(), "Lua objects were leaked.");
			#endif
		}

		/// <summary>Ask the GC to collect.</summary>
		public static void GcCollect()
		{
			GC.Collect();
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
			GC.WaitForPendingFinalizers();
		}

		/// <summary>Tests whether a specific object has been garbage collected.</summary>
		public struct GCTest
		{
			readonly WeakReference _weakref;
			public GCTest(object o) { _weakref = new WeakReference(o); }
			public void AssertAlive() { Assert.IsTrue(_weakref.IsAlive, "The object was garbage collected."); }
			public void AssertDead() { Assert.IsFalse(_weakref.IsAlive, "The object was not garbage collected."); }
			public void Kill()
			{
				for (int i = 0; i < 10; ++i)
				{
					GcCollect();
					if (!_weakref.IsAlive)
						break;
				}
				this.AssertDead();
			}
		}

		/// <summary>Reliably sets a variable to null, preventing dead store optimization.</summary>
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void unref<T>(ref T variable) where T : class
		{
			Assert.IsNotNull(variable);
			variable = null;
		}

		/// <summary>Calls the provided method and returns. The method is guaranteed not to be inlined.</summary>
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void Execute(Action a) { a(); }

		/// <summary>Calls the provided method, calls <see cref="GCTest.Kill"/> on the returned value, and returns. The method is guaranteed not to be inlined.</summary>
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void ExecuteAndKill(Func<GCTest> a) { a().Kill(); }

		/// <summary>Calls a method and returns a <see cref="GCTest"/> for the method's result. The method is guaranteed not to be inlined.</summary>
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static GCTest NewTest(Func<object> factory) { return new GCTest(factory()); }
	}
	[TestClass] public class Dispose
	{
		[TestCleanup] public void Cleanup()
		{
			GcUtil.GcAll();
		}

		[TestMethod] public void DisposeLua()
		{
			GcUtil.ExecuteAndKill(() =>
			{
				var lua = new Lua();
				var gc = new GcUtil.GCTest(lua);
				lua.Dispose();
				lua.Dispose();
				GcUtil.unref(ref lua);
				return gc;
			});
		}
		[TestMethod] public void FinalizeLua()
		{
			var gc = GcUtil.NewTest(() => new Lua());
			gc.Kill();
			#if DEBUG
			Assert.AreEqual(1u, Lua.PopLeakCount());
			#endif
		}

		[TestMethod] public void DisposeLuaTable()
		{
			var lua = new Lua();
			var table = lua.NewTable();
			var gc = new GcUtil.GCTest(table);
			table.Dispose();
			table.Dispose();
			GcUtil.unref(ref table);
			gc.Kill();
			lua.Dispose();
			lua.Dispose();

			// dispose in the wrong order
			lua = new Lua();
			table = lua.NewTable();
			gc = new GcUtil.GCTest(table);
			lua.Dispose();
			lua.Dispose();
			table.Dispose();
			table.Dispose();
			GcUtil.unref(ref table);
			gc.Kill();
		}
	}
}