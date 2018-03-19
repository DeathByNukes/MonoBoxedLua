using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using LuaInterface.LuaAPI;

namespace LuaInterface
{
	public static class LuaRegistrationHelper
	{
		#region Tagged instance methods
		/// <summary>
		/// Registers all public instance methods in an object tagged with <see cref="LuaGlobalAttribute"/> as Lua global functions
		/// </summary>
		/// <param name="lua">The Lua VM to add the methods to</param>
		/// <param name="o">The object to get the methods from</param>
		public static void TaggedInstanceMethods(Lua lua, object o)
		{
			if (lua == null) throw new ArgumentNullException("lua");
			if (o == null) throw new ArgumentNullException("o");

			foreach (MethodInfo method in o.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public))
			foreach (LuaGlobalAttribute attribute in method.GetCustomAttributes(typeof(LuaGlobalAttribute), true))
			{
				Debug.Assert(attribute.Name != "");
				lua.RegisterFunction(attribute.Name ?? method.Name, o, method);
			}
		}
		#endregion

		#region Tagged static methods
		/// <summary>
		/// Registers all public static methods in a class tagged with <see cref="LuaGlobalAttribute"/> as Lua global functions
		/// </summary>
		/// <param name="lua">The Lua VM to add the methods to</param>
		/// <param name="type">The class type to get the methods from</param>
		public static void TaggedStaticMethods(Lua lua, Type type)
		{
			if (lua == null) throw new ArgumentNullException("lua");
			if (type == null) throw new ArgumentNullException("type");
			if (!type.IsClass) throw new ArgumentException("The type must be a class!", "type");

			foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public))
			foreach (LuaGlobalAttribute attribute in method.GetCustomAttributes(typeof(LuaGlobalAttribute), false))
			{
				Debug.Assert(attribute.Name != "");
				lua.RegisterFunction(attribute.Name ?? method.Name, null, method);
			}
		}
		#endregion

		#region Enumeration
		/// <summary>Creates a Lua table with keys and values mirroring the specified enumeration.</summary>
		/// <typeparam name="T">The enum type to register</typeparam>
		/// <param name="interpreter">The Lua VM to add the enum to</param>
		[SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "The type parameter is used to select an enum type")]
		public static void Enumeration<T>(Lua interpreter) where T : struct, IConvertible
		{
			if (interpreter == null) throw new ArgumentNullException("lua");
			Type type = typeof(T);
			var L = interpreter._L;
			_PushEnumeration<T>(L, type);
			lua.setfield(L, LUA.GLOBALSINDEX, type.Name);
		}

		/// <summary>Creates a Lua table with keys and values mirroring the specified enumeration.</summary>
		/// <typeparam name="T">The enum type to register</typeparam>
		/// <param name="lua">The Lua VM to add the enum to</param>
		[SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "The type parameter is used to select an enum type")]
		public static LuaTable NewEnumeration<T>(Lua lua) where T : struct, IConvertible
		{
			if (lua == null) throw new ArgumentNullException("lua");
			var L = lua._L;
			_PushEnumeration<T>(L, typeof(T));
			return new LuaTable(L, lua);
		}

		/// <summary>[-0, +1, m]</summary>
		static void _PushEnumeration<T>(lua.State L, Type type) where T : struct, IConvertible
		{
			Debug.Assert(typeof(T) == type);
			if (!type.IsEnum) throw new ArgumentException("The type must be an enumeration.");

			string[] names = Enum.GetNames(type);
			var values = (T[])Enum.GetValues(type);

			Debug.Assert(EzDelegate.Func(() => {
				if (values.Length == 0) return true;
				try { var x = values[0].ToDouble(null); return true; }
				catch { return false; }
			})(), "enum conversion to double isn't expected to throw exceptions");

			StackAssert.Start(L);
			luaclr.checkstack(L, 3, "LuaRegistrationHelper._PushEnumeration");
			lua.createtable(L, 0, names.Length);
			for (int i = 0; i < names.Length; ++i)
			{
				lua.pushstring(L, names[i]);
				lua.pushnumber(L, values[i].ToDouble(null));
				lua.rawset(L, -3);
			}
			StackAssert.End(1);
		}
		#endregion
	}
}