
//#define TRACE
#undef TRACE

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LuaInterface.LuaAPI
{
	using Dbg = Debug;

	[DebuggerDisplay("{DebuggerValue}", Name = "[{DebuggerKey}]")]
	public struct StackIndex : IEnumerable<StackIndexChild>
	{
		const DebuggerBrowsableState NO = DebuggerBrowsableState.Never;
		[DebuggerBrowsable(NO)] public readonly lua.State L;
		[DebuggerBrowsable(NO)] public readonly int Index;
		public StackIndex(lua.State L, int index)
		{
			//Trace.TraceInformation("new StackIndex({0},{1})", L, index);
			L.NullCheck("L");
			if (index == 0) throw new ArgumentOutOfRangeException("index");
			this.L = L; this.Index = index;
		}

		public luanet.IndexTypes IndexType { get { return luanet.indextype(Index); } }
		public LUA.T             Type      { get { _log("Type");  L.NullCheck(); return lua.type(L, Index); } }
		public LuaValue          Value     { get { _log("Value"); L.NullCheck(); return LuaValue.read(L, Index, true); } }

		public void push() { _log("push()"); L.NullCheck(); lua.pushvalue(L, Index); }

		public override string ToString() { _log("ToString()"); return string.Format("[{0}] {1}", this.DebuggerKey, Type); }

		#region DebuggerKey

		[DebuggerBrowsable(NO)]
		public object DebuggerKey { get
		{
			switch (this.IndexType)
			{
			case luanet.IndexTypes.Pseudo:
				return (_PseudoIndices) this.Index;
			case luanet.IndexTypes.Upvalue:
				return new _OpaqueObject("lua.upvalueindex("+lua.upvalueindex(this.Index)+")"); // upvalueindex is its own inverse function
			default:
				return this.Index;
			}
		}}
		// change the quote marks around strings into brackets
		class _OpaqueObject
		{
			string _value;
			public _OpaqueObject(string value) { _value = value; }
			public override string ToString() { return _value; }
		}
		// remove quote marks for specific values
		enum _PseudoIndices
		{
			LUA_REGISTRYINDEX = LUA.REGISTRYINDEX,
			LUA_ENVIRONINDEX  = LUA.ENVIRONINDEX,
			LUA_GLOBALSINDEX  = LUA.GLOBALSINDEX,
		}

		#endregion

		[DebuggerBrowsable(NO)] public object DebuggerValue { get { return this.Value.Box(); } }

		[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
		public List<StackIndexChild> DebuggerContents { get { return this.ToList(); } }

		void _log_internal(string s) { Trace.TraceInformation("StackIndex({0},{1}).{2}", L, Index, s); }
		[Conditional("TRACE")] void _log(string member) { _log_internal(member); }
		[Conditional("TRACE")] void _log(string format, params object[] args) { _log_internal(string.Format(format, args)); }

		IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
		public IEnumerator<StackIndexChild> GetEnumerator()
		{
			_log("GetEnumerator()");
			L.NullCheck();
			var results = new List<StackIndexChild>();
			int index = luanet.absoluteindex(L, Index);
			if (index != 0 && lua.type(L, index) == LUA.T.TABLE)
			{	                                             StackAssert.Start(L);
				lua.pushnil(L);
				while (lua.next(L, index))
				{
					results.Add(new StackIndexChild(this, new[] { LuaValue.read(L, -2, true) }));
					lua.pop(L,1);
				}                                            StackAssert.End();
			}
			return results.GetEnumerator();
		}
		public ulong Length { get
		{
			_log("Length");
			L.NullCheck();
			return lua.isnumber(L, Index) ? 0 : (ulong) lua.objlen(L, Index);
		}}

		public StackIndexChild this[params LuaValue[] keys] { get { return new StackIndexChild(this, keys); } }
	}

	[DebuggerDisplay("{DebuggerValue}", Name = "[{DebuggerKey}]")]
	public struct StackIndexChild : IEnumerable<StackIndexChild>
	{
		const DebuggerBrowsableState NO = DebuggerBrowsableState.Never;
		[DebuggerBrowsable(NO)] public readonly StackIndex Parent;
		[DebuggerBrowsable(NO)] readonly LuaValue[] _keys;
		[DebuggerBrowsable(NO)] public LuaValue[] Keys { get { return (LuaValue[]) _keys.Clone(); } }

		public StackIndexChild(StackIndex parent, IEnumerable<LuaValue> keys) : this(parent, keys == null ? null : keys.ToArray()) {}
		private StackIndexChild(StackIndex parent, LuaValue[] keys)
		{
			//Trace.TraceInformation("new StackIndexChild({{{0},{1}}},{2})", parent.L, parent.Index, keys == null || keys.Length == 0 ? "null" : _keysToString(keys));
			if (keys == null || keys.Length == 0)
				throw new ArgumentException("null or empty", "keys");
			for (int i = 0; i < keys.Length-1; ++i)
				if (!keys[i].IsSupported) throw new ArgumentException("Cannot traverse "+keys[i].Type+" keys.", "keys");
			parent.L.NullCheck("parent");
			Parent = parent;
			_keys = keys;
		}

		public void push()
		{
			_keys[_keys.Length-1].VerifySupport();
			var L = Parent.L;                            StackAssert.Start(L);
			Dbg.Assert(_keys.All(k=>k.IsSupported));
			var keys = _keys;

			lua.CFunction tryBlock = L2 =>
			{
				for (int i = 0; i < keys.Length; ++i)
				{
					keys[i].push(L2);
					lua.gettable(L2, -2);
					lua.remove(L2, -2);
				}
				return 1;
			};
			// parent should be pushed first. it might be pointing to the index we're about to fill
			this.Parent.push();
			lua.pushcfunction(L, tryBlock);
			lua.insert(L, -2);
			var ex = luanet.pcall(L, 1, 1);
			if (ex != null)
			{	                                         StackAssert.End();
				Trace.TraceError("push failed: "+ex.Message);
				throw ex;
			}
			GC.KeepAlive(tryBlock);                      StackAssert.End(1);
		}

		public LUA.T Type { get
		{
			_log("Type");
			this.push();
			var L = Parent.L;
			var t = lua.type(L, -1);
			lua.pop(L, 1);
			return t;
		}}

		public LuaValue Value { get
		{
			_log("Value");
			this.push();
			return LuaValue.pop(this.Parent.L, true);
		}}

		public override string ToString()
		{
			_log("ToString()");
			return string.Format("[{0}][{1}] {2}", this.Parent.DebuggerKey, _keysToString(_keys), this.Value.ToString());
		}
		static string _keysToString(LuaValue[] keys)
		{
			var indices = new string[keys.Length];
			for (int i = 0; i < keys.Length; ++i)
				indices[i] = keys[i].ToString();
			return string.Join(",", indices);
		}

		[DebuggerBrowsable(NO)] public object DebuggerKey   { get { return _keys[_keys.Length-1].Box(); } }
		[DebuggerBrowsable(NO)] public object DebuggerValue { get { return this.Value.Box(); } }

		[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
		List<StackIndexChild> _DebuggerContents { get { return this.ToList(); } }

		void _log_internal(string s) { Trace.TraceInformation("StackIndexChild({{{0},{1}}},{2}).{3}", Parent.L, Parent.Index, _keysToString(_keys), s); }
		[Conditional("TRACE")] void _log(string member) { _log_internal(member); }
		[Conditional("TRACE")] void _log(string format, params object[] args) { _log_internal(string.Format(format, args)); }

		public IEnumerator<StackIndexChild> GetEnumerator()
		{
			_log("GetEnumerator()");
			var results = new List<StackIndexChild>();
			this.push();
			Dbg.Assert(_keys.All(k=>k.IsSupported));
			var L = Parent.L;
			if (lua.type(L, -1) == LUA.T.TABLE)
			{	                                             StackAssert.Start(L);
				lua.pushnil(L);
				while (lua.next(L, -2))
				{
					var keys = new LuaValue[_keys.Length+1];
					Array.Copy(_keys, keys, _keys.Length);
					keys[keys.Length-1] = LuaValue.read(L, -2, true);
					results.Add(new StackIndexChild(this.Parent, keys));
					lua.pop(L,1);
				}                                            StackAssert.End();
			}
			lua.pop(L,1);
			return results.GetEnumerator();
		}
		public ulong Length { get
		{
			_log("Length");
			this.push();
			var L = Parent.L;
			var len = lua.isnumber(L, -1) ? default(UIntPtr) : lua.objlen(L, -1);
			lua.pop(L, 1);
			return (ulong) len;
		}}
		IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

		public StackIndexChild this[params LuaValue[] keys] { get { return new StackIndexChild(this.Parent, _keys.Concat(keys)); } }
	}
}