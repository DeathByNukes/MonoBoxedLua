using System;
using System.Diagnostics;

namespace LuaInterface
{
	/// <summary>Base class for all Lua object references.</summary>
	public abstract class LuaBase : IDisposable
	{
		protected LuaBase(int reference, Lua interpreter)
		{
			Debug.Assert(interpreter != null);
			_Reference = reference;
			_Interpreter = interpreter;
		}
		protected readonly int _Reference;
		private Lua __Interpreter;

		protected Lua _Interpreter
		{
			get
			{
				Debug.Assert(__Interpreter != null, string.Format("{0} used after disposal.", this.GetType().Name));
				return __Interpreter;
			}
			private set { __Interpreter = value; }
		}
		public bool IsDisposed { get { return __Interpreter == null; } }

		/// <summary>The Lua instance that contains the referenced object.</summary>
		public Lua Parent { get { return _Interpreter; } }

		~LuaBase()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (this.IsDisposed) return;
			if (_Reference >= LuaRefs.Min)
				_Interpreter.dispose(_Reference);
			_Interpreter = null;
			//if (disposing)
			//	/* dispose managed objects here */;
		}

		public override bool Equals(object o)
		{
			var l = o as LuaBase;
			if (l == null) return false;
			return _Interpreter.compareRef(l._Reference, _Reference);
		}

		public override int GetHashCode()
		{
			return 0; // getting a safe hash code is impossible. lua_equal allows the script to implement custom equality via metatables
		}
	}
}