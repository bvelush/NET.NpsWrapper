// --------------------------------------------------------------------------------------------------------------------
// --------------------------------------------------------------------------------------------------------------------
// <copyright>
//   Copyright bvelush 2025 (https://github.com/bvelush)
//   
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
static const wchar_t* REG_PATH = L"SOFTWARE\\SMK2FA.NPS";
static const wchar_t* ENABLE_TRACE_KEY = L"EnableTraceLogging";

// Log name and source constants
public ref class LogConstants abstract sealed
{
public:
    literal System::String^ LOG_NAME = "Application";
    literal System::String^ LOG_SOURCE = "SMK2FA.NPS.Plugin";
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
void LogEvent(LogLevel level, int eventCode, System::String^ message)
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
    eventLog->WriteEntry(message, MapLogLevel(level), eventCode);
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
    LogEvent(LogLevel::Trace, 7, "LocalAssemblyResolver called.");
    try
    {
        System::String^ folderPath = Path::GetDirectoryName(Assembly::GetExecutingAssembly()->Location);
        System::String^ assemblyPath = Path::Combine(folderPath, String::Concat(args->Name->Split(',')[0], ".dll"));
        LogEvent(LogLevel::Information, 200, String::Concat("Assembly resolve requested: ", args->Name));

        if (File::Exists(assemblyPath))
        {
            LogEvent(LogLevel::Information, 200, String::Concat("Loading assembly from: ", assemblyPath));
            return Assembly::LoadFrom(assemblyPath);
        }

        LogEvent(LogLevel::Warning, 300, String::Concat("Assembly not found: ", assemblyPath));
        return nullptr;
    }
    catch (Exception^ ex)
    {
        LogEvent(LogLevel::Error, 400, String::Concat("Error in LocalAssemblyResolver: ", ex->ToString()));
        return nullptr;
    }
}

// Get module version information from Git-based versioning for logging
System::String^ GetModuleInfo()
{
    try
    {
        Assembly^ assembly = Assembly::GetExecutingAssembly();
        
        // Try to get the informational version which includes Git info
        array<Object^>^ infoVersionAttrs = assembly->GetCustomAttributes(AssemblyInformationalVersionAttribute::typeid, false);
        if (infoVersionAttrs != nullptr && infoVersionAttrs->Length > 0)
        {
            AssemblyInformationalVersionAttribute^ infoVersionAttr = 
                safe_cast<AssemblyInformationalVersionAttribute^>(infoVersionAttrs[0]);
            
            if (!String::IsNullOrEmpty(infoVersionAttr->InformationalVersion))
            {
                // Format: "1.0.0+abc1234" or similar from setuptools_scm-like versioning
                return String::Format("v{0}", infoVersionAttr->InformationalVersion);
            }
        }
        
        // Fallback to standard version attributes
        Version^ version = assembly->GetName()->Version;
        array<Object^>^ fileVersionAttrs = assembly->GetCustomAttributes(AssemblyFileVersionAttribute::typeid, false);
        
        if (fileVersionAttrs != nullptr && fileVersionAttrs->Length > 0)
        {
            AssemblyFileVersionAttribute^ fileVersionAttr = 
                safe_cast<AssemblyFileVersionAttribute^>(fileVersionAttrs[0]);
            
            // Check if GitVersionInformation class is available (from generated VersionInfo.cs)
            Type^ gitVersionType = assembly->GetType("GitVersionInformation");
            if (gitVersionType != nullptr)
            {
                try
                {
                    FieldInfo^ commitHashField = gitVersionType->GetField("CommitHash");
                    FieldInfo^ isCleanField = gitVersionType->GetField("IsClean");
                    FieldInfo^ distanceField = gitVersionType->GetField("CommitDistance");
                    
                    if (commitHashField != nullptr && isCleanField != nullptr && distanceField != nullptr)
                    {
                        System::String^ commitHash = safe_cast<System::String^>(commitHashField->GetValue(nullptr));
                        bool isClean = safe_cast<bool>(isCleanField->GetValue(nullptr));
                        int distance = safe_cast<int>(distanceField->GetValue(nullptr));
                        
                        System::String^ cleanStatus = isClean ? "clean" : "dirty";
                        System::String^ distanceInfo = distance > 0 ? String::Format("+{0}", distance) : "";
                        
                        return String::Format("v{0} ({1}, {2}{3})", 
                            fileVersionAttr->Version, 
                            commitHash, 
                            cleanStatus, 
                            distanceInfo);
                    }
                }
                catch (Exception^)
                {
                    // Fall through to simpler version
                }
            }
            
            return String::Format("v{0}", fileVersionAttr->Version);
        }
        
        if (version != nullptr)
        {
            return String::Format("v{0}", version->ToString());
        }
        
        return "(version unavailable)";
    }
    catch (Exception^)
    {
        return "(version unavailable)";
    }
}

// Use a static variable to track initialization
void Initialize()
{
    try
    {
        ReadTraceLoggingSetting();
        LogEvent(LogLevel::Information, 100, String::Format("Initializing Omni2FA.NPS.Plugin {0}", GetModuleInfo()));
        AppDomain::CurrentDomain->AssemblyResolve += gcnew ResolveEventHandler(LocalAssemblyResolver);
        g_initialized = true;
        LogEvent(LogLevel::Information, 101, "Omni2FA.NPS.Plugin initialized.");
    }
    catch (Exception^ ex)
    {
        LogEvent(LogLevel::Error, 401, String::Concat("Error during Initialize: ", ex->ToString()));
    }
}

void Cleanup()
{
    try
    {
        LogEvent(LogLevel::Information, 110, "Cleaning up Omni2FA.NPS.Plugin...");
        AppDomain::CurrentDomain->AssemblyResolve -= gcnew ResolveEventHandler(LocalAssemblyResolver);
        g_initialized = false;
        LogEvent(LogLevel::Information, 111, "Omni2FA.NPS.Plugin cleaned up.");
    }
    catch (Exception^ ex)
    {
        LogEvent(LogLevel::Error, 402, String::Concat("Error during Cleanup: ", ex->ToString()));
    }
}

DWORD WINAPI RadiusExtensionInit(VOID)
{
    LogEvent(LogLevel::Trace, 1, "RadiusExtensionInit called.");
    try
    {
        if (!g_initialized)
            Initialize();
        DWORD result = Omni2FA::Adapter::NpsAdapter::RadiusExtensionInit();
        LogEvent(LogLevel::Trace, 4, String::Concat("RadiusExtensionInit completed with result: ", result.ToString()));
        return result;
    }
    catch (Exception^ ex)
    {
        LogEvent(LogLevel::Error, 403, String::Concat("Error in RadiusExtensionInit: ", ex->ToString()));
        return ERROR_GEN_FAILURE;
    }
}

VOID WINAPI RadiusExtensionTerm(VOID)
{
    LogEvent(LogLevel::Trace, 2, "RadiusExtensionTerm called.");
    try
    {
        if (g_initialized)
            Cleanup();
        Omni2FA::Adapter::NpsAdapter::RadiusExtensionTerm();
        LogEvent(LogLevel::Trace, 5, "RadiusExtensionTerm completed.");
    }
    catch (Exception^ ex)
    {
        LogEvent(LogLevel::Error, 404, String::Concat("Error in RadiusExtensionTerm: ", ex->ToString()));
    }
}

DWORD WINAPI RadiusExtensionProcess2(PRADIUS_EXTENSION_CONTROL_BLOCK pECB)
{
	LogEvent(LogLevel::Trace, 3, "RadiusExtensionProcess2 called.");
    try
    {
        if (!g_initialized)
            Initialize();
        DWORD result = Omni2FA::Adapter::NpsAdapter::RadiusExtensionProcess2(IntPtr(pECB));
        LogEvent(LogLevel::Trace, 6, String::Concat("RadiusExtensionProcess2 completed with result: ", result.ToString()));
        return result;
    }
    catch (Exception^ ex)
    {
        LogEvent(LogLevel::Error, 405, String::Concat("Error in RadiusExtensionProcess2: ", ex->ToString()));
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
