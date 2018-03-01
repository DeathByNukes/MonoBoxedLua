using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using LuaInterface.LuaAPI;

namespace LuaInterface
{
	/// <summary>Type checking and conversion functions.</summary>
	/// <remarks>Author: Fabio Mascarenhas</remarks>
	class CheckType
	{
		readonly Lua interpreter;

		readonly ExtractValue extractNetObject;
		readonly Dictionary<RuntimeTypeHandle, Caster> _casters;
		struct Caster
		{
			/// <summary>[-0, +0, -]</summary>
			public readonly CheckValue   CheckValue;
			/// <summary>[-0, +0, m]</summary>
			public readonly ExtractValue ExtractValue;
			public Caster(CheckValue c, ExtractValue e) { CheckValue = c; ExtractValue = e; }
		}

		public CheckType(Lua interpreter)
		{
			this.interpreter = interpreter;
			// rationale for TypeHandle: http://stackoverflow.com/a/126507
			_casters = new Dictionary<RuntimeTypeHandle, Caster>(20)
			{
				{typeof(object     ).TypeHandle, new Caster(isObject  , asObject  )},
				{typeof(sbyte      ).TypeHandle, new Caster(isSbyte   , asSbyte   )},
				{typeof(byte       ).TypeHandle, new Caster(isByte    , asByte    )},
				{typeof(short      ).TypeHandle, new Caster(isShort   , asShort   )},
				{typeof(ushort     ).TypeHandle, new Caster(isUshort  , asUshort  )},
				{typeof(int        ).TypeHandle, new Caster(isInt     , asInt     )},
				{typeof(uint       ).TypeHandle, new Caster(isUint    , asUint    )},
				{typeof(long       ).TypeHandle, new Caster(isLong    , asLong    )},
				{typeof(ulong      ).TypeHandle, new Caster(isUlong   , asUlong   )},
				{typeof(double     ).TypeHandle, new Caster(isDouble  , asDouble  )},
				{typeof(char       ).TypeHandle, new Caster(isChar    , asChar    )},
				{typeof(float      ).TypeHandle, new Caster(isFloat   , asFloat   )},
				{typeof(decimal    ).TypeHandle, new Caster(isDecimal , asDecimal )},
				{typeof(bool       ).TypeHandle, new Caster(isBoolean , asBoolean )},
				{typeof(string     ).TypeHandle, new Caster(isString  , asString  )},
				{typeof(LuaValue   ).TypeHandle, new Caster(isObject  , asLuaValue)},
				{typeof(LuaFunction).TypeHandle, new Caster(isFunction, asFunction)},
				{typeof(LuaTable   ).TypeHandle, new Caster(isTable   , asTable   )},
				{typeof(LuaUserData).TypeHandle, new Caster(isUserData, asUserData)},
				{typeof(LuaString  ).TypeHandle, new Caster(isString  , asLString )},
				// update the constructor when you add more
			};
			extractNetObject = asNetObject;
		}

		internal ExtractValue getExtractor(IReflect paramType)
		{
			return getExtractor(paramType.UnderlyingSystemType);
		}
		internal ExtractValue getExtractor(Type paramType)
		{
			if(paramType.IsByRef) paramType=paramType.GetElementType();

			Caster caster;
			return _casters.TryGetValue(paramType.TypeHandle, out caster)
				? caster.ExtractValue : extractNetObject;
		}

		/// <summary>[-0, +0, m] Checks if the value at the specified Lua stack index matches paramType, returning a conversion function if it does and null otherwise.</summary>
		/// <exception cref="NullReferenceException"><paramref name="paramType"/> is required</exception>
		internal ExtractValue checkType(lua.State L,int index,Type paramType)
		{
			Debug.Assert(interpreter.IsSameLua(L));
			Debug.Assert(paramType != null);

			if (paramType.IsByRef) paramType = paramType.GetElementType();
			Debug.Assert(paramType != null);

			Type underlyingType = Nullable.GetUnderlyingType(paramType);
			if (underlyingType != null)
				paramType = underlyingType; // Silently convert nullable types to their non null requics

			Caster caster;
			if (_casters.TryGetValue(paramType.TypeHandle, out caster))
			{
				return caster.CheckValue(L, index)
					? caster.ExtractValue : null;
			}

			switch (lua.type(L, index))
			{
			case LUA.T.FUNCTION:
				if (typeof(Delegate).IsAssignableFrom(paramType))
				{
					#if __NOGEN__
						luaL.error(L,"Delegates not implemented");
					#else
						return new ExtractValue(new DelegateGenerator(interpreter, paramType).extractGenerated);
					#endif
				}
				break;

			case LUA.T.TABLE:
				if (paramType.IsInterface)
				{
					#if __NOGEN__
						luaL.error(L,"Interfaces not implemented");
					#else
						return new ExtractValue(new ClassGenerator(interpreter, paramType).extractGenerated);
					#endif
				}
				luaL.checkstack(L, 1, "CheckType.checkType");
				if (luaL.getmetafield(L, index, "__index"))
				{
					object indexer = luaclr.toref(L, -1);
					lua.pop(L,1);
					if (paramType.IsInstanceOfType(indexer))
						return extractNetObject;
				}
				break;

			case LUA.T.NIL:
				if (paramType.IsClass || paramType.IsInterface)
					return extractNetObject;
				break;

			case LUA.T.USERDATA:
				object obj = luaclr.toref(L, index);
				if (paramType.IsInstanceOfType(obj))
					return extractNetObject;
				break;
			}
			return null;
		}

		// [-0, +0, m] The following functions return the value in the specified Lua stack index as the desired type if it can, or null otherwise.
		static object asSbyte   (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) ? (object)(sbyte  ) num : null; } 
		static object asByte    (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) ? (object)(byte   ) num : null; }
		static object asShort   (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) ? (object)(short  ) num : null; }
		static object asUshort  (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) ? (object)(ushort ) num : null; }
		static object asInt     (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) ? (object)(int    ) num : null; }
		static object asUint    (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) ? (object)(uint   ) num : null; }
		static object asLong    (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) ? (object)(long   ) num : null; }
		static object asUlong   (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) ? (object)(ulong  ) num : null; }
		static object asDouble  (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) ? (object)          num : null; }
		static object asChar    (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) ? (object)(char   ) num : null; }
		static object asFloat   (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) ? (object)(float  ) num : null; }
		static object asDecimal (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) ? (object)(decimal) num : null; }
		static object asBoolean (lua.State L,int index) { return lua.toboolean(L,index); }
		static object asString  (lua.State L,int index) { return lua.tostring (L,index); }
		static object asLuaValue(lua.State L,int index) { return LuaValue.read(L, index, false); }
		       object asFunction(lua.State L,int index) { return lua.type(L, index) == LUA.T.FUNCTION ? new LuaFunction(L, interpreter, index) : null; }
		       object asTable   (lua.State L,int index) { return lua.type(L, index) == LUA.T.TABLE    ? new LuaTable   (L, interpreter, index) : null; }
		       object asUserData(lua.State L,int index) { return lua.type(L, index) == LUA.T.USERDATA ? new LuaUserData(L, interpreter, index) : null; }
		       object asLString (lua.State L,int index) { return lua.type(L, index) == LUA.T.STRING   ? new LuaString  (L, interpreter, index) : null; }

		// [-0, +0, m]
		public object asObject(lua.State L,int index)
		{
			Debug.Assert(interpreter.IsSameLua(L));
			if (lua.type(L,index) == LUA.T.TABLE) // todo: find what purpose this branch serves and document it
			{
				luaL.checkstack(L, 1, "CheckType.asObject");
				if (luaL.getmetafield(L,index,"__index"))
				{
					if (luaclr.isref(L,-1))
					{
						lua.insert(L,index);
						lua.remove(L,index+1);
					}
					else
					{
						lua.pop(L,1);
					}
				}
			}
			object obj = interpreter.translator.getObject(L,index);
			return obj;
		}
		// [-0, +0, m]
		public object asNetObject(lua.State L,int index)
		{
			Debug.Assert(interpreter.IsSameLua(L));
			object obj = luaclr.toref(L,index);
			if (obj == null && lua.type(L,index) == LUA.T.TABLE) // todo: find what purpose this branch serves and document it
			{
				luaL.checkstack(L, 1, "CheckType.asNetObject");
				if (luaL.getmetafield(L,index,"__index"))
				{
					if (luaclr.isref(L,-1))
					{
						lua.insert(L,index);
						lua.remove(L,index+1);
						obj = luaclr.toref(L,index);
					}
					else
					{
						lua.pop(L,1);
					}
				}
			}
			return obj;
		}

		// The following functions check the value in the specified Lua stack index and return true if it is the desired type
		static bool isSbyte  (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) && num >= sbyte .MinValue && num <= sbyte .MaxValue  ; }
		static bool isByte   (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) && num >= byte  .MinValue && num <= byte  .MaxValue  ; }
		static bool isShort  (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) && num >= short .MinValue && num <= short .MaxValue  ; }
		static bool isUshort (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) && num >= ushort.MinValue && num <= ushort.MaxValue  ; }
		static bool isInt    (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) && num >= int   .MinValue && num <= int   .MaxValue  ; }
		static bool isUint   (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) && num >= uint  .MinValue && num <= uint  .MaxValue  ; }
		static bool isLong   (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) && num >= long  .MinValue && num <= long  .MaxValue  ; }
		static bool isUlong  (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) && num >= ulong .MinValue && num <= ulong .MaxValue  ; }
		static bool isDouble (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) && num >= double.MinValue && num <= double.MaxValue  ; }
		static bool isChar   (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) && num >= char  .MinValue && num <= char  .MaxValue  ; }
		static bool isFloat  (lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) && num >= float .MinValue && num <= float .MaxValue  ; }
		static bool isDecimal(lua.State L,int index) { double num; return luanet.trygetnumber(L, index, out num) && num >= (double) decimal.MinValue && num <= (double) decimal.MaxValue  ; }
		static bool isBoolean(lua.State L,int index) { return lua.isboolean(L,index); }
		static bool isObject (lua.State L,int index) { return true; }

		static bool isString(lua.State L,int index)
		{
			switch (lua.type(L, index))
			{
			case LUA.T.NIL:
			case LUA.T.STRING:
				return true;
			default:
				return false;
			}
		}
		static bool isFunction(lua.State L,int index)
		{
			switch (lua.type(L, index))
			{
			case LUA.T.NIL:
			case LUA.T.FUNCTION:
				return true;
			default:
				return false;
			}
		}
		static bool isTable(lua.State L,int index)
		{
			switch (lua.type(L, index))
			{
			case LUA.T.NIL:
			case LUA.T.TABLE:
				return true;
			default:
				return false;
			}
		}
		static bool isUserData(lua.State L,int index)
		{
			switch (lua.type(L, index))
			{
			case LUA.T.NIL:
			case LUA.T.USERDATA:
				return true;
			default:
				return false;
			}
		}
	}
}