// --------------------------------------------------------------------------------------------------------------------
// <copyright>
//   Copyright lestoilfante 2023 (https://github.com/lestoilfante)
//   
//   GNU General Public License version 2.1 (GPLv2.1) 
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
using Microsoft.Win32;
using OpenCymd.Nps.Plugin;
using OpenCymd.Nps.Plugin.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
//using System.Runtime.Serialization.Json;

namespace NpsWrapperNET {
    public enum LogLevel {
        Trace,
        Information,
        Warning,
        Error
    }

    /// <summary>
    /// Provides the entry points for the NPS service (indirectly called by the C++/CLR wrapper).
    /// </summary>
    public class NpsWrapper {
        const string APP_NAME = "NPS-Wrapper.NET";

        private static int initCount = 0;
        // Add a static field for the authenticator
        // Store NoMFA group SIDs
        private static HashSet<string> _noMfaGroupSids = new HashSet<string>();
        private static bool _enableTraceLogging = false;
        // Registry path and value name for NoMFA groups
        // [HKEY_LOCAL_MACHINE\SOFTWARE\NpsWrapperNET]
        // "NoMfaGroups"="Group1;Group2;Group3"
        private const string _regPath = @"SOFTWARE\\NpsWrapperNET";
        private const string _noMfaKey = "NoMfaGroups";
        private const string _enableTraceLoggingKey = "EnableTraceLogging";

        /// <summary>
        /// <para>Called by NPS while the service is starting up</para>
        /// <remarks>Use RadiusExtensionInit to perform any initialization operations for the Extension DLL</remarks>
        /// </summary>
        /// <returns>A return value other then 0 will cause NPS to fail to start</returns>
        public static uint RadiusExtensionInit() {
            WriteEventLog(LogLevel.Trace, "RadiusExtensionInit called");
            if (initCount == 0) {
                initCount++;
                try {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(_regPath)) {
                        if (key != null) {
                            // Read trace logging setting
                            var traceValue = key.GetValue(_enableTraceLoggingKey);
                            if (traceValue != null && int.TryParse(traceValue.ToString(), out int traceInt)) {
                                _enableTraceLogging = (traceInt == 1);
                            }

                            // Read NoMFA group names and resolve to SIDs
                            var groupNames = key.GetValue(_noMfaKey) as string;
                            if (!string.IsNullOrEmpty(groupNames)) {
                                foreach (var groupName in groupNames.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)) {
                                    string trimmedName = groupName.Trim();
                                    try {
                                        using (PrincipalContext ctx = new PrincipalContext(ContextType.Domain)) {
                                            GroupPrincipal group = GroupPrincipal.FindByIdentity(ctx, trimmedName);
                                            if (group != null) {
                                                var sid = group.Sid.Value;
                                                _noMfaGroupSids.Add(sid);
                                                WriteEventLog(LogLevel.Information, $"NoMFA group added: {trimmedName} (SID: {sid})");
                                            } else {
                                                WriteEventLog(LogLevel.Warning, $"NoMFA group not found: {trimmedName}");
                                            }
                                        }
                                    } catch (Exception ex) {
                                        WriteEventLog(LogLevel.Warning, $"Error resolving group '{trimmedName}': {ex.Message}");
                                    }
                                }
                            } else {
                                WriteEventLog(LogLevel.Warning, "NoMfaGroups registry value is empty or missing.");
                            }
                        } else {
                            WriteEventLog(LogLevel.Warning, $"Registry key not found: {_regPath}");
                        }
                    }
                } catch (Exception ex) {
                    WriteEventLog(LogLevel.Warning, $"Error reading registry during initialization: {ex.Message}");
                }
            }
            return 0;
        }

        /// <summary>
        /// <para>Called by NPS prior to unloading the Extension DLL</para>
        /// <remarks>Use RadiusExtensionTerm to perform any clean-up operations for the Extension DLL</remarks>
        /// </summary>
        public static void RadiusExtensionTerm() {
            initCount--;
            if (initCount == 0) {
                WriteEventLog(LogLevel.Trace, "RadiusExtensionInit called");
            }
        }
        /// <summary>
        /// Called by the NPS host to process an authentication or authorization request.
        /// </summary>
        /// <param name="ecbPointer">Pointer to the extension control block.</param>
        /// <returns>0 if all plugins were processed successfully or 5 (access denied) when at least one of the plugins failed.</returns>
        public static uint RadiusExtensionProcess2(IntPtr ecbPointer) {
            var control = new ExtensionControl(ecbPointer);

            //// Get the attribute array pointer from the control block
            //IntPtr attrArrayPtr = ecb.GetRequest(ecbPtr);

            //// Define the RADIUS_ATTRIBUTE_ARRAY interface
            //[UnmanagedFunctionPointer(CallingConvention.StdCall)]
            //public delegate uint GetCountDelegate(IntPtr arrPtr);
            //[UnmanagedFunctionPointer(CallingConvention.StdCall)]
            //public delegate uint GetAtDelegate(IntPtr arrPtr, uint index, out IntPtr pAttr);

            //// Get delegates from the vtable of attrArrayPtr...
            //// (You may need to wrap in C++/CLI or use C# Unsafe code for this part)

            //uint count = getCount(attrArrayPtr);
            //for (uint i = 0; i < count; i++) {
            //    IntPtr attrPtr;
            //    getAt(attrArrayPtr, i, out attrPtr);
            //    RADIUS_ATTRIBUTE attr = Marshal.PtrToStructure<RADIUS_ATTRIBUTE>(attrPtr);

            //    if (attr.dwAttrType == 2418 /* ResourceId */) {
            //        string resourceId = Marshal.PtrToStringUni(attr.pValue, (int)attr.cbValue / 2);
            //        Console.WriteLine($"RDG ResourceId: {resourceId}");
            //    }
            //}

            string userName = string.Empty;
            /* 
             * Authorization request 
             *      -ExtensionPoint: Authorization
             *      -RequestType: AccessRequest
             *      -ResponseType: AccessAccept
             */
            if ((control.ExtensionPoint == RadiusExtensionPoint.Authorization) && (control.RequestType == RadiusCode.AccessRequest)) {
                if (control.ResponseType == RadiusCode.AccessAccept) {
                    /*
                     * Call MFA on already authorized request
                     * If MFA returns OK keep original disposition -> AccessAccept disposition
                     * If MFA returns KO override original disposition -> AccessReject 
                     */
                    logRequest(control);
                    userName = AttributeLookup(control.Request, RadiusAttributeType.UserName);
                    var resource = AttributeLookup(control.Request, RadiusAttributeType.MS_RDGateway_ResourceId);

                    // Call REST API for MFA
                    var requestId = Guid.NewGuid().ToString();
                    var payload = new {
                        request_id = requestId,
                        samid = userName,
                        requestor = "SMK"
                    };

                    try {
                        using (var client = new HttpClient()) {
                            var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                            var content = new StringContent(json, Encoding.UTF8, "application/json");
                            //var response = client.PostAsync("http://localhost:8000/Authenticate", content).Result;
                            //if (!response.IsSuccessStatusCode) {
                            //    WriteEventLog(LogLevel.Error, $"MFA REST call failed: {response.StatusCode}");
                            //    // Optionally override disposition here
                            //}

                            //var responseContent = response.Content.ReadAsStringAsync().Result;
                            var sleepTime = 10; 
                            System.Threading.Thread.Sleep(sleepTime * 1000);
                            WriteEventLog(LogLevel.Trace, $"after sleep, {sleepTime}, {userName}, {resource}");
                            control.ResponseType = RadiusCode.AccessAccept;
                        }
                    } catch (Exception ex) {
                        WriteEventLog(LogLevel.Error, $"MFA REST call exception: {ex.Message}");
                        // Optionally override disposition here
                    }
                }
            }
            return 0;  // control.ResponseType = RadiusCode.AccessAccept;
        }

        private static void logRequest(ExtensionControl control) {
            List<string> logMessage = new List<string>
{
                "NPS request start",
                "-ExtensionPoint: " + control.ExtensionPoint.ToString(),
                "-RequestType: " + control.RequestType.ToString(),
                "-ResponseType: " + control.ResponseType.ToString()
            };

            WriteEventLog(LogLevel.Trace, "RadiusExtensionProcess2 called with params:", logMessage);

            var userName = AttributeLookup(control.Request, RadiusAttributeType.UserName);
            if (!string.IsNullOrEmpty(userName)) {
                logMessage.Add("-UserName: " + userName);
                logMessage.Add("-NAS IPAddress: " + AttributeLookup(control.Request, RadiusAttributeType.NASIPAddress));
                logMessage.Add("-Src IPAddress: " + AttributeLookup(control.Request, RadiusAttributeType.SrcIPAddress));
                logMessage.Add("-Connection Request Policy Name: " + AttributeLookup(control.Request, RadiusAttributeType.CRPPolicyName));
                logMessage.Add("-Network Policy Name: " + AttributeLookup(control.Request, RadiusAttributeType.PolicyName));
                logMessage.Add("==RDG Destination: " + AttributeLookup(control.Request, RadiusAttributeType.MS_RDGateway_ResourceId));
            }
            WriteEventLog(LogLevel.Trace, "Authorization request", logMessage);

            string s = "";
            foreach (var attr in AttributesToList(control.Request)) {
                s += " | " + attr;
            }
            WriteEventLog(LogLevel.Trace, $"Request components: {s}");

            s = "";
            foreach (var attr in AttributesToList(control.Response[RadiusCode.AccessAccept])) {
                s += " ~ " + attr;
            }
            WriteEventLog(LogLevel.Trace, $"Response components: {s}");
        }

        private static string AttributeLookup(IList<RadiusAttribute> attributesList, RadiusAttributeType attributeType) {
            var a = attributesList.FirstOrDefault(x => x.AttributeId.Equals((int)attributeType));
            if (a == null)
                return string.Empty;
            return (a.Value is byte[] val) ? Encoding.Default.GetString(val) : a.Value.ToString();
        }
        /* Get all attributes*/
        private static List<string> AttributesToList(IList<RadiusAttribute> attributesList) {
            var r = new List<string>();
            foreach (var attrib in attributesList) {
                string attribName = (Enum.IsDefined(typeof(RadiusAttributeType), attrib.AttributeId)) ?
                    ((RadiusAttributeType)attrib.AttributeId).ToString()
                    : attrib.AttributeId.ToString();
                string attribValue = attrib.Value is byte[] val ? Encoding.Default.GetString(val) : attrib.Value.ToString();
                r.Add($"{attribName}: {attribValue}");
            }
            return r;
        }
        /// <summary>
        /// Writes Windows Event Log (Application)
        /// </summary>
        /// <param name="src">Event Source Name</param>
        /// <param name="subj">Event first row</param>
        /// <param name="subj_body">Event additional rows to append</param>
        /// <param name="level">Event Level</param>
        private static void WriteEventLog(LogLevel level, string subj, List<string> subj_body = null) {
            EventLogEntryType winLevel;
            switch (level) {
                case LogLevel.Trace:
                    if (!_enableTraceLogging) {
                        return;
                    }
                    subj = "[TRACE] " + subj;
                    winLevel = EventLogEntryType.Information;
                    break;
                case LogLevel.Information:
                    winLevel = EventLogEntryType.Information;
                    break;
                case LogLevel.Warning:
                    winLevel = EventLogEntryType.Warning;
                    break;
                case LogLevel.Error:
                    winLevel = EventLogEntryType.Error;
                    break;
                default:
                    winLevel = EventLogEntryType.Information;
                    break;
            }

            if (subj_body == null) {
                subj_body = new List<string>();
            }
            using (EventLog eventLog = new EventLog("Application")) {
                eventLog.Source = APP_NAME;
                EventInstance eventInstance = new EventInstance(0, 0, winLevel);
                var body = string.Join(Environment.NewLine, subj_body);
                EventLog.WriteEvent(eventLog.Source, eventInstance, new List<string>() { subj + Environment.NewLine + body }.ToArray());
            }
        }
    }
}
