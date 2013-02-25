using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
