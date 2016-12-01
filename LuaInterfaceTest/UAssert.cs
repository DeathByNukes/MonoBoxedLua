using System;

namespace LuaInterfaceTest
{
	#if NUNIT
	using NUnit.Framework;
	#else
	using Microsoft.VisualStudio.TestTools.UnitTesting;
	#endif
	/// <summary>Universal Assert</summary>
	public static class UAssert
	{
		public static void IsInstanceOf<T>(object value)
		{
			#if NUNIT
				Assert.IsInstanceOf<T>(value);
			#else
				Assert.IsInstanceOfType(value, typeof(T));
			#endif
		}
		public static void IsInstanceOf<T>(object value, string message)
		{
			#if NUNIT
				Assert.IsInstanceOf<T>(value, message);
			#else
				Assert.IsInstanceOfType(value, typeof(T), message);
			#endif
		}
		public static void AreEqual<T>(T expected, T actual)
		{
			#if NUNIT
				Assert.AreEqual((object) expected, (object) actual);
			#else
				Assert.AreEqual<T>(expected, actual);
			#endif
		}
		public static void AreEqual<T>(T expected, T actual, string message)
		{
			#if NUNIT
				Assert.AreEqual((object) expected, (object) actual, message);
			#else
				Assert.AreEqual<T>(expected, actual, message);
			#endif
		}

		public static void Throws<T>(Action test) where T : Exception { Throws<T>(test, "Exception "+typeof(T).FullName+" was not thrown.");}
		public static void Throws<T>(Action test, string message) where T : Exception
		{
			try
			{
				test();
				Assert.Fail(message);
			}
			catch (T) { }
		}
		public static void NoThrow<T>(Action test) where T : Exception { Throws<T>(test, "Exception "+typeof(T).FullName+" was thrown.");}
		public static void NoThrow<T>(Action test, string message) where T : Exception
		{
			try { test(); }
			catch (T)
			{
				Assert.Fail(message);
			}
		}
	}
}
