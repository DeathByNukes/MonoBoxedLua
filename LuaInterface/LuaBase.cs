using System;
using System.Diagnostics;
using System.Linq;

namespace LuaInterface
{
	/// <summary>
	/// Base class to provide consistent disposal flow across lua objects.
	/// </summary>
	public abstract class LuaBase : IDisposable
	{
		protected LuaBase(int reference, Lua interpreter)
		{
			Debug.Assert(interpreter != null);
			_Reference = reference;
			_Interpreter = interpreter;
		}
		protected readonly int _Reference;
		/// <summary>This is set to null when the object is disposed.</summary>
		protected Lua _Interpreter { get; private set; }

		~LuaBase()
		{
			if (Debugger.IsAttached)
			{
				if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "LuaInterfaceTest"))
				{
					// running a unit test. breaking causes the test to freeze. instead, might as well throw an exception
					throw new InvalidOperationException("Lua object was not disposed.");
				}
				else
					Debugger.Break(); // you failed to dispose of an IDisposable. shame on you.
			}
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (_Interpreter == null) return;
			if (_Reference != 0)
				_Interpreter.dispose(_Reference);
			_Interpreter = null;
			//if (disposing)
			//	/* dispose managed objects here */;
		}

		public override bool Equals(object o)
		{
			if (_Interpreter == null) throw new ObjectDisposedException("LuaBase");
			var l = o as LuaBase;
			if (l == null) return false;
			return _Interpreter.compareRef(l._Reference, _Reference);
		}

		public override int GetHashCode()
		{
			return _Reference;
		}
	}
}