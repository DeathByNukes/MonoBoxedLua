MonoBoxedLua
=============

MonoBoxedLua is based on [MonoLuaInterface](https://github.com/stevedonovan/MonoLuaInterface).

It's a stripped-down version meant to be used to run a Lua sandbox. 

 * The "luanet" module has been removed so that Lua code cannot load and use random .NET classes.
 * Some Lua libraries have been disabled to prevent direct access to the system (See linit.c and loslib.c)

MonoBoxedLua is used as the scripting engine in [CraftStudio](http://craftstud.io/), a real-time cooperative 3d game-making platform.

License
-------

MonoBoxedLua is made available under the MIT license (See LICENSE.txt)