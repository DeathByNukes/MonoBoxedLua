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

		/// <summary>Checks if the value at the specified Lua stack index matches paramType, returning a conversion function if it does and null otherwise.</summary>
		internal ExtractValue checkType(IntPtr luaState,int index,Type paramType)
		{
			Debug.Assert(luaState == translator.interpreter.luaState);
			LuaType luatype = LuaDLL.lua_type(luaState, index);

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

			if (LuaDLL.lua_isnumber(luaState, index))
				return extractValues[paramTypeHandle];

			if (paramType == typeof(bool))
			{
				if (luatype == LuaType.Boolean)
					return extractValues[paramTypeHandle];
			}
			else if (paramType == typeof(string))
			{
				if (LuaDLL.lua_isstring(luaState, index))
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
				if (LuaDLL.luaL_getmetafield(luaState, index, "__index"))
				{
					object obj = translator.getNetObject(luaState, -1);
					LuaDLL.lua_pop(luaState,1);
					if (obj != null && paramType.IsAssignableFrom(obj.GetType()))
						return extractNetObject;
				}
				else
					return null;
			}
			else
			{
				object obj = translator.getNetObject(luaState, index);
				if (obj != null && paramType.IsAssignableFrom(obj.GetType()))
					return extractNetObject;
			}

			return null;
		}

		// The following functions return the value in the specified Lua stack index as the desired type if it can, or null otherwise.
		private static object getAsSbyte(IntPtr luaState,int index)
		{
			sbyte retVal=(sbyte)LuaDLL.lua_tonumber(luaState,index);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,index)) return null;
			return retVal;
		}
		private static object getAsByte(IntPtr luaState,int index)
		{
			byte retVal=(byte)LuaDLL.lua_tonumber(luaState,index);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,index)) return null;
			return retVal;
		}
		private static object getAsShort(IntPtr luaState,int index)
		{
			short retVal=(short)LuaDLL.lua_tonumber(luaState,index);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,index)) return null;
			return retVal;
		}
		private static object getAsUshort(IntPtr luaState,int index)
		{
			ushort retVal=(ushort)LuaDLL.lua_tonumber(luaState,index);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,index)) return null;
			return retVal;
		}
		private static object getAsInt(IntPtr luaState,int index)
		{
			int retVal=(int)LuaDLL.lua_tonumber(luaState,index);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,index)) return null;
			return retVal;
		}
		private static object getAsUint(IntPtr luaState,int index)
		{
			uint retVal=(uint)LuaDLL.lua_tonumber(luaState,index);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,index)) return null;
			return retVal;
		}
		private static object getAsLong(IntPtr luaState,int index)
		{
			long retVal=(long)LuaDLL.lua_tonumber(luaState,index);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,index)) return null;
			return retVal;
		}
		private static object getAsUlong(IntPtr luaState,int index)
		{
			ulong retVal=(ulong)LuaDLL.lua_tonumber(luaState,index);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,index)) return null;
			return retVal;
		}
		private static object getAsDouble(IntPtr luaState,int index)
		{
			double retVal=LuaDLL.lua_tonumber(luaState,index);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,index)) return null;
			return retVal;
		}
		private static object getAsChar(IntPtr luaState,int index)
		{
			char retVal=(char)LuaDLL.lua_tonumber(luaState,index);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,index)) return null;
			return retVal;
		}
		private static object getAsFloat(IntPtr luaState,int index)
		{
			float retVal=(float)LuaDLL.lua_tonumber(luaState,index);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,index)) return null;
			return retVal;
		}
		private static object getAsDecimal(IntPtr luaState,int index)
		{
			decimal retVal=(decimal)LuaDLL.lua_tonumber(luaState,index);
			if(retVal==0 && !LuaDLL.lua_isnumber(luaState,index)) return null;
			return retVal;
		}
		private static object getAsBoolean(IntPtr luaState,int index)
		{
			return LuaDLL.lua_toboolean(luaState,index);
		}
		private static object getAsString(IntPtr luaState,int index)
		{
			string retVal=LuaDLL.lua_tostring(luaState,index);
			if(retVal=="" && !LuaDLL.lua_isstring(luaState,index)) return null;
			return retVal;
		}

		public object getAsObject(IntPtr luaState,int index)
		{
			Debug.Assert(luaState == translator.interpreter.luaState);
			if(LuaDLL.lua_type(luaState,index)==LuaType.Table)
			{
				if(LuaDLL.luaL_getmetafield(luaState,index,"__index"))
				{
					if(LuaDLL.luanet_checkmetatable(luaState,-1))
					{
						LuaDLL.lua_insert(luaState,index);
						LuaDLL.lua_remove(luaState,index+1);
					}
					else
					{
						LuaDLL.lua_pop(luaState,1);
					}
				}
			}
			object obj=translator.getObject(luaState,index);
			return obj;
		}
		public object getAsNetObject(IntPtr luaState,int index)
		{
			Debug.Assert(luaState == translator.interpreter.luaState);
			object obj=translator.getNetObject(luaState,index);
			if(obj==null && LuaDLL.lua_type(luaState,index)==LuaType.Table)
			{
				if(LuaDLL.luaL_getmetafield(luaState,index,"__index"))
				{
					if(LuaDLL.luanet_checkmetatable(luaState,-1))
					{
						LuaDLL.lua_insert(luaState,index);
						LuaDLL.lua_remove(luaState,index+1);
						obj=translator.getNetObject(luaState,index);
					}
					else
					{
						LuaDLL.lua_pop(luaState,1);
					}
				}
			}
			return obj;
		}
	}
}