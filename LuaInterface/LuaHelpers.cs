using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace LuaInterface.Helpers
{
	public static class LuaHelpers
	{
		/// <summary>Tests if the object is a <see cref="LuaBase"/> and disposes it.</summary>
		public static void TryDispose<T>(this T o)
		where T : class // this is slightly less annoying than TryDispose(this object o)
		{
			// doing LuaBase rather than IDisposable because a table can contain .NET userdata values (which can be IDisposable, but aren't instantiated on each table query)
			var disposable = o as LuaBase;
			if (disposable != null)
				disposable.Dispose();
		}

		/// <summary>Tests if each object is a <see cref="LuaBase"/> and disposes it.</summary>
		public static void DisposeAll(this IEnumerable objects)
		{
			Debug.Assert(objects != null);
			foreach (var entry in objects)
				entry.TryDispose();
		}
		/// <summary>Tests if each key and each value is a <see cref="LuaBase"/> and disposes them.</summary>
		public static void DisposeAll(this IEnumerable<KeyValuePair<object, object>> dict)
		{
			Debug.Assert(dict != null);
			foreach (var pair in dict)
			{
				pair.Key.TryDispose();
				pair.Value.TryDispose();
			}
		}

		/// <summary>Returns an object that, upon disposal, tests if each enumerated object is a <see cref="LuaBase"/> and disposes it.</summary>
		public static IDisposable DeferDisposeAll(this IEnumerable objects)
		{
			Debug.Assert(objects != null);
			return new EnumerableDisposer(objects);
		}
		private class EnumerableDisposer : IDisposable
		{
			private IEnumerable _objects;
			public EnumerableDisposer(IEnumerable objects) { _objects = objects; }
			public void Dispose() { _objects.DisposeAll(); }
		}
		/// <summary>Returns an object that, upon disposal, tests if each key and each value is a <see cref="LuaBase"/> and disposes them.</summary>
		public static IDisposable DeferDisposeAll(this IEnumerable<KeyValuePair<object, object>> dict)
		{
			Debug.Assert(dict != null);
			return new DictionaryDisposer(dict);
		}
		private class DictionaryDisposer : IDisposable
		{
			private IEnumerable<KeyValuePair<object, object>> _dict;
			public DictionaryDisposer(IEnumerable<KeyValuePair<object, object>> dict) { _dict = dict; }
			public void Dispose() { _dict.DisposeAll(); }
		}
	}
}