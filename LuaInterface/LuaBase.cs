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
			Owner = interpreter;
		}
		protected readonly int _Reference;

		/// <summary>The Lua instance that contains the referenced object.</summary>
		public Lua Owner
		{
			get
			{
				Debug.Assert(_interpreter != null, string.Format("{0} used after disposal.", this.GetType().Name));
				return _interpreter;
			}
			private set { _interpreter = value; }
		}
		public bool IsDisposed { get { return _interpreter == null; } }
		private Lua _interpreter; // should only be directly accessed by the above two members

		~LuaBase()
		{
			Dispose(false);
			Lua.leaked();
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
				Owner.dispose(_Reference);
			Owner = null;
			//if (disposing)
			//	/* dispose managed objects here */;
		}

		public override bool Equals(object o)
		{
			var l = o as LuaBase;
			if (l == null) return false;
			return Owner.compareRef(l._Reference, _Reference);
		}

		public override int GetHashCode()
		{
			return 0; // getting a safe hash code is impossible. lua_equal allows the script to implement custom equality via metatables
		}
	}
}