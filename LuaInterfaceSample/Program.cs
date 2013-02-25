using System;

namespace LuaInterfaceSample
{
    class Program
    {
        public static void Main()
        {
            var luaSample = new MyLuaAPI();

            luaSample.Lua.DoString( "MyLuaNameSpace.CoolFunction(5)" );
        }
    }
}
