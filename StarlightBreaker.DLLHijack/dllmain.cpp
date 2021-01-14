// dllmain.cpp : 定义 DLL 应用程序的入口点。
#include "SuperDllHijack/dllhijack.h"
#include "SignatureScanner/SignatureScanner.h"
#include <iostream>
#include <fstream>
#include <algorithm>
#include <string>
#include "shlwapi.h"
#include <vector>
#include <iomanip>
#pragma comment(lib, "shlwapi")

VOID DllHijack(HMODULE hMod) {
  TCHAR tszDllPath[MAX_PATH] = {0};

  GetModuleFileName(hMod, tszDllPath, MAX_PATH);
  PathRemoveFileSpec(tszDllPath);
  PathAppend(tszDllPath, TEXT("SoundCoreBridge.Real.dll"));

  SuperDllHijack(L"SoundCoreBridge.dll", tszDllPath);
}


VOID StarBreaker() {
  //std::ofstream out("./log.log", std::ios::app);
  signature_scanner *sig = new signature_scanner();
  DWORD_PTR address = sig->search("74 ?? 48 8B D3 E8 ?? ?? ?? ?? 48 8B C3");
  delete sig;
  //out << std ::hex << address << std ::endl;
  char* p;
  p = (char*)address;
  *p = 0xEB;
}

BOOL APIENTRY DllMain(HMODULE hModule,
                      DWORD ul_reason_for_call,
                      LPVOID lpReserved) {
  switch (ul_reason_for_call) {
    case DLL_PROCESS_ATTACH:
      DllHijack(hModule);
      StarBreaker();
      break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
      break;
  }
  return TRUE;
}
