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
#include <msclr/marshal_cppstd.h>

using namespace System;
using namespace System::Runtime::InteropServices;
using namespace ::System::Reflection;
using namespace System::IO;
using namespace System::Diagnostics;

// LogLevel enum
public enum class LogLevel {
    Trace,
    Information,
    Warning,
    Error
};

static bool g_initialized = false;
static bool g_enableTraceLogging = false;

// Registry path and key
static const wchar_t* REG_PATH = L"SOFTWARE\\NpsWrapperNET";
static const wchar_t* ENABLE_TRACE_KEY = L"EnableTraceLogging";

// Log name and source constants
public ref class LogConstants abstract sealed
{
public:
    literal System::String^ LOG_NAME = "Application";
    literal System::String^ LOG_SOURCE = "NPS-Wrapper";
};

// Map LogLevel to EventLogEntryType
EventLogEntryType MapLogLevel(LogLevel level) {
    switch (level) {
    case LogLevel::Trace:
        return EventLogEntryType::Information;
    case LogLevel::Information:
        return EventLogEntryType::Information;
    case LogLevel::Warning:
        return EventLogEntryType::Warning;
    case LogLevel::Error:
        return EventLogEntryType::Error;
    default:
        return EventLogEntryType::Information;
    }
}

// Log to Windows Application Event Log, fallback: append error to log file in DLL directory
void LogEvent(LogLevel level, System::String^ message)
{
    if (level == LogLevel::Trace && !g_enableTraceLogging)
        return;
    if (!EventLog::SourceExists(LogConstants::LOG_SOURCE))
    {
        EventLog::CreateEventSource(LogConstants::LOG_SOURCE, LogConstants::LOG_NAME);
    }
    EventLog^ eventLog = gcnew EventLog(LogConstants::LOG_NAME);
    eventLog->Source = LogConstants::LOG_SOURCE;
    if (level == LogLevel::Trace)
        message = String::Concat("[TRACE] ", message);
    eventLog->WriteEntry(message, MapLogLevel(level));
}

// Read EnableTraceLogging from registry
void ReadTraceLoggingSetting()
{
    HKEY hKey;
    DWORD traceVal = 0;
    DWORD dwType = REG_DWORD;
    DWORD dwSize = sizeof(DWORD);
    if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, REG_PATH, 0, KEY_READ, &hKey) == ERROR_SUCCESS)
    {
        if (RegQueryValueExW(hKey, ENABLE_TRACE_KEY, nullptr, &dwType, (LPBYTE)&traceVal, &dwSize) == ERROR_SUCCESS)
        {
            g_enableTraceLogging = (traceVal == 1);
        }
        RegCloseKey(hKey);
    }
}

// Custom assembly resolution method
Assembly^ LocalAssemblyResolver(Object^ sender, ResolveEventArgs^ args)
{
    LogEvent(LogLevel::Trace, "LocalAssemblyResolver called.");
    try
    {
        System::String^ folderPath = Path::GetDirectoryName(Assembly::GetExecutingAssembly()->Location);
        System::String^ assemblyPath = Path::Combine(folderPath, String::Concat(args->Name->Split(',')[0], ".dll"));
        LogEvent(LogLevel::Information, String::Concat("Assembly resolve requested: ", args->Name));

        if (File::Exists(assemblyPath))
        {
            LogEvent(LogLevel::Information, String::Concat("Loading assembly from: ", assemblyPath));
            return Assembly::LoadFrom(assemblyPath);
        }

        LogEvent(LogLevel::Warning, String::Concat("Assembly not found: ", assemblyPath));
        return nullptr;
    }
    catch (Exception^ ex)
    {
        LogEvent(LogLevel::Error, String::Concat("Error in LocalAssemblyResolver: ", ex->ToString()));
        return nullptr;
    }
}

// Get module information for logging
System::String^ GetModuleInfo()
{
    try
    {
        System::String^ modulePath = Assembly::GetExecutingAssembly()->Location;
        if (File::Exists(modulePath))
        {
            FileInfo^ fileInfo = gcnew FileInfo(modulePath);
            return String::Format("({0}, {1} bytes)", 
                fileInfo->LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"), 
                fileInfo->Length);
        }
        return "(info unavailable)";
    }
    catch (Exception^)
    {
        return "(info unavailable)";
    }
}

// Use a static variable to track initialization
void Initialize()
{
    try
    {
        ReadTraceLoggingSetting();
        LogEvent(LogLevel::Information, String::Format("Initializing NpsWrapper {0}", GetModuleInfo()));
        AppDomain::CurrentDomain->AssemblyResolve += gcnew ResolveEventHandler(LocalAssemblyResolver);
        g_initialized = true;
        LogEvent(LogLevel::Information, "NpsWrapper initialized.");
    }
    catch (Exception^ ex)
    {
        LogEvent(LogLevel::Error, String::Concat("Error during Initialize: ", ex->ToString()));
    }
}

void Cleanup()
{
    try
    {
        LogEvent(LogLevel::Information, "Cleaning up NpsWrapper...");
        AppDomain::CurrentDomain->AssemblyResolve -= gcnew ResolveEventHandler(LocalAssemblyResolver);
        g_initialized = false;
        LogEvent(LogLevel::Information, "NpsWrapper cleaned up.");
    }
    catch (Exception^ ex)
    {
        LogEvent(LogLevel::Error, String::Concat("Error during Cleanup: ", ex->ToString()));
    }
}

DWORD WINAPI RadiusExtensionInit(VOID)
{
    LogEvent(LogLevel::Trace, "RadiusExtensionInit called.");
    try
    {
        if (!g_initialized)
            Initialize();
        DWORD result = NpsWrapperNET::NpsWrapper::RadiusExtensionInit();
        LogEvent(LogLevel::Trace, String::Concat("RadiusExtensionInit completed with result: ", result.ToString()));
        return result;
    }
    catch (Exception^ ex)
    {
        LogEvent(LogLevel::Error, String::Concat("Error in RadiusExtensionInit: ", ex->ToString()));
        return ERROR_GEN_FAILURE;
    }
}

VOID WINAPI RadiusExtensionTerm(VOID)
{
    LogEvent(LogLevel::Trace, "RadiusExtensionTerm called.");
    try
    {
        if (g_initialized)
            Cleanup();
        NpsWrapperNET::NpsWrapper::RadiusExtensionTerm();
        LogEvent(LogLevel::Trace, "RadiusExtensionTerm completed.");
    }
    catch (Exception^ ex)
    {
        LogEvent(LogLevel::Error, String::Concat("Error in RadiusExtensionTerm: ", ex->ToString()));
    }
}

DWORD WINAPI RadiusExtensionProcess2(PRADIUS_EXTENSION_CONTROL_BLOCK pECB)
{
    LogEvent(LogLevel::Trace, "RadiusExtensionProcess2 called.");
    try
    {
        if (!g_initialized)
            Initialize();
        DWORD result = NpsWrapperNET::NpsWrapper::RadiusExtensionProcess2(IntPtr(pECB));
        LogEvent(LogLevel::Trace, String::Concat("RadiusExtensionProcess2 completed with result: ", result.ToString()));
        return result;
    }
    catch (Exception^ ex)
    {
        LogEvent(LogLevel::Error, String::Concat("Error in RadiusExtensionProcess2: ", ex->ToString()));
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
