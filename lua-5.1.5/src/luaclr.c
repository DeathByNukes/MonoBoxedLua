/*
** luaclr - functions used when bridging Lua and the Common Language Runtime
** This file is a component of LuaInterface
*/

/* recreate the conditions in lapi.c */
#include <assert.h>
#include <math.h>
#include <stdarg.h>
#include <string.h>

#define lapi_c
#define LUA_CORE

#include "lua.h"

#include "lapi.h"
#include "ldebug.h"
#include "ldo.h"
#include "lfunc.h"
#include "lgc.h"
#include "lmem.h"
#include "lobject.h"
#include "lstate.h"
#include "lstring.h"
#include "ltable.h"
#include "ltm.h"
#include "lundump.h"
#include "lvm.h"

#include "luaclr.h"

static int tag = 0;
LUA_API void *luaclr_gettag() {
	return &tag;
}

LUA_API lua_State *luaclr_mainthread (lua_State *L) {
  return G(L)->mainthread;
}

LUA_API void luaclr_settrythrowf (lua_State *L, luaclr_Try ftry, luaclr_Throw fthrow) {
  lua_lock(L);
  lua_assert((ftry == NULL) == (fthrow == NULL));
  G(L)->ftry = ftry;
  G(L)->fthrow = fthrow;
  lua_unlock(L);
}

LUA_API int luaclr_getfreestack (lua_State *L) {
  int res = 0;
  lua_lock(L);
  // in luaD_reallocstack: lua_assert(L->stack_last - L->stack == L->stacksize - EXTRA_STACK - 1);
  // in lua_checkstack: if (L->stack_last - L->top <= n) grow(L,n);
  res = L->stack_last - L->top - 1;
  lua_unlock(L);
  return res;
}

static void f_checkstack (lua_State *L, void *ud) {
  int size = *cast(int *, ud);
  luaD_checkstack(L, size);
  if (L->ci->top < L->top + size)
    L->ci->top = L->top + size;
}
LUA_API int luaclr_checkstack (lua_State *L, int size) {
  int res = 0;
  int status;
  lua_lock(L);
  if (size > LUAI_MAXCSTACK || (L->top - L->base + size) > LUAI_MAXCSTACK)
    res = 1;  /* stack overflow */
  else if (size > 0) {
    status = luaD_rawrunprotected(L, f_checkstack, &size);
    if (status != 0) {
      if (status == LUA_ERRMEM) {
        res = 2;
      }
      else {
        luaD_throw(L, status);
      }
    }
  }
  lua_unlock(L);
  return res;
}

LUA_API void luaclr_setbytecodeenabled (lua_State *L, int value) {
  lua_lock(L);
  lua_assert(value == 0 || value == 1);
  G(L)->readbytecode = value;
  lua_unlock(L);
}
LUA_API int luaclr_getbytecodeenabled (lua_State *L) {
  int res;
  lua_lock(L);
  res = G(L)->readbytecode;
  lua_unlock(L);
  return res;
}

#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
BOOL APIENTRY DllMain(HANDLE module, DWORD reason, LPVOID reserved) { return TRUE; }
#endif