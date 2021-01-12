// This file is part of SignatureScanner.
// It is from another one of my projects which I have released too.



#include "SignatureScanner.h"
#include <Psapi.h>
#include <windows.h>
#include <iomanip>
#include <iostream>

void signature_scanner::proctect_claim(PVOID address, SIZE_T size) {
  // Add a protection
  protections.push(new MemPageProtection(address, size));
}
void signature_scanner::protect_unclaim() { protections.pop(); }
signature_scanner::signature_scanner() {
  // SYSTEM_INFO info;
  // GetSystemInfo(&info);
  // this->BaseAddress = (unsigned long)info.lpMinimumApplicationAddress;
  // Could be injected earlier than expected
  while (!(this->BaseAddress = (DWORD_PTR)GetModuleHandle(NULL)))
    Sleep(100);
  // Getting size of image
  MODULEINFO modinfo;
  while (!GetModuleInformation(GetCurrentProcess(), GetModuleHandle(NULL),
                               &modinfo, sizeof(MODULEINFO)))
    Sleep(100);
  this->ModuleSize = modinfo.SizeOfImage;
  // Wait for the application to finish loading
  // Hook Virtual Protect
  // If its called from a gameguard module then bullshit the reply so it thinks
  // memory access as changed
  // BOOL WINAPI VirtualProtect(
  //  LPVOID lpAddress,
  //  SIZE_T dwSize,
  //  DWORD flNewProtect,
  //  PDWORD lpflOldProtect
  //);
  // Log.Write("Something is trying to change memory protection of %08X size of
  // %u",lpAddress,dwSize);
  //
  MEMORY_BASIC_INFORMATION meminfo;
  while (true) {
    if (VirtualQuery((void *)this->ModuleSize, &meminfo,
                     sizeof(MEMORY_BASIC_INFORMATION)))
      if (!(meminfo.Protect & PAGE_EXECUTE_WRITECOPY))
        break;
    Sleep(100);
  }
};
// Fixed it up so that it dosnt try to cast whats at the location as an unsigned
// long* rather it just passes the pointer back. Unless we have XXXX marking the
// address we want casted into the pointer :).
DWORD_PTR
signature_scanner::search(const char *string, short offset, bool fromStart,
                          DWORD_PTR startAddress, DWORD_PTR *nextAddress) {
  unsigned int p_length = strlen(string);
  
  // Pattern's length
  if (p_length % 2 != 0 || p_length < 2 || !this->BaseAddress ||
      !this->ModuleSize)
    return NULL;
  // Invalid operationk
  unsigned short length = p_length / 2;
  // Number of bytes
  // The buffer is storing the real bytes' values after parsing the string
  unsigned char *buffer = new unsigned char[length];
  SecureZeroMemory(buffer, length);
  // Copy of string
  char *pattern = new char[p_length + 1];
  // +1 for the null terminated string
  ZeroMemory(pattern, p_length + 1);
  strcpy_s(pattern, p_length + 1, string);
  _strupr_s(pattern, p_length + 1);
  // Set vars
  unsigned char f_byte;
  unsigned char s_byte;
  // Parsing of string
  for (unsigned short z = 0; z < length; z++) {
    // Should I toupper these?
    f_byte = pattern[z * 2];
    // First byte
    s_byte = pattern[(z * 2) + 1];
    // Second byte
    if (((f_byte <= 'F' && f_byte >= 'A') ||
         (f_byte <= '9' && f_byte >= '0')) &&
        ((s_byte <= 'F' && s_byte >= 'A') ||
         (s_byte <= '9' && s_byte >= '0'))) {
      if (f_byte <= '9')
        buffer[z] += f_byte - '0';
      else
        buffer[z] += f_byte - 'A' + 10;
      buffer[z] *= 16;
      if (s_byte <= '9')
        buffer[z] += s_byte - '0';
      else
        buffer[z] += s_byte - 'A' + 10;
    } else if (f_byte == 'X' || s_byte == 'X')
      buffer[z] = 'X';
    else
      buffer[z] = '?';
    // Wildcard
  }
  // Remove buffer
  delete[] pattern;
  // Start searching
  unsigned short x;
  DWORD_PTR i = this->BaseAddress;
  if (startAddress != 0)
    i = startAddress;
  MEMORY_BASIC_INFORMATION meminfo;
  DWORD_PTR EOR;
  while (i < this->BaseAddress + this->ModuleSize) {
    VirtualQuery((void *)i, &meminfo, sizeof(MEMORY_BASIC_INFORMATION));
    DWORD dwPage_Protection;
    // Set page protection to readwrite
    VirtualProtect((PVOID)meminfo.BaseAddress, meminfo.RegionSize,
                   PAGE_EXECUTE_READWRITE, &dwPage_Protection);
    EOR = i + meminfo.RegionSize;
    for (; i < EOR; i++) {
      for (x = 0; x < length; x++) {
        if (buffer[x] != ((unsigned char *)i)[x] && buffer[x] != '?' &&
            buffer[x] != 'X')
          break;
      }
      if (x == length) {
        delete[] buffer;
        const char *s_offset = strstr(string, "X");
        if (nextAddress != NULL) {
          *nextAddress = i + 1;
        }
        if (s_offset != NULL) {
          // Set page protection back to what it was origionaly
          VirtualProtect((PVOID)meminfo.BaseAddress, meminfo.RegionSize,
                         dwPage_Protection, NULL);
          return (*(DWORD_PTR *)&(
                     (unsigned char *)i)[length - strlen(s_offset) / 2]) +
                 offset;
          // return (unsigned long)i+ (length-strlen(s_offset)/2);
        } else {
          if (fromStart) {
            // Set page protection back to what it was origionaly
            VirtualProtect((PVOID)meminfo.BaseAddress, meminfo.RegionSize,
                           dwPage_Protection, NULL);
            return (DWORD_PTR)i + offset;
            // return *(unsigned long*)&((unsigned char*)i)[offset];
          } else {
            // Set page protection back to what it was origionaly
            VirtualProtect((PVOID)meminfo.BaseAddress, meminfo.RegionSize,
                           dwPage_Protection, NULL);
            // return *(unsigned long*)&((unsigned char*)i)[length + offset];
            return (DWORD_PTR)i + length + offset;
          }
        }
      }
    }
    // Set page protection back to what it was origionaly
    VirtualProtect((PVOID)meminfo.BaseAddress, meminfo.RegionSize,
                   dwPage_Protection, NULL);
  }
  // Didn't find anything
  delete[] buffer;
  return NULL;
}
bool signature_scanner::find_all(vector_DWORD_PTR &found, const char *string,
                                 short offset, bool fromStart,
                                 DWORD_PTR startAddress) {
  vector_DWORD_PTR::size_type size = found.size();
  DWORD_PTR address = startAddress;
  DWORD_PTR nextAddress;
  nextAddress = startAddress;
  while (address =
             search(string, offset, fromStart, nextAddress, &nextAddress)) {
    found.push_back(address);
  }
  return !size == found.size();
}