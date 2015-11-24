using System;
using System.Diagnostics;
using LuaInterface.LuaAPI;

namespace LuaInterface
{
	/// <summary>Base class for all Lua object references.</summary>
	public abstract class LuaBase : IDisposable
	{
		#region Constructor / State

		protected LuaBase(int reference, Lua interpreter)
		{
			Debug.Assert(interpreter != null);
			Reference = reference;
			Owner = interpreter;
		}

		protected readonly int Reference;

		/// <summary>The Lua instance that contains the referenced object.</summary>
		public Lua Owner
		{
			get
			{
				if (_interpreter == null)
				{
					//Debug.Assert(false, "LuaBase used after disposal!");
					throw new ObjectDisposedException(this.GetType().Name);
				}
				return _interpreter;
			}
			private set { _interpreter = value; }
		}
		public bool IsDisposed { get { return _interpreter == null; } }
		private Lua _interpreter; // should only be directly accessed by the above two members

		~LuaBase()
		{
			Dispose(false);
			Lua.leaked(Reference);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (this.IsDisposed) return;
			if (Reference >= LuaRefs.Min && !Owner._L.IsNull)
				luaL.unref(Owner._L, Reference);
			Owner = null;
			//if (disposing)
			//	/* dispose managed objects here */;
		}

		#endregion

		#region Type Safety

		/// <summary>[-0, +1, m] Push the referenced object onto the stack.</summary>
		/// <remarks>
		/// No default implementation is provided because all implementers should be validating the type (see <see cref="CheckType(lua.State,LuaType)"/>)
		/// If you throw an exception you should leave the stack how it was.
		/// DO NOT call <see cref="rawpush"/> from within your implementation; <see cref="rawpush"/> redirects to <see cref="push"/> in debug builds.
		/// </remarks>
		/// <exception cref="InvalidCastException">Might be thrown if the registry was tampered with or <paramref name="L"/> isn't this reference's owner. Don't bother catching this exception. It should never happen unless you're doing it wrong or there was a security breach.</exception>
		protected internal abstract void push(lua.State L);

		/// <summary>[-0, +1, -] Push the referenced object onto the stack without verifying its type matches the class. For internal use only. Should only be used when calling lua functions that can safely accept any type.</summary>
		/// <remarks>DO NOT call this from within your <see cref="push"/> implementation; it redirects to <see cref="push"/> in debug builds.</remarks>
		protected virtual void rawpush(lua.State L)
		{
			#if DEBUG
			try { push(L); return; }
			catch (Exception ex) { Debug.Fail(ex.ToString()); }
			#endif
			luaL.getref(L, Reference);
		}

		/// <summary>
		/// [-1, +0, v] Pops a value from the top of the stack and returns a reference or throws if it doesn't match the provided type.
		/// Validating the type is critically important because some Lua functions don't check the type at all, potentially corrupting memory when given unexpected input.
		/// </summary>
		protected static int TryRef(lua.State L, Lua interpreter, LuaType t)
		{
			Debug.Assert(L == interpreter._L);
			var actual = lua.type(L,-1);
			if (actual == t)
				return luaL.@ref(L);
			else
			{
				lua.pop(L, 1);
				throw NewBadTypeError(typeof(LuaBase).Name, t, actual);
			}
		}
		/// <summary>
		/// [-0, +0, v] Checks that the instance references the given type. If it doesn't, the instance is disposed and an exception is thrown.
		/// Validating the type is critically important because some Lua functions don't check the type at all, potentially corrupting memory when given unexpected input.
		/// </summary>
		protected void CheckType(LuaType t)
		{
			var L = Owner._L;
			luaL.getref(L, Reference);
			var actual = lua.type(L,-1);
			lua.pop(L,1);
			if (actual != t)
			{
				Dispose();
				throw NewBadTypeError(t, actual);
			}
		}
		/// <summary>
		/// [-(0|1), +0, v] Checks that the value on top of the stack is the given type. If it isn't then the value is popped, the instance is disposed, and an exception is thrown.
		/// Validating the type is critically important because some Lua functions don't check the type at all, potentially corrupting memory when given unexpected input.
		/// </summary>
		protected void CheckType(lua.State L, LuaType t)
		{
			Debug.Assert(L == Owner._L);
			var actual = lua.type(L,-1);
			if (actual == t) return;
			lua.pop(L, 1);
			Dispose();
			throw NewBadTypeError(t, actual);
		}
		/// <summary>Exception factory for use when a <see cref="LuaBase"/> object references a Lua value of an incorrect type.</summary>
		protected static InvalidCastException NewBadTypeError(string type, object expected, object actual) {
			return new InvalidCastException(string.Format("{0} created with a {2} reference. ({1} expected)", type, expected, actual));
		}
		/// <summary>Exception factory for use when a <see cref="LuaBase"/> object references a Lua value of an incorrect type.</summary>
		protected InvalidCastException NewBadTypeError(object expected, object actual) {
			return NewBadTypeError(this.GetType().Name, expected, actual);
		}

		#endregion

		#region .NET object implementation

		/// <summary>Raw equality. (For table field lookups, Lua uses raw comparisons internally.)</summary>
		public override bool Equals(object o)
		{
			return Equals(o as LuaBase);
		}

		/// <summary>Raw equality. (For table field lookups, Lua uses raw comparisons internally.)</summary>
		public bool Equals(LuaBase o)
		{
			if (o == null || o.Owner != Owner) return false;
			var L = Owner._L;
			rawpush(L);
			o.rawpush(L);
			bool ret = lua.rawequal(L, -1, -2);
			lua.pop(L,2);
			return ret;
		}

		public unsafe override int GetHashCode()
		{
			var L = Owner._L;
			rawpush(L);
			void* ptr = lua.topointer(L, -1);
			lua.pop(L,1);

			if (sizeof(IntPtr) == sizeof(int))
				return (int)ptr;
			else
				return ((ulong)ptr).GetHashCode();
		}

		/// <summary>Full Lua equality which can be controlled with metatables.</summary>
		public bool LuaEquals(LuaBase o)
		{
			if (o == null || o.Owner != Owner) return false;
			var L = Owner._L;
			rawpush(L);
			o.rawpush(L);
			try { return lua.equal(L, -1, -2); }
			finally { lua.pop(L,2); }
		}

		/// <summary>Full Lua equality which can be controlled with metatables.</summary>
		public static bool operator ==(LuaBase left, LuaBase right)
		{
			return (object)left == null
				? (object)right == null
				: left.LuaEquals(right);
		}

		/// <summary>Full Lua equality which can be controlled with metatables.</summary>
		public static bool operator !=(LuaBase left, LuaBase right)
		{
			return (object)left == null
				? (object)right != null
				: !left.LuaEquals(right);
		}

		public override string ToString()
		{
			var L = Owner._L;
			luaL.getref(L, Owner.tostring_ref);
			rawpush(L);
			if (lua.pcall(L, 1, 1, 0) != LuaStatus.Ok)
				throw Owner.ExceptionFromError(-2); // -2, pop the error from the stack
			var str = lua.tostring(L, -1);
			lua.pop(L,1);
			return str ?? "";
		}

		#endregion

		#region Indexers

		/// <summary>Indexer for nested string fields of the Lua object.</summary>
		public object this[params string[] path]
		{
			get
			{
				var L = Owner._L;                         StackAssert.Start(L);
				rawpush(L);
				var ret = Owner.getNestedObject(-1, path);
				lua.pop(L,1);                             StackAssert.End();
				return ret;
			}
			set
			{
				var L = Owner._L;                         StackAssert.Start(L);
				rawpush(L);
				Owner.setNestedObject(-1, path, value);
				lua.pop(L,1);                             StackAssert.End();
			}
		}

		/// <summary>Indexer for string fields of the Lua object.</summary>
		public object this[string field]
		{
			get
			{
				var L = Owner._L;                         StackAssert.Start(L);
				rawpush(L);
				lua.getfield(L, -1, field);
				var obj = Owner.translator.getObject(L,-1);
				lua.pop(L,2);                             StackAssert.End();
				return obj;
			}
			set
			{
				var L = Owner._L;                         StackAssert.Start(L);
				rawpush(L);
				Owner.translator.push(L,value);
				lua.setfield(L, -2, field);
				lua.pop(L,1);                             StackAssert.End();
			}
		}

		/// <summary>Indexer for numeric fields of the Lua object.</summary>
		public object this[object field]
		{
			get
			{
				var L = Owner._L;                         StackAssert.Start(L);
				rawpush(L);
				Owner.translator.push(L,field);
				lua.gettable(L,-2);
				var obj = Owner.translator.getObject(L,-1);
				lua.pop(L,2);                             StackAssert.End();
				return obj;
			}
			set
			{
				var L = Owner._L;                         StackAssert.Start(L);
				rawpush(L);
				Owner.translator.push(L,field);
				Owner.translator.push(L,value);
				lua.settable(L,-3);
				lua.pop(L,1);                             StackAssert.End();
			}
		}

		/// <summary>Looks up the field and checks for a nil result. The field's value is discarded without performing any Lua to CLR translation.</summary>
		public bool ContainsKey(object field) {
			return this.FieldType(field) != LuaType.Nil;
		}

		/// <summary>Looks up the field and gets its Lua type. The field's value is discarded without performing any Lua to CLR translation.</summary>
		public LuaType FieldType(object field)
		{
			var L = Owner._L;                         StackAssert.Start(L);
			push(L);
			Owner.translator.push(L, field);
			lua.gettable(L,-2);
			var type = lua.type(L, -1);
			lua.pop(L,2);                             StackAssert.End();
			return type;
		}

		#endregion

		#region Call

		/// <summary>Calls the object and returns its return values inside an array.</summary>
		public object[] Call(params object[] args)
		{
			return this.call(args, null);
		}

		/// <summary>Calls the function casting return values to the types in returnTypes</summary>
		internal object[] call(object[] args, Type[] returnTypes)
		{
			var L = Owner._L; var translator = Owner.translator;
			int nArgs = args==null ? 0 : args.Length;
			int oldTop=lua.gettop(L);
			if(!lua.checkstack(L,nArgs+6)) // todo: why 6?
				throw new LuaException("Lua stack overflow");

			translator.push(L,this);

			for(int i = 0; i < nArgs; ++i)
				translator.push(L,args[i]);

			++Owner._executing;
			var status = lua.pcall(L, nArgs, returnTypes == null ? LUA.MULTRET : returnTypes.Length, 0);
			checked { --Owner._executing; }
			if (status != LuaStatus.Ok)
				throw Owner.ExceptionFromError(oldTop);

			return translator.popValues(L, oldTop, returnTypes);
		}

		#endregion

		#region Metatable

		/// <summary>Gets/sets the object's metatable. Note that only tables and userdata have individual metatables; setting a function's metatable will alter every function.</summary>
		public LuaTable Metatable
		{
			get
			{
				var L = Owner._L;                         StackAssert.Start(L);
				push(L);
				if (lua.getmetatable(L,-1))
				{
					lua.remove(L, -2);                        StackAssert.End(1);
					return new LuaTable(L, Owner);
				}
				lua.pop(L,1);                             StackAssert.End();
				return null;
			}
			set
			{
				var L = Owner._L;
				var oldTop = lua.gettop(L);
				push(L);
				try
				{
					if (value == null) lua.pushnil(L);
					else value.push(L);
					lua.setmetatable(L, -2);
				}
				finally { lua.settop(L, oldTop); }
			}
		}

		#endregion
	}
}