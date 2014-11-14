#define DEBUG // so that the Debug.Assert used internally will not be compiled out in release builds
			  // because [Conditional] is dependent on the caller's defines, not ours
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace LuaInterface.LuaAPI
{
	using Top = KeyValuePair<lua.State,int>;

	/// <summary>Helps catch stack balance mistakes at their source rather than potentially much later.</summary>
	public static class StackAssert
	{
		[Conditional("DEBUG")]
		public static void Start(lua.State L)
		{
			t_tops.Push(new Top(L, lua.gettop(L)));
		}
		[Conditional("DEBUG")] public static void Check()           { check(0,      t_tops.Peek()); }
		[Conditional("DEBUG")] public static void Check(int offset) { check(offset, t_tops.Peek()); }

		[Conditional("DEBUG")] public static void End()             { check(0,      t_tops.Pop()); }
		[Conditional("DEBUG")] public static void End(int offset)   { check(offset, t_tops.Pop()); }


		static void check(int offset, Top top)
		{
			Debug.Assert(lua.gettop(top.Key) == top.Value + offset, "Stack is unbalanced!");
		}

		static Stack<Top> t_tops { get { return t__tops ?? (t__tops = new Stack<Top>()); } }
		[ThreadStatic] static Stack<Top> t__tops;
	}
}