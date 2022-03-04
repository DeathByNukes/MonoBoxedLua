using System;
using System.Runtime.InteropServices;
using LuaInterface.LuaAPI;

namespace LuaInterface
{
	/// <summary><para>Container for simple Lua values.</para><para>Though it cannot contain the value of garbage collected Lua types other than string (see <see cref="IsSupported"/>), it works around this fact whenever possible.</para></summary>
	/// <remarks>
	/// <para><see cref="LuaValue"/> versions of APIs provide two advantages over the regular <see cref="Object"/> APIs:<list type="number"><item><description>Lua values stored in a LuaValue are never boxed. This means less garbage collection overhead and all the object data is stored on the stack.</description></item><item><description><see cref="LuaBase"/> objects (tables, functions, and userdata) are never generated. This means less burden on both the CLR and Lua garbage collectors, and there is no risk of a memory leak from failing to call <see cref="LuaBase.Dispose()"/>.</description></item></list></para>
	/// <para>LuaValue instances can be implicitly cast to their CLR equivalents. If the LuaValue contains a type other than type it is being cast to, <see cref="InvalidCastException"/> will be thrown.</para>
	/// <para>In the case of casting to nullable types and strings, nil is converted to null.</para>
	/// <para>CLR equivalents can also be implicitly cast to a LuaValue, so they can be directly assigned to LuaValue variables and arguments.</para>
	/// <para>The OrDefault methods return the stored value if it matches the argument type; if it doesn't match or is nil, the argument is returned. They do not throw any exceptions.</para>
	/// <para>The IfNil methods are like OrDefault except an exception is thrown when the value is the wrong type. This produces the same result as casting the value to a nullable type and then immediately using the null-coalescing (??) operator.</para>
	/// <para>The As* properties attempt to cast to the desired value and return null if the cast fails. They do not throw any exceptions.</para>
	/// <para>The TryGetAs methods attempt to cast to the same type as the argument. If the cast succeeds, the value is assigned to the specified variable; otherwise, the variable is left unchanged and false is returned. Warning: <see cref="TryGetAs(ref string)"/> operates the same as all the other overloads; it considers nil to be a non-string type and will never set a string variable to null.</para>
	/// </remarks>
	[StructLayout(LayoutKind.Auto)] public struct LuaValue : luanet.IPushable
	{
		[StructLayout(LayoutKind.Explicit)] struct Union
		{
			[FieldOffset(0)] public readonly bool   Boolean;       public Union(bool value)   : this() { Boolean       = value; }
			[FieldOffset(0)] public readonly IntPtr LightUserData; public Union(IntPtr value) : this() { LightUserData = value; }
			[FieldOffset(0)] public readonly double Number;        public Union(double value) : this() { Number        = value; }
		}

		readonly Union _union;
		readonly string _string;

		/// <summary>The type of the value stored in this structure.</summary>
		public readonly LuaType Type; // LuaType.Nil is 0

		public LuaValue(bool value)   : this() { _union = new Union(value); this.Type = LuaType.Boolean; }
		public LuaValue(IntPtr value) : this() { _union = new Union(value); this.Type = LuaType.LightUserData; }
		public LuaValue(double value) : this() { _union = new Union(value); this.Type = LuaType.Number; }
		public LuaValue(string value) : this() { _string = value; this.Type = value == null ? LuaType.Nil : LuaType.String; }
		public LuaValue(LuaType type) : this() { this.Type = type; }

		private LuaValue(string value, LuaType type) : this() { _string = value; this.Type = type; }

		/// <summary>A nil LuaValue. default(LuaValue) can also be used for nil.</summary>
		public static LuaValue Nil { get { return default(LuaValue); } }

		public static implicit operator bool  (LuaValue v) { if (v.Type == LuaType.Boolean)       return v._union.Boolean;       throw v._newCastException(LuaType.Boolean); }
		public static implicit operator IntPtr(LuaValue v) { if (v.Type == LuaType.LightUserData) return v._union.LightUserData; throw v._newCastException(LuaType.LightUserData); }
		public static implicit operator double(LuaValue v) { if (v.Type == LuaType.Number)        return v._union.Number;        throw v._newCastException(LuaType.Number); }
		public static explicit operator float (LuaValue v) { if (v.Type == LuaType.Number)        return (float)v._union.Number; throw v._newCastException(LuaType.Number); }
		public static explicit operator int   (LuaValue v) { if (v.Type == LuaType.Number)        return (int)v._union.Number;   throw v._newCastException(LuaType.Number); }
		public static implicit operator string (LuaValue v) { switch (v.Type) { case LuaType.String:        return v._string;                           case LuaType.Nil: return null;             } throw v._newCastException(LuaType.String); }
		public static implicit operator bool?  (LuaValue v) { switch (v.Type) { case LuaType.Boolean:       return new bool?  (v._union.Boolean);       case LuaType.Nil: return default(bool?);   } throw v._newCastException(LuaType.Boolean); }
		public static implicit operator IntPtr?(LuaValue v) { switch (v.Type) { case LuaType.LightUserData: return new IntPtr?(v._union.LightUserData); case LuaType.Nil: return default(IntPtr?); } throw v._newCastException(LuaType.LightUserData); }
		public static implicit operator double?(LuaValue v) { switch (v.Type) { case LuaType.Number:        return new double?(v._union.Number);        case LuaType.Nil: return default(double?); } throw v._newCastException(LuaType.Number); }
		public static explicit operator float? (LuaValue v) { switch (v.Type) { case LuaType.Number:        return new float? ((float)v._union.Number); case LuaType.Nil: return default(float?);  } throw v._newCastException(LuaType.Number); }
		public static explicit operator int?   (LuaValue v) { switch (v.Type) { case LuaType.Number:        return new int?   ((int)v._union.Number);   case LuaType.Nil: return default(int?);    } throw v._newCastException(LuaType.Number); }
		InvalidCastException _newCastException(LuaType t) { return new InvalidCastException(string.Format("Tried to read a {0} as a {1}.", this.Type, t)); }

		///                                                                                                                                <summary>Returns the value if it is a <see cref="LuaType.Boolean"      />, otherwise returns <paramref name="default_value"/>.</summary>
		public bool   OrDefault(bool   default_value) { return this.Type == LuaType.Boolean       ? _union.Boolean       : default_value; }
		///                                                                                                                                <summary>Returns the value if it is a <see cref="LuaType.LightUserData"/>, otherwise returns <paramref name="default_value"/>.</summary>
		public IntPtr OrDefault(IntPtr default_value) { return this.Type == LuaType.LightUserData ? _union.LightUserData : default_value; }
		///                                                                                                                                <summary>Returns the value if it is a <see cref="LuaType.Number"       />, otherwise returns <paramref name="default_value"/>.</summary>
		public double OrDefault(double default_value) { return this.Type == LuaType.Number        ? _union.Number        : default_value; }
		///                                                                                                                                <summary>Returns the value if it is a <see cref="LuaType.Number"       />, otherwise returns <paramref name="default_value"/>.</summary>
		public float  OrDefault(float  default_value) { return this.Type == LuaType.Number        ? (float)_union.Number : default_value; }
		///                                                                                                                                <summary>Returns the value if it is a <see cref="LuaType.Number"       />, otherwise returns <paramref name="default_value"/>.</summary>
		public int    OrDefault(int    default_value) { return this.Type == LuaType.Number        ? (int)_union.Number   : default_value; }
		///                                                                                                                                <summary>Returns the value if it is a <see cref="LuaType.String"       />, otherwise returns <paramref name="default_value"/>.</summary>
		public string OrDefault(string default_value) { return this.Type == LuaType.String        ? _string              : default_value; }

		///                                                                                                                                                                                                                  <summary>Returns the value if it is a <see cref="LuaType.Boolean"      />, <paramref name="default_value"/> if it is <see cref="LuaType.Nil"/>, or throws an exception if it is a different type.</summary><exception cref="InvalidCastException"></exception>
		public bool   IfNil(bool   default_value) { switch (this.Type) { case LuaType.Boolean:       return _union.Boolean;       case LuaType.Nil: return default_value; } throw _newCastException(LuaType.Boolean); }
		///                                                                                                                                                                                                                  <summary>Returns the value if it is a <see cref="LuaType.LightUserData"/>, <paramref name="default_value"/> if it is <see cref="LuaType.Nil"/>, or throws an exception if it is a different type.</summary><exception cref="InvalidCastException"></exception>
		public IntPtr IfNil(IntPtr default_value) { switch (this.Type) { case LuaType.LightUserData: return _union.LightUserData; case LuaType.Nil: return default_value; } throw _newCastException(LuaType.LightUserData); }
		///                                                                                                                                                                                                                  <summary>Returns the value if it is a <see cref="LuaType.Number"       />, <paramref name="default_value"/> if it is <see cref="LuaType.Nil"/>, or throws an exception if it is a different type.</summary><exception cref="InvalidCastException"></exception>
		public double IfNil(double default_value) { switch (this.Type) { case LuaType.Number:        return _union.Number;        case LuaType.Nil: return default_value; } throw _newCastException(LuaType.Number); }
		///                                                                                                                                                                                                                  <summary>Returns the value if it is a <see cref="LuaType.Number"       />, <paramref name="default_value"/> if it is <see cref="LuaType.Nil"/>, or throws an exception if it is a different type.</summary><exception cref="InvalidCastException"></exception>
		public float  IfNil(float  default_value) { switch (this.Type) { case LuaType.Number:        return (float)_union.Number; case LuaType.Nil: return default_value; } throw _newCastException(LuaType.Number); }
		///                                                                                                                                                                                                                  <summary>Returns the value if it is a <see cref="LuaType.Number"       />, <paramref name="default_value"/> if it is <see cref="LuaType.Nil"/>, or throws an exception if it is a different type.</summary><exception cref="InvalidCastException"></exception>
		public int    IfNil(int    default_value) { switch (this.Type) { case LuaType.Number:        return (int)_union.Number;   case LuaType.Nil: return default_value; } throw _newCastException(LuaType.Number); }
		///                                                                                                                                                                                                                  <summary>Returns the value if it is a <see cref="LuaType.String"       />, <paramref name="default_value"/> if it is <see cref="LuaType.Nil"/>, or throws an exception if it is a different type.</summary><exception cref="InvalidCastException"></exception>
		public string IfNil(string default_value) { switch (this.Type) { case LuaType.String:        return _string;              case LuaType.Nil: return default_value; } throw _newCastException(LuaType.String); }

		///                                                                                                                                  <summary>Returns the value if it is a <see cref="LuaType.Boolean"      />, otherwise returns null.</summary>
		public bool?   AsBool   { get { return this.Type == LuaType.Boolean       ? new bool?  (_union.Boolean)       : default(bool?);   } }
		///                                                                                                                                  <summary>Returns the value if it is a <see cref="LuaType.LightUserData"/>, otherwise returns null.</summary>
		public IntPtr? AsIntPtr { get { return this.Type == LuaType.LightUserData ? new IntPtr?(_union.LightUserData) : default(IntPtr?); } }
		///                                                                                                                                  <summary>Returns the value if it is a <see cref="LuaType.Number"       />, otherwise returns null.</summary>
		public double? AsDouble { get { return this.Type == LuaType.Number        ? new double?(_union.Number)        : default(double?); } }
		///                                                                                                                                  <summary>Returns the value if it is a <see cref="LuaType.Number"       />, otherwise returns null.</summary>
		public float?  AsFloat  { get { return this.Type == LuaType.Number        ? new float? ((float)_union.Number) : default(float?);  } }
		///                                                                                                                                  <summary>Returns the value if it is a <see cref="LuaType.Number"       />, otherwise returns null.</summary>
		public int?    AsInt    { get { return this.Type == LuaType.Number        ? new int?   ((int)_union.Number)   : default(int?);    } }
		///                                                                                                                                  <summary>Returns the value if it is a <see cref="LuaType.String"       />, otherwise returns null.</summary>
		public string  AsString { get { return this.Type == LuaType.String        ? _string                           : null;             } }

		///                                                                                                                                              <summary>If the value is a <see cref="LuaType.Boolean"      />, assigns the value to <paramref name="variable"/> and returns true. Otherwise, leaves <paramref name="variable"/> alone and returns false.</summary>
		public bool TryGetAs(ref bool   variable) { if (this.Type != LuaType.Boolean      ) return false; variable = _union.Boolean      ; return true; }
		///                                                                                                                                              <summary>If the value is a <see cref="LuaType.LightUserData"/>, assigns the value to <paramref name="variable"/> and returns true. Otherwise, leaves <paramref name="variable"/> alone and returns false.</summary>
		public bool TryGetAs(ref IntPtr variable) { if (this.Type != LuaType.LightUserData) return false; variable = _union.LightUserData; return true; }
		///                                                                                                                                              <summary>If the value is a <see cref="LuaType.Number"       />, assigns the value to <paramref name="variable"/> and returns true. Otherwise, leaves <paramref name="variable"/> alone and returns false.</summary>
		public bool TryGetAs(ref double variable) { if (this.Type != LuaType.Number       ) return false; variable = _union.Number       ; return true; }
		///                                                                                                                                              <summary>If the value is a <see cref="LuaType.Number"       />, assigns the value to <paramref name="variable"/> and returns true. Otherwise, leaves <paramref name="variable"/> alone and returns false.</summary>
		public bool TryGetAs(ref float  variable) { if (this.Type != LuaType.Number       ) return false; variable = (float)_union.Number; return true; }
		///                                                                                                                                              <summary>If the value is a <see cref="LuaType.Number"       />, assigns the value to <paramref name="variable"/> and returns true. Otherwise, leaves <paramref name="variable"/> alone and returns false.</summary>
		public bool TryGetAs(ref int    variable) { if (this.Type != LuaType.Number       ) return false; variable = (int)_union.Number  ; return true; }
		///                                                                                                                                              <summary>If the value is a <see cref="LuaType.String"       />, assigns the value to <paramref name="variable"/> and returns true. Otherwise, leaves <paramref name="variable"/> alone and returns false.</summary>
		public bool TryGetAs(ref string variable) { if (this.Type != LuaType.String       ) return false; variable = _string             ; return true; }

		public static implicit operator LuaValue(bool   v) { return new LuaValue(v); }
		public static implicit operator LuaValue(IntPtr v) { return new LuaValue(v); }
		public static implicit operator LuaValue(double v) { return new LuaValue(v); }
		public static implicit operator LuaValue(string v) { return new LuaValue(v); }
		public static implicit operator LuaValue(bool?   v) { return v.HasValue ? new LuaValue(v.Value) : default(LuaValue); }
		public static implicit operator LuaValue(IntPtr? v) { return v.HasValue ? new LuaValue(v.Value) : default(LuaValue); }
		public static implicit operator LuaValue(double? v) { return v.HasValue ? new LuaValue(v.Value) : default(LuaValue); }

		/// <summary>Converts the value to a boolean according to Lua type conversion rules. (nil and false are false, all other values are true)</summary>
		public bool If()
		{
			switch (this.Type)
			{
				case LuaType.Boolean: return _union.Boolean;
				case LuaType.Nil:     return false;
				default:              return true;
			}
		}

		/// <summary>Returns the value cast as an <see cref="Object"/>. If <see cref="IsSupported"/> is false, returns the <see cref="LuaValue"/> unaltered.</summary>
		public object Box()
		{
			switch (this.Type)
			{
				case LuaType.Nil:           return null;
				case LuaType.Boolean:       return _union.Boolean;
				case LuaType.LightUserData: return _union.LightUserData;
				case LuaType.Number:        return _union.Number;
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
				case LuaType.Boolean:       return _union.Boolean == other._union.Boolean;
				case LuaType.LightUserData: return _union.LightUserData == other._union.LightUserData;
				case LuaType.Number:        return _union.Number == other._union.Number;
				case LuaType.String:        return _string == other._string;
				case LuaType.Table: if (_string != null && other._string != null && _string != other._string) return false; goto default;
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
				case LuaType.Boolean:       return _union.Boolean.Equals(obj);
				case LuaType.LightUserData: return _union.LightUserData.Equals(obj);
				case LuaType.Number:        return _union.Number.Equals(obj);
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
				default:                    return base.GetHashCode();
				case LuaType.Boolean:       return _union.Boolean.GetHashCode();
				case LuaType.LightUserData: return _union.LightUserData.GetHashCode();
				case LuaType.Number:        return _union.Number.GetHashCode();
				case LuaType.String:        return _string.GetHashCode();
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
				case LuaType.Boolean:       return _union.Boolean ? "true" : "false";
				case LuaType.LightUserData: return string.Format("lightuserdata({0})", _union.LightUserData.ToString());
				case LuaType.Number:        return _union.Number.ToString();
				case LuaType.String:        return _string;
				case LuaType.Table:         return _string == null ? "table" : "table; "+_string;
				case LuaType.Function:      return "function";
				case LuaType.UserData:      return "userdata";
				case LuaType.Thread:        return "thread";
				case (LuaType) LUA.T.NONE:  return "<out of bounds>";
				default:                    return "<invalid(#"+((int)this.Type)+")>";
			}
		}

		/// <summary>[-0, +1, e] Pushes the value, or raises a Lua error if the value is unknown. (see <see cref="IsSupported"/>)</summary>
		void luanet.IPushable.push(lua.State L) { this.push(L); }
		/// <summary>[-0, +1, e] Pushes the value, or raises a Lua error if the value is unknown. (see <see cref="IsSupported"/>)</summary>
		internal void push(lua.State L)
		{
			switch (this.Type)
			{
				case LuaType.Nil:           lua.pushnil(L);                                 break;
				case LuaType.Boolean:       lua.pushboolean(L, _union.Boolean);             break;
				case LuaType.LightUserData: lua.pushlightuserdata(L, _union.LightUserData); break;
				case LuaType.Number:        lua.pushnumber(L, _union.Number);               break;
				case LuaType.String:        lua.pushstring(L, _string);                     break;
				default:                    luaL.error(L, _NotSupportedMsg);                break;
			}
		}

		/// <summary>[-0, +0, -] Creates a <see cref="LuaValue"/> from the specified value on the stack. If <paramref name="detailed_tostring"/> is true, <see cref="luanet.summarizetable"/> will be used to preemptively generate and store a detailed <see cref="ToString"/> value.</summary>
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

		/// <summary>[-1, +0, -] Creates a <see cref="LuaValue"/> from the value on the top of the stack and pops it. If <paramref name="detailed_tostring"/> is true, <see cref="luanet.summarizetable"/> will be used to preemptively generate and store a detailed <see cref="ToString"/> value.</summary>
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