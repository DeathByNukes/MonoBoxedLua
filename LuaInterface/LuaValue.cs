using System;
using System.Runtime.InteropServices;
using LuaInterface.LuaAPI;

namespace LuaInterface
{
	/// <summary>Container for simple Lua values.</summary>
	[StructLayout(LayoutKind.Explicit)] public struct LuaValue
	{
		[FieldOffset(0)] readonly bool _boolean;
		[FieldOffset(0)] readonly IntPtr _lightuserdata;
		[FieldOffset(0)] readonly double _number;

		[FieldOffset(8)] readonly string _string;

		[FieldOffset(16)] public readonly LuaType Type; // LuaType.Nil is 0

		public LuaValue(bool value)   : this() { _boolean       = value; this.Type = LuaType.Boolean; }
		public LuaValue(IntPtr value) : this() { _lightuserdata = value; this.Type = LuaType.LightUserData; }
		public LuaValue(double value) : this() { _number        = value; this.Type = LuaType.Number; }
		public LuaValue(string value) : this() { _string        = value; this.Type = value == null ? LuaType.Nil : LuaType.String; }
		public LuaValue(LuaType type) : this() { this.Type = type; }

		private LuaValue(string value, LuaType type) : this() { _string = value; this.Type = type; }

		public static LuaValue Nil { get { return default(LuaValue); } }

		public static implicit operator bool  (LuaValue v) { if (v.Type == LuaType.Boolean)       return v._boolean;       throw v._newCastException(LuaType.Boolean); }
		public static implicit operator IntPtr(LuaValue v) { if (v.Type == LuaType.LightUserData) return v._lightuserdata; throw v._newCastException(LuaType.LightUserData); }
		public static implicit operator double(LuaValue v) { if (v.Type == LuaType.Number)        return v._number;        throw v._newCastException(LuaType.Number); }
		public static implicit operator string(LuaValue v)  { switch (v.Type) { case LuaType.String:        return v._string;                     case LuaType.Nil: return null;             } throw v._newCastException(LuaType.String); }
		public static implicit operator bool?  (LuaValue v) { switch (v.Type) { case LuaType.Boolean:       return new bool?(v._boolean);         case LuaType.Nil: return default(bool?);   } throw v._newCastException(LuaType.Boolean); }
		public static implicit operator IntPtr?(LuaValue v) { switch (v.Type) { case LuaType.LightUserData: return new IntPtr?(v._lightuserdata); case LuaType.Nil: return default(IntPtr?); } throw v._newCastException(LuaType.LightUserData); }
		public static implicit operator double?(LuaValue v) { switch (v.Type) { case LuaType.Number:        return new double?(v._number);        case LuaType.Nil: return default(double?); } throw v._newCastException(LuaType.Number); }
		InvalidCastException _newCastException(LuaType t) { return new InvalidCastException(string.Format("Tried to read a {0} as a {1}.", this.Type, t)); }

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
				case LuaType.Nil:           return null;
				case LuaType.Boolean:       return _boolean;
				case LuaType.LightUserData: return _lightuserdata;
				case LuaType.Number:        return _number;
				case LuaType.String:        return _string;
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
				case LuaType.Nil:           return true;
				case LuaType.Boolean:       return _boolean == other._boolean;
				case LuaType.LightUserData: return _lightuserdata == other._lightuserdata;
				case LuaType.Number:        return _number == other._number;
				case LuaType.String:        return _string == other._string;
				default: throw _newException(); // they might be equal or they might not
			}
		}

		public override bool Equals(object obj)
		{
			if (obj is LuaValue)
				return this.Equals((LuaValue) obj);
			switch (this.Type)
			{
				case LuaType.Nil:           return obj == null;
				case LuaType.Boolean:       return _boolean.Equals(obj);
				case LuaType.LightUserData: return _lightuserdata.Equals(obj);
				case LuaType.Number:        return _number.Equals(obj);
				case LuaType.String:        return _string.Equals(obj);
				case LuaType.Table:    if (obj is LuaTable)    throw _newException(); break;
				case LuaType.Function: if (obj is LuaFunction) throw _newException(); break;
				case LuaType.UserData: throw _newException();
			}
			return false;
		}

		public override int GetHashCode()
		{
			switch (this.Type)
			{
				case LuaType.Nil:           return 0;
				case LuaType.Boolean:       return _boolean.GetHashCode();
				case LuaType.LightUserData: return _lightuserdata.GetHashCode();
				case LuaType.Number:        return _number.GetHashCode();
				case LuaType.String:        return _string.GetHashCode();
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
				case LuaType.Nil:           return "nil";
				case LuaType.Boolean:       return _boolean ? "true" : "false";
				case LuaType.LightUserData: return string.Format("lightuserdata({0})", _lightuserdata.ToString());
				case LuaType.Number:        return _number.ToString();
				case LuaType.String:        return _string;
				case LuaType.Table:         return _string == null ? "table" : "table; "+_string;
				case LuaType.Function:      return "function";
				case LuaType.UserData:      return "userdata";
				case LuaType.Thread:        return "thread";
				case (LuaType) LUA.T.NONE:  return "<out of bounds>";
				default:                  return "<invalid(#"+((int)this.Type)+")>";
			}
		}

		/// <summary>[-0, +1, e] Pushes the value, or raises a Lua error if the value is unknown. (see <see cref="IsSupported"/>)</summary>
		public void push(lua.State L)
		{
			switch (this.Type)
			{
				case LuaType.Nil:           lua.pushnil(L);                           break;
				case LuaType.Boolean:       lua.pushboolean(L, _boolean);             break;
				case LuaType.LightUserData: lua.pushlightuserdata(L, _lightuserdata); break;
				case LuaType.Number:        lua.pushnumber(L, _number);               break;
				case LuaType.String:        lua.pushstring(L, _string);               break;
				default:                  luaL.error(L, _NotSupportedMsg);          break;
			}
		}

		/// <summary>[-0, +0, -]</summary>
		unsafe public static LuaValue read(lua.State L, int index, bool detailed_tostring)
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
				return new LuaValue(detailed_tostring ? luanet.summarizetable(L, index, 256) : null, LuaType.Table);
			default:
				return new LuaValue((LuaType) type);
			}
		}

		/// <summary>[-1, +0, -]</summary>
		public static LuaValue pop(lua.State L, bool detailed_tostring)
		{
			var ret = read(L, -1, detailed_tostring);
			if (ret.Type != (LuaType) LUA.T.NONE)
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
				case LuaType.Nil:
				case LuaType.Boolean:
				case LuaType.LightUserData:
				case LuaType.Number:
				case LuaType.String:
					return true;
				default:
					return false;
				}
			}
		}

		string _NotSupportedMsg { get { return this.Type.ToString() + " is not supported."; } }
		NotSupportedException _newException() { return new NotSupportedException(_NotSupportedMsg); }

		/// <summary>Throws <see cref="NotSupportedException"/> if <see cref="IsSupported"/> is false.</summary>
		public void VerifySupport() { if (!this.IsSupported) throw _newException(); }
		/// <summary>Throws <see cref="ArgumentException"/> if <see cref="IsSupported"/> is false.</summary>
		public void VerifySupport(string param_name) { if (!this.IsSupported) throw new ArgumentException(_NotSupportedMsg, param_name); }
	}
}