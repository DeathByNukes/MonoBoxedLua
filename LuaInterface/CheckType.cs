using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace LuaInterface
{
	/// <summary>Type checking and conversion functions.</summary>
	/// <remarks>
	/// Author: Fabio Mascarenhas
	/// Version: 1.0
	/// </remarks>
	class CheckType
	{
		readonly ObjectTranslator translator;

		readonly ExtractValue extractNetObject;
		readonly Dictionary<RuntimeTypeHandle, ExtractValue> extractValues;

		public CheckType(ObjectTranslator translator)
		{
			this.translator = translator;
			// rationale for TypeHandle: http://stackoverflow.com/a/126507
			extractValues = new Dictionary<RuntimeTypeHandle, ExtractValue>
			{
				{typeof(object)     .TypeHandle, getAsObject},
				{typeof(sbyte)      .TypeHandle, getAsSbyte},
				{typeof(byte)       .TypeHandle, getAsByte},
				{typeof(short)      .TypeHandle, getAsShort},
				{typeof(ushort)     .TypeHandle, getAsUshort},
				{typeof(int)        .TypeHandle, getAsInt},
				{typeof(uint)       .TypeHandle, getAsUint},
				{typeof(long)       .TypeHandle, getAsLong},
				{typeof(ulong)      .TypeHandle, getAsUlong},
				{typeof(double)     .TypeHandle, getAsDouble},
				{typeof(char)       .TypeHandle, getAsChar},
				{typeof(float)      .TypeHandle, getAsFloat},
				{typeof(decimal)    .TypeHandle, getAsDecimal},
				{typeof(bool)       .TypeHandle, getAsBoolean},
				{typeof(string)     .TypeHandle, getAsString},
				{typeof(LuaFunction).TypeHandle, translator.getFunction},
				{typeof(LuaTable)   .TypeHandle, translator.getTable},
				{typeof(LuaUserData).TypeHandle, translator.getUserData}
			};
			extractNetObject = getAsNetObject;
		}

		/// <summary>Checks if the value at Lua stack index stackPos matches paramType, returning a conversion function if it does and null otherwise.</summary>
		internal ExtractValue getExtractor(IReflect paramType)
		{
			return getExtractor(paramType.UnderlyingSystemType);
		}
		internal ExtractValue getExtractor(Type paramType)
		{
			if(paramType.IsByRef) paramType=paramType.GetElementType();

			ExtractValue value;
			return extractValues.TryGetValue(paramType.TypeHandle, out value)
				? value : extractNetObject;
		}

		internal ExtractValue checkType(IntPtr luaState,int stackPos,Type paramType)
		{
			Debug.Assert(luaState == translator.interpreter.luaState); // this is stupid
			LuaType luatype = LuaDLL.lua_type(luaState, stackPos);

			if(paramType.IsByRef) paramType=paramType.GetElementType();

			Type underlyingType = Nullable.GetUnderlyingType(paramType);
			if (underlyingType != null)
			{
				paramType = underlyingType;     // Silently convert nullable types to their non null requics
			}

			var paramTypeHandle = paramType.TypeHandle;

			if (paramType == typeof(object))
				return extractValues[paramTypeHandle];

			/*
			//CP: Added support for generic parameters
			if (paramType.IsGenericParameter)
			{
				if (luatype == LuaType.Boolean)
					return extractValues[typeof(bool).TypeHandle];
				else if (luatype == LuaType.String)
					return extractValues[typeof(string).TypeHandle];
				else if (luatype == LuaType.Table)
					return extractValues[typeof(LuaTable).TypeHandle];
				else if (luatype == LuaType.Userdata)
					return extractValues[typeof(object).TypeHandle];
				else if (luatype == LuaType.Function)
					return extractValues[typeof(LuaFunction).TypeHandle];
				else if (luatype == LuaType.Number)
					return extractValues[typeof(double).TypeHandle];
				//else // suppress CS0642
					;//an unsupported type was encountered
			}
			*/

			if (LuaDLL.lua_isnumber(luaState, stackPos))
				return extractValues[paramTypeHandle];

			if (paramType == typeof(bool))
			{
				if (luatype == LuaType.Boolean)
					return extractValues[paramTypeHandle];
			}
			else if (paramType == typeof(string))
			{
				if (LuaDLL.lua_isstring(luaState, stackPos))
					return extractValues[paramTypeHandle];
				else if (luatype == LuaType.Nil)
					return extractNetObject; // kevinh - silently convert nil to a null string pointer
			}
			else if (paramType == typeof(LuaTable))
			{
				if (luatype == LuaType.Table)
					return extractValues[paramTypeHandle];
				else if (luatype == LuaType.Nil)
					return extractNetObject; // tkopal - silently convert nil to a null table
			}
			else if (paramType == typeof(LuaUserData))
			{
				if (luatype == LuaType.Userdata)
					return extractValues[paramTypeHandle];
			}
			else if (paramType == typeof(LuaFunction))
			{
				if (luatype == LuaType.Function)
					return extractValues[paramTypeHandle];
				else if (luatype == LuaType.Nil)
					return extractNetObject; // elisee - silently convert nil to a null string pointer
			}
			else if (typeof(Delegate).IsAssignableFrom(paramType) && luatype == LuaType.Function)
			{
#if __NOGEN__
				translator.throwError(luaState,"Delegates not implemented");
#else
				return new ExtractValue(new DelegateGenerator(translator, paramType).extractGenerated);
#endif
			}
			else if (paramType.IsInterface && luatype == LuaType.Table)
			{
#if __NOGEN__
				translator.throwError(luaState,"Interfaces not implemented");
#else
				return new ExtractValue(new ClassGenerator(translator, paramType).extractGenerated);
#endif
			}
			else if ((paramType.IsInterface || paramType.IsClass) && luatype == LuaType.Nil)
			{
				// kevinh - allow nil to be silently converted to null - extractNetObject will return null when the item ain't found
				return extractNetObject;
			}
			else if (luatype == LuaType.Table)
			{
				if (LuaDLL.luaL_getmetafield(luaState, stackPos, "__index"))
				{
					object obj = translator.getNetObject(luaState, -1);
					LuaDLL.lua_settop(luaState, -2);
					if (obj != null && paramType.IsAssignableFrom(obj.GetType()))
						return extractNetObject;
				}
				else
					return null;
			}
			else
			{
				object obj = translator.getNetObject(luaState, stackPos);
				if (obj != null && paramType.IsAssignableFrom(obj.GetType()))
					return extractNetObject;
			}

			return null;
		}

		// The following functions return the value in the Lua stack index stackPos as the desired type if it can, or null otherwise.
		private static object getAsSbyte(IntPtr luaState,int stackPos)
		{
			sbyte retVal=(sbyte)LuaDLL.lua_tonumber(luaState,stackPos);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,stackPos)) return null;
			return retVal;
		}
		private static object getAsByte(IntPtr luaState,int stackPos)
		{
			byte retVal=(byte)LuaDLL.lua_tonumber(luaState,stackPos);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,stackPos)) return null;
			return retVal;
		}
		private static object getAsShort(IntPtr luaState,int stackPos)
		{
			short retVal=(short)LuaDLL.lua_tonumber(luaState,stackPos);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,stackPos)) return null;
			return retVal;
		}
		private static object getAsUshort(IntPtr luaState,int stackPos)
		{
			ushort retVal=(ushort)LuaDLL.lua_tonumber(luaState,stackPos);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,stackPos)) return null;
			return retVal;
		}
		private static object getAsInt(IntPtr luaState,int stackPos)
		{
			int retVal=(int)LuaDLL.lua_tonumber(luaState,stackPos);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,stackPos)) return null;
			return retVal;
		}
		private static object getAsUint(IntPtr luaState,int stackPos)
		{
			uint retVal=(uint)LuaDLL.lua_tonumber(luaState,stackPos);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,stackPos)) return null;
			return retVal;
		}
		private static object getAsLong(IntPtr luaState,int stackPos)
		{
			long retVal=(long)LuaDLL.lua_tonumber(luaState,stackPos);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,stackPos)) return null;
			return retVal;
		}
		private static object getAsUlong(IntPtr luaState,int stackPos)
		{
			ulong retVal=(ulong)LuaDLL.lua_tonumber(luaState,stackPos);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,stackPos)) return null;
			return retVal;
		}
		private static object getAsDouble(IntPtr luaState,int stackPos)
		{
			double retVal=LuaDLL.lua_tonumber(luaState,stackPos);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,stackPos)) return null;
			return retVal;
		}
		private static object getAsChar(IntPtr luaState,int stackPos)
		{
			char retVal=(char)LuaDLL.lua_tonumber(luaState,stackPos);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,stackPos)) return null;
			return retVal;
		}
		private static object getAsFloat(IntPtr luaState,int stackPos)
		{
			float retVal=(float)LuaDLL.lua_tonumber(luaState,stackPos);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,stackPos)) return null;
			return retVal;
		}
		private static object getAsDecimal(IntPtr luaState,int stackPos)
		{
			decimal retVal=(decimal)LuaDLL.lua_tonumber(luaState,stackPos);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,stackPos)) return null;
			return retVal;
		}
		private static object getAsBoolean(IntPtr luaState,int stackPos)
		{
			return LuaDLL.lua_toboolean(luaState,stackPos);
		}
		private static object getAsString(IntPtr luaState,int stackPos)
		{
			string retVal=LuaDLL.lua_tostring(luaState,stackPos);
			if(retVal=="" && !LuaDLL.lua_isstring(luaState,stackPos)) return null;
			return retVal;
		}

		public object getAsObject(IntPtr luaState,int stackPos)
		{
			Debug.Assert(luaState == translator.interpreter.luaState); // this is stupid
			if(LuaDLL.lua_type(luaState,stackPos)==LuaType.Table)
			{
				if(LuaDLL.luaL_getmetafield(luaState,stackPos,"__index"))
				{
					if(LuaDLL.luaL_checkmetatable(luaState,-1))
					{
						LuaDLL.lua_insert(luaState,stackPos);
						LuaDLL.lua_remove(luaState,stackPos+1);
					}
					else
					{
						LuaDLL.lua_settop(luaState,-2);
					}
				}
			}
			object obj=translator.getObject(luaState,stackPos);
			return obj;
		}
		public object getAsNetObject(IntPtr luaState,int stackPos)
		{
			Debug.Assert(luaState == translator.interpreter.luaState); // this is stupid
			object obj=translator.getNetObject(luaState,stackPos);
			if(obj==null && LuaDLL.lua_type(luaState,stackPos)==LuaType.Table)
			{
				if(LuaDLL.luaL_getmetafield(luaState,stackPos,"__index"))
				{
					if(LuaDLL.luaL_checkmetatable(luaState,-1))
					{
						LuaDLL.lua_insert(luaState,stackPos);
						LuaDLL.lua_remove(luaState,stackPos+1);
						obj=translator.getNetObject(luaState,stackPos);
					}
					else
					{
						LuaDLL.lua_settop(luaState,-2);
					}
				}
			}
			return obj;
		}
	}
}