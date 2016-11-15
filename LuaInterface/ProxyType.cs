using System;
using System.Globalization;
using System.Reflection;

namespace LuaInterface
{
	/// <summary>Represents the static type references passed to Lua.</summary>
	public sealed class ProxyType : IReflect, IEquatable<ProxyType>
	{
		readonly Type proxy;

		/// <exception cref="ArgumentNullException"></exception>
		public ProxyType(Type proxy)
		{
			if (proxy == null) throw new ArgumentNullException("proxy");
			this.proxy = proxy;
		}

		/// <summary>Provide human readable short hand for this proxy object</summary>
		public override string ToString()
		{
			return "ProxyType(" + proxy.Name + ")";
		}

		#region forward IReflect implementation to proxy

		public Type UnderlyingSystemType { get { return proxy; } }

		public FieldInfo GetField(string name, BindingFlags bindingAttr)
		{
			return proxy.GetField(name, bindingAttr);
		}

		public FieldInfo[] GetFields(BindingFlags bindingAttr)
		{
			return proxy.GetFields(bindingAttr);
		}

		public MemberInfo[] GetMember(string name, BindingFlags bindingAttr)
		{
			return proxy.GetMember(name, bindingAttr);
		}

		public MemberInfo[] GetMembers(BindingFlags bindingAttr)
		{
			return proxy.GetMembers(bindingAttr);
		}

		public MethodInfo GetMethod(string name, BindingFlags bindingAttr)
		{
			return proxy.GetMethod(name, bindingAttr);
		}

		public MethodInfo GetMethod(string name, BindingFlags bindingAttr, Binder binder, Type[] types, ParameterModifier[] modifiers)
		{
			return proxy.GetMethod(name, bindingAttr, binder, types, modifiers);
		}

		public MethodInfo[] GetMethods(BindingFlags bindingAttr)
		{
			return proxy.GetMethods(bindingAttr);
		}

		public PropertyInfo GetProperty(string name, BindingFlags bindingAttr)
		{
			return proxy.GetProperty(name, bindingAttr);
		}

		public PropertyInfo GetProperty(string name, BindingFlags bindingAttr, Binder binder, Type returnType, Type[] types, ParameterModifier[] modifiers)
		{
			return proxy.GetProperty(name, bindingAttr, binder, returnType, types, modifiers);
		}

		public PropertyInfo[] GetProperties(BindingFlags bindingAttr)
		{
			return proxy.GetProperties(bindingAttr);
		}

		public object InvokeMember(string name,	BindingFlags invokeAttr, Binder binder,	object target, object[] args, ParameterModifier[] modifiers, CultureInfo culture, string[] namedParameters)
		{
			return proxy.InvokeMember(name, invokeAttr, binder, target, args, modifiers, culture, namedParameters);
		}

		#endregion

		#region equality

		public bool Equals(ProxyType other)
		{
			if ((object) other == null) return false;
			return this.proxy == other.proxy;
		}

		public override bool Equals(object obj) { return this.Equals(obj as ProxyType); }

		public override int GetHashCode() { return proxy.GetHashCode() ^ 1; }

		public static bool operator ==(ProxyType left, ProxyType right) { return Equals(left, right); }
		public static bool operator !=(ProxyType left, ProxyType right) { return !Equals(left, right); }

		#endregion
	}
}