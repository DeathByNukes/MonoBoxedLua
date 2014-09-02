using System;
using LuaInterface;

namespace LuaInterfaceSample
{
	class MyLuaAPI : IDisposable
	{
		public Lua      Lua;

		public MyLuaAPI()
		{
			Lua = new LuaInterface.Lua();
			Type self = GetType();

			// Expose some C# functions to Lua
			Lua.NewTable( "MyLuaNameSpace" );
			Lua.RegisterFunction( "MyLuaNameSpace.CoolFunction", this, self.GetMethod( "MyCoolFunction" ) );
			Lua.RegisterFunction( "MyLuaNameSpace.GetVectorLength", this, self.GetMethod( "GetVectorLength" ) );

			Lua["MyLuaNameSpace.SomeConstant"] = 42;
		}

		public void MyCoolFunction( int count )
		{
			Console.WriteLine( "Got {0}!", count );
		}

		public double GetVectorLength( LuaTable _vector )
		{
			// Always dispose LuaTable objects after they have been used
			// This ensure the refcount on the Lua side is properly decremented
			using (_vector)
			{
				double x = (double)_vector["x"];
				double y = (double)_vector["y"];

				return Math.Sqrt(x * x + y * y);
			}
		}

		public void Dispose()
		{
			if (Lua == null) return;
			Lua.Dispose();
			Lua = null;
		}
	}
}