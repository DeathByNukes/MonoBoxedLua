using System;
using System.Diagnostics;

namespace LuaInterface
{
	/// <summary>Helper to enable C# to use implicit typing for delegate creation.</summary>
	public static class EzDelegate
	{
		public static Action                                                         Action                                                        (this Action d)                                                         {return d;}
		public static Action<T1>                                                     Action<T1>                                                    (this Action<T1> d)                                                     {return d;}
		public static Action<T1,T2>                                                  Action<T1,T2>                                                 (this Action<T1,T2> d)                                                  {return d;}
		public static Action<T1,T2,T3>                                               Action<T1,T2,T3>                                              (this Action<T1,T2,T3> d)                                               {return d;}
		public static Action<T1,T2,T3,T4>                                            Action<T1,T2,T3,T4>                                           (this Action<T1,T2,T3,T4> d)                                            {return d;}
		#if NET_4 || NET_4_6
		public static Action<T1,T2,T3,T4,T5>                                         Action<T1,T2,T3,T4,T5>                                        (this Action<T1,T2,T3,T4,T5> d)                                         {return d;}
		public static Action<T1,T2,T3,T4,T5,T6>                                      Action<T1,T2,T3,T4,T5,T6>                                     (this Action<T1,T2,T3,T4,T5,T6> d)                                      {return d;}
		public static Action<T1,T2,T3,T4,T5,T6,T7>                                   Action<T1,T2,T3,T4,T5,T6,T7>                                  (this Action<T1,T2,T3,T4,T5,T6,T7> d)                                   {return d;}
		public static Action<T1,T2,T3,T4,T5,T6,T7,T8>                                Action<T1,T2,T3,T4,T5,T6,T7,T8>                               (this Action<T1,T2,T3,T4,T5,T6,T7,T8> d)                                {return d;}
		public static Action<T1,T2,T3,T4,T5,T6,T7,T8,T9>                             Action<T1,T2,T3,T4,T5,T6,T7,T8,T9>                            (this Action<T1,T2,T3,T4,T5,T6,T7,T8,T9> d)                             {return d;}
		public static Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10>                         Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10>                        (this Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10> d)                         {return d;}
		public static Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11>                     Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11>                    (this Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11> d)                     {return d;}
		public static Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12>                 Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12>                (this Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12> d)                 {return d;}
		public static Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13>             Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13>            (this Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13> d)             {return d;}
		public static Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14>         Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14>        (this Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14> d)         {return d;}
		public static Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14,T15>     Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14,T15>    (this Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14,T15> d)     {return d;}
		public static Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14,T15,T16> Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14,T15,T16>(this Action<T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14,T15,T16> d) {return d;}
		#endif

		public static Func<TResult>                                                        Func<TResult>                                                       (this Func<TResult> d)                                                        {return d;}
		public static Func<TResult,T1>                                                     Func<TResult,T1>                                                    (this Func<TResult,T1> d)                                                     {return d;}
		public static Func<TResult,T1,T2>                                                  Func<TResult,T1,T2>                                                 (this Func<TResult,T1,T2> d)                                                  {return d;}
		public static Func<TResult,T1,T2,T3>                                               Func<TResult,T1,T2,T3>                                              (this Func<TResult,T1,T2,T3> d)                                               {return d;}
		public static Func<TResult,T1,T2,T3,T4>                                            Func<TResult,T1,T2,T3,T4>                                           (this Func<TResult,T1,T2,T3,T4> d)                                            {return d;}
		#if NET_4 || NET_4_6
		public static Func<TResult,T1,T2,T3,T4,T5>                                         Func<TResult,T1,T2,T3,T4,T5>                                        (this Func<TResult,T1,T2,T3,T4,T5> d)                                         {return d;}
		public static Func<TResult,T1,T2,T3,T4,T5,T6>                                      Func<TResult,T1,T2,T3,T4,T5,T6>                                     (this Func<TResult,T1,T2,T3,T4,T5,T6> d)                                      {return d;}
		public static Func<TResult,T1,T2,T3,T4,T5,T6,T7>                                   Func<TResult,T1,T2,T3,T4,T5,T6,T7>                                  (this Func<TResult,T1,T2,T3,T4,T5,T6,T7> d)                                   {return d;}
		public static Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8>                                Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8>                               (this Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8> d)                                {return d;}
		public static Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9>                             Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9>                            (this Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9> d)                             {return d;}
		public static Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10>                         Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10>                        (this Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10> d)                         {return d;}
		public static Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11>                     Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11>                    (this Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11> d)                     {return d;}
		public static Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12>                 Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12>                (this Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12> d)                 {return d;}
		public static Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13>             Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13>            (this Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13> d)             {return d;}
		public static Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14>         Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14>        (this Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14> d)         {return d;}
		public static Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14,T15>     Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14,T15>    (this Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14,T15> d)     {return d;}
		public static Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14,T15,T16> Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14,T15,T16>(this Func<TResult,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14,T15,T16> d) {return d;}
		#endif
	}
	/*
	public class example
	{
		public example()
		{
			// store lambdas in variables
			var inc = EzDelegate.Func((int a) => a + 1);
			Debug.Assert(inc(11) == 12);

			// reflection without putting the function name in a string (i.e. typeof(example).GetMethod("foo"))
			Debug.Assert(EzDelegate.Action(this.foo).Method.ReturnType == typeof(void));
			Debug.Assert(EzDelegate.Func(example.bar).Method.ReturnType == typeof(int)); // resharper fails to parse this
		}
		public void foo() {}
		public static int bar() { return 0; }
	} // */
}