// --------------------------------------------------------------------------------------------------------------------
// <copyright>
//   Copyright lestoilfante 2023 (https://github.com/lestoilfante)
//   
//   GNU General Public License version 2.1 (GPLv2.1) 
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
#include "pch.h"
#include <Windows.h>
#include <authif.h>
#include <lmcons.h>
#include "radutil.h"
#include "libloaderapi.h"

using namespace System;
using namespace System::Runtime::InteropServices;
using namespace ::System::Reflection;
using namespace System::IO;

// Custom assembly resolution method
Assembly^ LocalAssemblyResolver(Object^ sender, ResolveEventArgs^ args)
{
    String^ folderPath = Path::GetDirectoryName(Assembly::GetExecutingAssembly()->Location);
    // Construct the full path to the requested assembly
    String^ assemblyPath = Path::Combine(folderPath, args->Name->Split(',')[0] + ".dll");

    // Load the assembly from the specified path
    if (File::Exists(assemblyPath))
        return Assembly::LoadFrom(assemblyPath);

    // Return null if the assembly cannot be resolved
    return nullptr;
}
void Initialize()
{
    // Subscribe to the AssemblyResolve event
    AppDomain::CurrentDomain->AssemblyResolve += gcnew ResolveEventHandler(LocalAssemblyResolver);
}
void Cleanup()
{
    // Unsubscribe from the AssemblyResolve event
    AppDomain::CurrentDomain->AssemblyResolve -= gcnew ResolveEventHandler(LocalAssemblyResolver);
}

DWORD WINAPI RadiusExtensionInit(VOID)
{
    return NpsWrapperNET::NpsWrapper::RadiusExtensionInit();
}

VOID WINAPI RadiusExtensionTerm(VOID)
{
    return NpsWrapperNET::NpsWrapper::RadiusExtensionTerm();
}

DWORD WINAPI RadiusExtensionProcess2(PRADIUS_EXTENSION_CONTROL_BLOCK pECB)
{
    return NpsWrapperNET::NpsWrapper::RadiusExtensionProcess2(IntPtr(pECB));
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
        case DLL_PROCESS_ATTACH:
            Initialize();
            break;
        case DLL_PROCESS_DETACH:
            Cleanup();
            break;
        default:
            break;
    }
    return TRUE;
}
