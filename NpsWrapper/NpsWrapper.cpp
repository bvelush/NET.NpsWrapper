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

// Use a static variable to track initialization
void Initialize()
{
    try
    {
        ReadTraceLoggingSetting();
        LogEvent(LogLevel::Information, "Initializing NpsWrapper...");
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


#define RADIUS_ATTRIBUTE_VENDOR_SPECIFIC 26
#define MICROSOFT_VENDOR_ID 311
#define RDG_RESOURCE_ID_SUBTYPE 2418
DWORD WINAPI RadiusExtensionProcess2(PRADIUS_EXTENSION_CONTROL_BLOCK pECB)
{
    LogEvent(LogLevel::Trace, "RadiusExtensionProcess2 called.");
    try
    {
        if (!g_initialized)
            Initialize();

        PRADIUS_ATTRIBUTE_ARRAY pAttrArray = pECB->GetRequest(pECB);
        if (pAttrArray)
		{
            DWORD attrCount = pAttrArray->GetSize(pAttrArray);
            LogEvent(LogLevel::Trace, "Processing RADIUS attributes, there are " + attrCount.ToString());
            for (DWORD i = 0; i < attrCount; ++i)
            {
                const RADIUS_ATTRIBUTE* pAttr = pAttrArray->AttributeAt(pAttrArray, i);
                if (pAttr && pAttr->dwAttrType == RADIUS_ATTRIBUTE_VENDOR_SPECIFIC && pAttr->cbDataLength > 8)  
                {
                    const BYTE* pData = pAttr->lpValue; // Value pointer in the attribute
                    // Vendor ID is 4 bytes, big-endian (RFC format)
                    uint32_t vendorId = (pData[0] << 24) | (pData[1] << 16) | (pData[2] << 8) | pData[3];
                    if (vendorId == MICROSOFT_VENDOR_ID)
                    {
                        // VSA format: [VendorID (4)][Vendor-Type (1)][Vendor-Length (1)][Value ...]
                        BYTE vendorType = pData[4];
				        LogEvent(LogLevel::Trace, String::Concat("Attribute ", i.ToString(), ", Length=", pAttr->cbDataLength.ToString(), " Subtype=", vendorType.ToString()));
                        if (vendorType == RDG_RESOURCE_ID_SUBTYPE)
                        {
                            BYTE vendorLen = pData[5];
                            // Value at pData+6, length vendorLen-2
                            int valueLen = vendorLen - 2;
                            if (valueLen > 0 && (6 + valueLen) <= (int)pAttr->cbDataLength)
                            {
                                // The value is usually a Unicode string (wchar_t), but may be ASCII.
                                const wchar_t* wszResourceId = (const wchar_t*)(pData + 6);
                                int wcharLen = valueLen / sizeof(wchar_t);
                                std::wstring wsResourceId(wszResourceId, wcharLen);
                                wprintf(L"RDG Resource ID MS-VSA 2418: %s\n", wsResourceId.c_str());
								LogEvent(LogLevel::Information, String::Concat("RDG Resource ID MS-VSA 2418: ", msclr::interop::marshal_as<String^>(wsResourceId)));
                            }
                        }
                    }
                }
            }
        }

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
