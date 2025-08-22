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
using namespace System::Diagnostics;

// Log to Windows Application Event Log
void LogEvent(String^ source, String^ message, EventLogEntryType type)
{
    String^ logName = "Application";
    try
    {
        if (!EventLog::SourceExists(source))
        {
            // Creating a new event source requires admin rights
            EventLog::CreateEventSource(source, logName);
        }
        EventLog^ eventLog = gcnew EventLog(logName);
        eventLog->Source = source;
        eventLog->WriteEntry(message, type);
    }
    catch (Exception^ ex)
    {
        // Fallback: log to Trace if event log fails
        Trace::WriteLine("EventLog error: " + ex->ToString());
        Trace::WriteLine("Original log: " + message);
    }
}

// Helper for consistent source name
String^ GetLogSource()
{
    return "NPS-Wrapper";
}

// Custom assembly resolution method
Assembly^ LocalAssemblyResolver(Object^ sender, ResolveEventArgs^ args)
{
    try
    {
        String^ folderPath = Path::GetDirectoryName(Assembly::GetExecutingAssembly()->Location);
        String^ assemblyPath = Path::Combine(folderPath, args->Name->Split(',')[0] + ".dll");
        LogEvent(GetLogSource(), "Assembly resolve requested: " + args->Name, EventLogEntryType::Information);

        if (File::Exists(assemblyPath))
        {
            LogEvent(GetLogSource(), "Loading assembly from: " + assemblyPath, EventLogEntryType::Information);
            return Assembly::LoadFrom(assemblyPath);
        }

        LogEvent(GetLogSource(), "Assembly not found: " + assemblyPath, EventLogEntryType::Warning);
        return nullptr;
    }
    catch (Exception^ ex)
    {
        LogEvent(GetLogSource(), "Error in LocalAssemblyResolver: " + ex->ToString(), EventLogEntryType::Error);
        return nullptr;
    }
}

// Use a static variable to track initialization
static bool g_initialized = false;

void Initialize()
{
    try
    {
        LogEvent(GetLogSource(), "Initializing NpsWrapper...", EventLogEntryType::Information);
        AppDomain::CurrentDomain->AssemblyResolve += gcnew ResolveEventHandler(LocalAssemblyResolver);
        g_initialized = true;
        LogEvent(GetLogSource(), "NpsWrapper initialized.", EventLogEntryType::Information);
    }
    catch (Exception^ ex)
    {
        LogEvent(GetLogSource(), "Error during Initialize: " + ex->ToString(), EventLogEntryType::Error);
    }
}

void Cleanup()
{
    try
    {
        LogEvent(GetLogSource(), "Cleaning up NpsWrapper...", EventLogEntryType::Information);
        AppDomain::CurrentDomain->AssemblyResolve -= gcnew ResolveEventHandler(LocalAssemblyResolver);
        g_initialized = false;
        LogEvent(GetLogSource(), "NpsWrapper cleaned up.", EventLogEntryType::Information);
    }
    catch (Exception^ ex)
    {
        LogEvent(GetLogSource(), "Error during Cleanup: " + ex->ToString(), EventLogEntryType::Error);
    }
}

DWORD WINAPI RadiusExtensionInit(VOID)
{
    LogEvent(GetLogSource(), "RadiusExtensionInit called.", EventLogEntryType::Information);
    try
    {
        if (!g_initialized)
            Initialize();
        DWORD result = NpsWrapperNET::NpsWrapper::RadiusExtensionInit();
        LogEvent(GetLogSource(), "RadiusExtensionInit completed with result: " + result, EventLogEntryType::Information);
        return result;
    }
    catch (Exception^ ex)
    {
        LogEvent(GetLogSource(), "Error in RadiusExtensionInit: " + ex->ToString(), EventLogEntryType::Error);
        return ERROR_GEN_FAILURE;
    }
}

VOID WINAPI RadiusExtensionTerm(VOID)
{
    LogEvent(GetLogSource(), "RadiusExtensionTerm called.", EventLogEntryType::Information);
    try
    {
        if (g_initialized)
            Cleanup();
        NpsWrapperNET::NpsWrapper::RadiusExtensionTerm();
        LogEvent(GetLogSource(), "RadiusExtensionTerm completed.", EventLogEntryType::Information);
    }
    catch (Exception^ ex)
    {
        LogEvent(GetLogSource(), "Error in RadiusExtensionTerm: " + ex->ToString(), EventLogEntryType::Error);
    }
}

DWORD WINAPI RadiusExtensionProcess2(PRADIUS_EXTENSION_CONTROL_BLOCK pECB)
{
    LogEvent(GetLogSource(), "RadiusExtensionProcess2 called.", EventLogEntryType::Information);
    try
    {
        if (!g_initialized)
            Initialize();
        DWORD result = NpsWrapperNET::NpsWrapper::RadiusExtensionProcess2(IntPtr(pECB));
        LogEvent(GetLogSource(), "RadiusExtensionProcess2 completed with result: " + result, EventLogEntryType::Information);
        return result;
    }
    catch (Exception^ ex)
    {
        LogEvent(GetLogSource(), "Error in RadiusExtensionProcess2: " + ex->ToString(), EventLogEntryType::Error);
        return ERROR_GEN_FAILURE;
    }
}

// DllMain should not call managed code
BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    // Do not call Initialize or Cleanup here to avoid running managed code under loader lock
    // No managed code should be called here!
    return TRUE;
}

// --------------------------------------------------------------------------------------------------------------------
