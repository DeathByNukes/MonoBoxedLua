#ifndef luaclr_h
#define luaclr_h

#include "lua.h"

LUA_API void *luaclr_gettag(void);

LUA_API lua_State *luaclr_mainthread (lua_State *L);

LUA_API void luaclr_settrythrowf (lua_State *L, luaclr_Try ftry, luaclr_Throw fthrow);

LUA_API int luaclr_getfreestack (lua_State *L);

LUA_API int luaclr_checkstack (lua_State *L, int size);

LUA_API void luaclr_setbytecodeenabled (lua_State *L, int value);

LUA_API int luaclr_getbytecodeenabled (lua_State *L);

#endif