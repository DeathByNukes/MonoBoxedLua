// Demonstrates using the higher-level LuaInterface API

// un-comment this to make Test.Print do Console.WriteLine and disable Test.AssertPrinted
//#define TEST_PRINT_WRITELINE

using System;
using System.Collections.Generic;
using LuaInterface;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace LuaInterfaceTest
{
	// adding new Lua functions is particularly easy...
	public class MyClass
	{
		[LuaGlobal]
		public static double Sqr(double x) {
			return x*x;
		}

		[LuaGlobal]
		public static double Sum(double x, double y) {
			return x + y;
		}

		// can put into a particular table with a new name
		[LuaGlobal(Name="utils.sum",Description="sum a table of numbers")]
		public static double SumX(params double[] values) {
			double sum = 0.0;
			for (int i = 0; i < values.Length; i++)
				sum += values[i];
			return sum;
		}

		public static void Register(Lua L) {
			L.NewTable("utils");
			LuaRegistrationHelper.TaggedStaticMethods(L,typeof(MyClass));
		}
	}

	public class CSharp
	{
		public virtual string MyMethod(string s) {
			return s.ToUpper();
		}

		protected virtual string Protected(string s) {
			return s + "?";
		}

		protected virtual bool ProtectedBool() {
			return false;
		}

		public static string UseMe (CSharp obj, string val) {
			Test.Print("calling protected {0} {1}",obj.Protected(val),obj.ProtectedBool());
			return obj.MyMethod(val);
		}
	}

	public class RefParms
	{
		public void Args(out int a, out int b) {
			a = 2;
			b = 3;
		}

		public int ArgsI(out int a, out int b) {
			a = 2;
			b = 3;
			return 1;
		}

		public void ArgsVar(params object[] obj) {
			int i = (int)obj[0];
			Test.Print("cool {0}",i);
		}
	}

	[TestClass] public class CallLua
	{
		public static bool IsInteger(double x) {
			return Math.Ceiling(x) == x;
		}

		static void dump(string msg, IEnumerable<object> values)
		{
			Test.Print("{0}:", msg);
			foreach(object o in values)
			if (o == null)
				Test.Print("\tnull");
			else
				Test.Print("\t({0}) {1}", o.GetType(), o);
		}


		[TestMethod] public void Main()
		{
			// disposing LuaInterface.Lua destroys the Lua VM
			// you don't need to bother disposing it if you're using it for the entire lifetime of your program
			using (var L = new Lua())
			{
				L["print"] = L.NewPrintFunction((lua, args) => Test.Print(string.Join("\t", args)));
				// testing out parameters and type coercion for object[] args.
				L["obj"] = new RefParms();
				dump("void,out,out",L.DoString("return obj:Args()"));
				Test.AssertPrinted(
					"void,out,out:",
					"	(System.Double) 2",
					"	(System.Double) 3"
				);
				dump("int,out,out",L.DoString("return obj:ArgsI()"));
				Test.AssertPrinted(
					"int,out,out:",
					"	(System.Double) 1",
					"	(System.Double) 2",
					"	(System.Double) 3"
				);
				L.DoString("obj:ArgsVar{1}");
				Test.AssertPrinted(
					"cool 1"
				);
				Test.Print("equals {0} {1} {2}",IsInteger(2.3),IsInteger(0),IsInteger(44));
				Test.AssertPrinted(
					"equals False True True"
				);
				//Environment.Exit(0);

				object[] res = L.DoString("return 20,'hello'","tmp");
				Test.Print("returned {0} {1}",res[0],res[1]);
				Test.AssertPrinted(
					"returned 20 hello"
				);



				L.DoString("answer = 42");
				Test.Print("answer was {0}",L["answer"]);
				Test.AssertPrinted(
					"answer was 42"
				);

				MyClass.Register(L);

				L.DoString(@"
				print(Sqr(4))
				print(Sum(1.2,10))
				-- please note that this isn't true varargs syntax!
				print(utils.sum {1,5,4.2})
				");
				Test.AssertPrinted(
					"16",
					"11.2",
					"10.2"
				);

				L.DoString("X = {1,2,3}; Y = {fred='dog',alice='cat'}");

				// LuaTable is a reference (luaL_ref) to a Lua table.
				// it should be disposed to avoid memory leaks and keep the registry clean.
				using (var X = (LuaTable)L["X"])
				{
					Test.Print("1st {0} 2nd {1}",X[1],X[2]);
					// (but no Length defined: an oversight?)
					Test.Print("X[4] was nil {0}",X[4] == null);
				}
				Test.AssertPrinted(
					"1st 1 2nd 2",
					"X[4] was nil True"
				);

				using (var Y = L.GetTable("Y")) // equivalent to (LuaTable)L["Y"] but faster
				foreach (var pair in Y)
					Test.Print("{0}={1}", pair.Key, pair.Value);

				Test.AssertPrinted(
					"fred=dog",
					"alice=cat"
				);

				// getting and calling functions by name
				using (LuaFunction f = L.GetFunction("string.gsub"))
				{
					object[] ans = f.Call("here is the dog's dog","dog","cat");
					Test.Print("results '{0}' {1}",ans[0],ans[1]);
				}
				Test.AssertPrinted(
					"results 'here is the cat's cat' 2"
				);

				// and it's entirely possible to do these things from Lua...
				L["L"] = L;
				L.DoString(@"
					L:DoString 'print(1,2,3)'
				");
				Test.AssertPrinted(
					"1	2	3"
				);
				/*
				// it is also possible to override a CLR class in Lua using luanet.make_object.
				// This defines a proxy object which will successfully fool any C# code
				// receiving it.
				 object[] R = L.DoString(@"
					luanet.load_assembly 'CallLua'  -- load this program
					local CSharp = luanet.import_type 'CSharp'
					local T = {}
					function T:MyMethod(s)
						return s:lower()
					end
					function T:Protected(s)
						return s:upper()
					end
					function T:ProtectedBool()
						return true
					end
					luanet.make_object(T,'CSharp')
					print(CSharp.UseMe(T,'CoOl'))
					io.flush()
					return T
				");
				// but it's still a table, and there's no way to cast it to CSharp from here...
				Test.Print("type of returned value {0}",R[0].GetType());
				*/
			}
		}

		[TestCleanup] public void Cleanup()
		{
			GcUtil.GcAll();
		}
	}

	static class Test
	{
		public static void Print(string format, params object[] args) { Print(string.Format(format, args)); }
		public static void Print(string msg)
		{
			#if TEST_PRINT_WRITELINE
			Console.WriteLine(msg);
			#endif
			_messages.Add(msg);
		}

		static readonly List<string> _messages = new List<string>();

		public static void AssertPrinted(params string[] lines)
		{
			#if !TEST_PRINT_WRITELINE
			CollectionAssert.AreEqual(lines, _messages);
			#endif
			_messages.Clear();
		}

		public static string PopOutput() { return PopOutput("\n"); }
		public static string PopOutput(string separator)
		{
			string ret = string.Join(separator, _messages);
			_messages.Clear();
			return ret;
		}
	}
}