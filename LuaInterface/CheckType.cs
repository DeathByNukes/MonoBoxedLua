using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using LuaInterface.LuaAPI;

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
		internal ExtractValue checkType(lua.State L,int index,Type paramType)
		{
			Debug.Assert(L == translator.interpreter._L);
			var luatype = lua.type(L, index);

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
				if (luatype == LUA.T.BOOLEAN)
					return extractValues[typeof(bool).TypeHandle];
				else if (luatype == LUA.T.STRING)
					return extractValues[typeof(string).TypeHandle];
				else if (luatype == LUA.T.TABLE)
					return extractValues[typeof(LuaTable).TypeHandle];
				else if (luatype == LUA.T.USERDATA)
					return extractValues[typeof(object).TypeHandle];
				else if (luatype == LUA.T.FUNCTION)
					return extractValues[typeof(LuaFunction).TypeHandle];
				else if (luatype == LUA.T.NUMBER)
					return extractValues[typeof(double).TypeHandle];
				//else // suppress CS0642
					;//an unsupported type was encountered
			}
			*/

			if (lua.isnumber(L, index))
				return extractValues[paramTypeHandle];

			if (paramType == typeof(bool))
			{
				if (luatype == LUA.T.BOOLEAN)
					return extractValues[paramTypeHandle];
			}
			else if (paramType == typeof(string))
			{
				if (lua.isstring(L, index))
					return extractValues[paramTypeHandle];
				else if (luatype == LUA.T.NIL)
					return extractNetObject; // kevinh - silently convert nil to a null string pointer
			}
			else if (paramType == typeof(LuaTable))
			{
				if (luatype == LUA.T.TABLE)
					return extractValues[paramTypeHandle];
				else if (luatype == LUA.T.NIL)
					return extractNetObject; // tkopal - silently convert nil to a null table
			}
			else if (paramType == typeof(LuaUserData))
			{
				if (luatype == LUA.T.USERDATA)
					return extractValues[paramTypeHandle];
			}
			else if (paramType == typeof(LuaFunction))
			{
				if (luatype == LUA.T.FUNCTION)
					return extractValues[paramTypeHandle];
				else if (luatype == LUA.T.NIL)
					return extractNetObject; // elisee - silently convert nil to a null string pointer
			}
			else if (typeof(Delegate).IsAssignableFrom(paramType) && luatype == LUA.T.FUNCTION)
			{
#if __NOGEN__
				luaL.error(L,"Delegates not implemented");
#else
				return new ExtractValue(new DelegateGenerator(translator, paramType).extractGenerated);
#endif
			}
			else if (paramType.IsInterface && luatype == LUA.T.TABLE)
			{
#if __NOGEN__
				luaL.error(L,"Interfaces not implemented");
#else
				return new ExtractValue(new ClassGenerator(translator, paramType).extractGenerated);
#endif
			}
			else if ((paramType.IsInterface || paramType.IsClass) && luatype == LUA.T.NIL)
			{
				// kevinh - allow nil to be silently converted to null - extractNetObject will return null when the item ain't found
				return extractNetObject;
			}
			else if (luatype == LUA.T.TABLE)
			{
				luaL.checkstack(L, 1, "CheckType.checkType");
				if (luaL.getmetafield(L, index, "__index"))
				{
					object obj = translator.getNetObject(L, -1);
					lua.pop(L,1);
					if (obj != null && paramType.IsAssignableFrom(obj.GetType()))
						return extractNetObject;
				}
				else
					return null;
			}
			else
			{
				object obj = translator.getNetObject(L, index);
				if (obj != null && paramType.IsAssignableFrom(obj.GetType()))
					return extractNetObject;
			}

			return null;
		}

		// The following functions return the value in the specified Lua stack index as the desired type if it can, or null otherwise.
		private static object getAsSbyte(lua.State L,int index)
		{
			sbyte retVal=(sbyte)lua.tonumber(L,index);
			if(retVal==0 && !lua.isnumber(L,index)) return null;
			return retVal;
		}
		private static object getAsByte(lua.State L,int index)
		{
			byte retVal=(byte)lua.tonumber(L,index);
			if(retVal==0 && !lua.isnumber(L,index)) return null;
			return retVal;
		}
		private static object getAsShort(lua.State L,int index)
		{
			short retVal=(short)lua.tonumber(L,index);
			if(retVal==0 && !lua.isnumber(L,index)) return null;
			return retVal;
		}
		private static object getAsUshort(lua.State L,int index)
		{
			ushort retVal=(ushort)lua.tonumber(L,index);
			if(retVal==0 && !lua.isnumber(L,index)) return null;
			return retVal;
		}
		private static object getAsInt(lua.State L,int index)
		{
			int retVal=(int)lua.tonumber(L,index);
			if(retVal==0 && !lua.isnumber(L,index)) return null;
			return retVal;
		}
		private static object getAsUint(lua.State L,int index)
		{
			uint retVal=(uint)lua.tonumber(L,index);
			if(retVal==0 && !lua.isnumber(L,index)) return null;
			return retVal;
		}
		private static object getAsLong(lua.State L,int index)
		{
			long retVal=(long)lua.tonumber(L,index);
			if(retVal==0 && !lua.isnumber(L,index)) return null;
			return retVal;
		}
		private static object getAsUlong(lua.State L,int index)
		{
			ulong retVal=(ulong)lua.tonumber(L,index);
			if(retVal==0 && !lua.isnumber(L,index)) return null;
			return retVal;
		}
		private static object getAsDouble(lua.State L,int index)
		{
			double retVal=lua.tonumber(L,index);
			if(retVal==0 && !lua.isnumber(L,index)) return null;
			return retVal;
		}
		private static object getAsChar(lua.State L,int index)
		{
			char retVal=(char)lua.tonumber(L,index);
			if(retVal==0 && !lua.isnumber(L,index)) return null;
			return retVal;
		}
		private static object getAsFloat(lua.State L,int index)
		{
			float retVal=(float)lua.tonumber(L,index);
			if(retVal==0 && !lua.isnumber(L,index)) return null;
			return retVal;
		}
		private static object getAsDecimal(lua.State L,int index)
		{
			decimal retVal=(decimal)lua.tonumber(L,index);
			if(retVal==0 && !lua.isnumber(L,index)) return null;
			return retVal;
		}
		private static object getAsBoolean(lua.State L,int index)
		{
			return lua.toboolean(L,index);
		}
		private static object getAsString(lua.State L,int index)
		{
			string retVal=lua.tostring(L,index);
			if(retVal=="" && !lua.isstring(L,index)) return null;
			return retVal;
		}

		public object getAsObject(lua.State L,int index)
		{
			Debug.Assert(L == translator.interpreter._L);
			if(lua.type(L,index)==LUA.T.TABLE)
			{
				luaL.checkstack(L, 1, "CheckType.getAsObject");
				if(luaL.getmetafield(L,index,"__index"))
				{
					if(luanet.checkmetatable(L,-1))
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
			object obj=translator.getObject(L,index);
			return obj;
		}
		public object getAsNetObject(lua.State L,int index)
		{
			Debug.Assert(L == translator.interpreter._L);
			object obj=translator.getNetObject(L,index);
			if(obj==null && lua.type(L,index)==LUA.T.TABLE)
			{
				luaL.checkstack(L, 1, "CheckType.getAsNetObject");
				if(luaL.getmetafield(L,index,"__index"))
				{
					if(luanet.checkmetatable(L,-1))
					{
						lua.insert(L,index);
						lua.remove(L,index+1);
						obj=translator.getNetObject(L,index);
					}
					else
					{
						lua.pop(L,1);
					}
				}
			}
			return obj;
		}
	}
}