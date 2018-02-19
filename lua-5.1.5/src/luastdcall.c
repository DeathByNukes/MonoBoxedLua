// steffenj: replaced all occurances of LUA_DLLEXPORT with LUA_DLLEXPORT due to "macro redefinition" error
// there's probably a "correct" way to solve this but right now I prefer the one that works :)

#include "lua.h"
#include "lualib.h"
#include "lauxlib.h"
#ifdef _WIN32
#include "luastdcall-windows.h"
#include <windows.h>
BOOL APIENTRY DllMain(HANDLE module, DWORD reason, LPVOID reserved) { return TRUE; }
#else
#define LUA_DLLEXPORT
#include "luastdcall-unix.h"
#endif
#include <stdio.h>
#include <string.h>

static int tag = 0;

// [-0, +0, -]
LUA_DLLEXPORT void *luanet_gettag() {
  return &tag;
}

