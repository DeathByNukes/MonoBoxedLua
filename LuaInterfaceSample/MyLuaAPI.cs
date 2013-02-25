using System;
using LuaInterface;

namespace LuaInterfaceSample
{
    class MyLuaAPI
    {
        public Lua      Lua;

        public MyLuaAPI()
        {
            Lua = new LuaInterface.Lua();
            Type self = GetType();

            // Expose some C# functions to Lua
            Lua.NewTable( "MyLuaNameSpace" );
            Lua.RegisterFunction( "MyLuaNameSpace.CoolFunction", this, self.GetMethod( "MyCoolFunction" ) );
        }
 
        public void MyCoolFunction( int count )
        {
            Console.WriteLine( "Got {0}!", count );
        }
    }
}
