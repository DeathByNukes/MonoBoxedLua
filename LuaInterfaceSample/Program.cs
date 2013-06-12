using System;

namespace LuaInterfaceSample
{
    class Program
    {
        public static void Main()
        {
            var luaSample = new MyLuaAPI();

            luaSample.Lua.DoString( "MyLuaNameSpace.CoolFunction( MyLuaNameSpace.SomeConstant )" );
            luaSample.Lua.DoString( "MyLuaNameSpace.CoolFunction( 123 )" );
            luaSample.Lua.DoString( "local myVector = { x=2, y=5 }; print( MyLuaNameSpace.GetVectorLength( myVector ) )" );
        }
    }
}
