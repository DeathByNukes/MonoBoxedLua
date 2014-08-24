using System;
using LuaInterface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LuaInterfaceTest
{
	[TestClass] public class Dispose
	{
		private class GCTest
		{
			readonly WeakReference _weakref;
			public GCTest(object o) { _weakref = new WeakReference(o); }
			public void AssertAlive() { Assert.IsTrue(_weakref.IsAlive); }
			public void AssertDead() { Assert.IsFalse(_weakref.IsAlive); }
			public void Kill()
			{
				GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
				GC.WaitForPendingFinalizers();
				this.AssertDead();
			}
		}
		private void Unref<T>(ref T variable) where T : class
		{
			Assert.IsNotNull(variable);
			variable = null;
			Assert.IsNull(variable); // prevent optimization
		}

		[TestMethod] public void DisposeLua()
		{
			var lua = new Lua();
			var gc = new GCTest(lua);
			lua.Dispose();
			lua.Dispose();
			Unref(ref lua);
			gc.Kill();
		}
		[TestMethod] public void FinalizeLua()
		{
			var gc = new GCTest(new Lua());
			gc.Kill();
		}

		[TestMethod] public void DisposeLuaTable()
		{
			var lua = new Lua();
			var table = lua.NewTable();
			var gc = new GCTest(table);
			table.Dispose();
			table.Dispose();
			Unref(ref table);
			gc.Kill();
			lua.Dispose();
			lua.Dispose();

			// dispose in the wrong order
			lua = new Lua();
			table = lua.NewTable();
			gc = new GCTest(table);
			lua.Dispose();
			lua.Dispose();
			table.Dispose();
			table.Dispose();
			Unref(ref table);
			gc.Kill();
		}
	}
}