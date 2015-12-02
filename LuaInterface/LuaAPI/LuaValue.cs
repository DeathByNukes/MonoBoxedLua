using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LuaInterface.LuaAPI
{
	/// <summary>Container for simple Lua values.</summary>
	[StructLayout(LayoutKind.Explicit)] public struct LuaValue
	{
		[FieldOffset(0)] readonly bool _boolean;
		[FieldOffset(0)] readonly IntPtr _lightuserdata;
		[FieldOffset(0)] readonly double _number;

		[FieldOffset(8)] readonly string _string;

		[FieldOffset(16)] public readonly LUA.T Type; // LUA.T.NIL is 0

		public LuaValue(bool value)   : this() { _boolean       = value; this.Type = LUA.T.BOOLEAN; }
		public LuaValue(IntPtr value) : this() { _lightuserdata = value; this.Type = LUA.T.LIGHTUSERDATA; }
		public LuaValue(double value) : this() { _number        = value; this.Type = LUA.T.NUMBER; }
		public LuaValue(string value) : this() { _string        = value; this.Type = value == null ? LUA.T.NIL : LUA.T.STRING; }
		public LuaValue(LUA.T type) : this() { this.Type = type; }

		private LuaValue(string value, LUA.T type) : this() { _string = value; this.Type = type; }

		public static implicit operator bool  (LuaValue v) { if (v.Type == LUA.T.BOOLEAN)       return v._boolean;       throw v._newCastException(LUA.T.BOOLEAN); }
		public static implicit operator IntPtr(LuaValue v) { if (v.Type == LUA.T.LIGHTUSERDATA) return v._lightuserdata; throw v._newCastException(LUA.T.LIGHTUSERDATA); }
		public static implicit operator double(LuaValue v) { if (v.Type == LUA.T.NUMBER)        return v._number;        throw v._newCastException(LUA.T.NUMBER); }
		public static implicit operator string(LuaValue v)  { switch (v.Type) { case LUA.T.STRING:        return v._string;                     case LUA.T.NIL: return null;             } throw v._newCastException(LUA.T.STRING); }
		public static implicit operator bool?  (LuaValue v) { switch (v.Type) { case LUA.T.BOOLEAN:       return new bool?(v._boolean);         case LUA.T.NIL: return default(bool?);   } throw v._newCastException(LUA.T.BOOLEAN); }
		public static implicit operator IntPtr?(LuaValue v) { switch (v.Type) { case LUA.T.LIGHTUSERDATA: return new IntPtr?(v._lightuserdata); case LUA.T.NIL: return default(IntPtr?); } throw v._newCastException(LUA.T.LIGHTUSERDATA); }
		public static implicit operator double?(LuaValue v) { switch (v.Type) { case LUA.T.NUMBER:        return new double?(v._number);        case LUA.T.NIL: return default(double?); } throw v._newCastException(LUA.T.NUMBER); }
		InvalidCastException _newCastException(LUA.T t) { return new InvalidCastException(string.Format("Tried to read a {0} as a {1}.", this.Type, t)); }

		public static implicit operator LuaValue(bool   v) { return new LuaValue(v); }
		public static implicit operator LuaValue(IntPtr v) { return new LuaValue(v); }
		public static implicit operator LuaValue(double v) { return new LuaValue(v); }
		public static implicit operator LuaValue(string v) { return new LuaValue(v); }
		public static implicit operator LuaValue(bool?   v) { return v.HasValue ? new LuaValue(v.Value) : default(LuaValue); }
		public static implicit operator LuaValue(IntPtr? v) { return v.HasValue ? new LuaValue(v.Value) : default(LuaValue); }
		public static implicit operator LuaValue(double? v) { return v.HasValue ? new LuaValue(v.Value) : default(LuaValue); }

		/// <summary>Returns the value cast as an <see cref="Object"/>. If <see cref="IsSupported"/> is false, returns the <see cref="LuaValue"/> unaltered.</summary>
		public object Box()
		{
			switch (this.Type)
			{
				case LUA.T.NIL:           return null;
				case LUA.T.BOOLEAN:       return _boolean;
				case LUA.T.LIGHTUSERDATA: return _lightuserdata;
				case LUA.T.NUMBER:        return _number;
				case LUA.T.STRING:        return _string;
				default: return this;
			}
		}

		#region Equality

		public bool Equals(LuaValue other)
		{
			if (this.Type != other.Type)
				return false;
			switch (this.Type)
			{
				case LUA.T.NIL:           return true;
				case LUA.T.BOOLEAN:       return _boolean == other._boolean;
				case LUA.T.LIGHTUSERDATA: return _lightuserdata == other._lightuserdata;
				case LUA.T.NUMBER:        return _number == other._number;
				case LUA.T.STRING:        return _string == other._string;
				default: throw _newException();
			}
		}

		public override bool Equals(object obj)
		{
			return obj is LuaValue && this.Equals((LuaValue) obj);
		}

		public override int GetHashCode()
		{
			switch (this.Type)
			{
				case LUA.T.NIL:           return 0;
				case LUA.T.BOOLEAN:       return _boolean.GetHashCode();
				case LUA.T.LIGHTUSERDATA: return _lightuserdata.GetHashCode();
				case LUA.T.NUMBER:        return _number.GetHashCode();
				case LUA.T.STRING:        return _string.GetHashCode();
				default: throw _newException();
			}
		}

		public static bool operator ==(LuaValue left, LuaValue right) { return left.Equals(right); }
		public static bool operator !=(LuaValue left, LuaValue right) { return !left.Equals(right); }

		#endregion

		public override string ToString()
		{
			switch (this.Type)
			{
				case LUA.T.NIL:           return "nil";
				case LUA.T.BOOLEAN:       return _boolean ? "true" : "false";
				case LUA.T.LIGHTUSERDATA: return string.Format("lightuserdata({0})", _lightuserdata.ToString());
				case LUA.T.NUMBER:        return _number.ToString();
				case LUA.T.STRING:        return _string;
				case LUA.T.TABLE:         return _string == null ? "table" : "table; "+_string;
				case LUA.T.FUNCTION:      return "function";
				case LUA.T.USERDATA:      return "userdata";
				case LUA.T.THREAD:        return "thread";
				case LUA.T.NONE:          return "<out of bounds>";
				default:                  return "<invalid(#"+((int)this.Type)+")>";
			}
		}

		/// <summary>[-0, +1, m]</summary>
		/// <exception cref="NotSupportedException">The value is unknown. (see <see cref="IsSupported"/>)</exception>
		public void push(lua.State L)
		{
			switch (this.Type)
			{
			case LUA.T.NIL:
				lua.pushnil(L);
				break;
			case LUA.T.BOOLEAN:
				lua.pushboolean(L, _boolean);
				break;
			case LUA.T.LIGHTUSERDATA:
				lua.pushlightuserdata(L, _lightuserdata);
				break;
			case LUA.T.NUMBER:
				lua.pushnumber(L, _number);
				break;
			case LUA.T.STRING:
				lua.pushstring(L, _string);
				break;
			default:
				throw _newException();
			}
		}

		/// <summary>[-0, +0, -]</summary>
		unsafe public static LuaValue read(lua.State L, int index)
		{
			var type = lua.type(L, index);
			switch (type)
			{
			case LUA.T.BOOLEAN:
				return new LuaValue(lua.toboolean(L, index));
			case LUA.T.LIGHTUSERDATA:
				return new LuaValue(new IntPtr(lua.touserdata(L, index)));
			case LUA.T.NUMBER:
				return new LuaValue(lua.tonumber(L, index));
			case LUA.T.STRING:
				return new LuaValue(lua.tostring(L, index));
			case LUA.T.TABLE:
				return new LuaValue(_summarizeTable(L, index), LUA.T.TABLE);
			default:
				return new LuaValue(type);
			}
		}

		public static string _summarizeTable(lua.State L, int i)
		{
			i = luanet.absoluteindex(L, i);
			Debug.Assert(lua.istable(L,i));
			var objlen = lua.objlen(L,i);
			uint c_str = 0, c_str_added = 0, c_other = 0; // count_
			int len_str = 0;
			// build the output as a list of strings which are only concatenated at the very end
			var o = new List<string>(7) {"length: ", objlen.ToString(), ", non-string/array keys: ", "", ", strings: ", "", " ("};

			var old_top = lua.gettop(L);
			try
			{
				lua.pushnil(L);
				while (lua.next(L,i))
				{
					if (lua.type(L,-2) != LUA.T.STRING)
						++c_other;
					else
					{
						++c_str;
						if (len_str < 256)
						{
							++c_str_added;
							var s = lua.tostring(L,-2);
							if (_containsWhitespace(s))
								s = "“"+s+"”";
							len_str += s.Length + 1;
							o.Add(s);
							switch (lua.type(L,-1))
							{
							case LUA.T.FUNCTION:
								len_str += 2;
								o.Add("() ");
								break;
							case LUA.T.TABLE:
								len_str += 3;
								o.Add("={} ");
								break;
							default:
								o.Add(" ");
								break;
							}
						}
					}
					lua.pop(L,1);
				}
			}
			#if DEBUG
			finally { lua.settop(L, old_top); }
			#else
			catch { lua.settop(L, old_top); throw; }
			#endif

			if (c_str == 0)
				o.RemoveRange(4,3);
			else
			{
				o[5] = c_str.ToString();
				if (c_str != c_str_added)
					o.Add("...)");
				else
				{
					var last = o[o.Count - 1];
					o[o.Count - 1] = last.Substring(0,last.Length-1); // remove the space from the end
					o.Add(")");
				}
			}

			c_other -= objlen.ToUInt32();
			if (c_other == 0)
				o[2] = "";
			else
				o[3] = c_other.ToString();

			return string.Concat(o.ToArray());
		}
		static bool _containsWhitespace(string s)
		{
			for (int i = 0; i < s.Length; ++i)
				if (Char.IsWhiteSpace(s[i])) return true;
			return false;
		}
		/// <summary>[-1, +0, -]</summary>
		public static LuaValue pop(lua.State L)
		{
			var ret = read(L, -1);
			if (ret.Type != LUA.T.NONE)
				lua.pop(L, 1);
			return ret;
		}

		/// <summary><see cref="Type"/> is a type that this struct can hold a value for.</summary>
		public bool IsSupported
		{
			get
			{
				switch (this.Type)
				{
				case LUA.T.NIL:
				case LUA.T.BOOLEAN:
				case LUA.T.LIGHTUSERDATA:
				case LUA.T.NUMBER:
				case LUA.T.STRING:
					return true;
				default:
					return false;
				}
			}
		}
		NotSupportedException _newException() { return new NotSupportedException(this.Type.ToString() + " is not supported."); }

		/// <summary>Throws <see cref="NotSupportedException"/> if <see cref="IsSupported"/> is false.</summary>
		public void VerifySupport() { if (!this.IsSupported) throw _newException(); }
	}
}