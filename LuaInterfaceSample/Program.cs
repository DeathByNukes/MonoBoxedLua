using System;

namespace LuaInterfaceSample
{
    class Program
    {
        public static void Main()
        {
            var luaSample = new MyLuaAPI();

            luaSample.Lua.DoString( "MyLuaNameSpace.CoolFunction(5)" );
            luaSample.Lua.DoString( "local myVector = { x=2, y=5 }; print( MyLuaNameSpace.GetVectorLength( myVector ) )" );
        }
    }
}
