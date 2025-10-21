// --------------------------------------------------------------------------------------------------------------------
// <copyright>
//   Copyright lestoilfante 2023 (https://github.com/lestoilfante)
//   
//   GNU General Public License version 2.1 (GPLv2.1) 
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
using System;
using OpenCymd.Nps.Plugin;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Omni2FA.AuthClient;
using System.DirectoryServices.AccountManagement;
using Microsoft.Win32;

namespace Omni2FA.NPS.Adapter {
    public enum LogLevel {
        Trace,
        Information,
        Warning,
        Error
    }

    /// <summary>
    /// Provides the entry points for the NPS service (indirectly called by the C++/CLR wrapper).
    /// </summary>
    public class NpsAdapter {
        const string APP_NAME = "Omni2FA.Adapter";

        private static int initCount = 0;
        // Add a static field for the authenticator
        private static Authenticator _authenticator;
        // Store NoMFA group SIDs
        private static HashSet<string> _noMfaGroupSids = new HashSet<string>();
        private static bool _enableTraceLogging = false;
        // Store MFA-enabled NPS policy name
        private static string _mfaEnabledNpsPolicy = string.Empty;
        // Registry path and value name for NoMFA groups
        // [HKEY_LOCAL_MACHINE\SOFTWARE\Omni2FA.NPS]
        // "NoMfaGroups"="Group1;Group2;Group3"
        private const string _regPath = @"SOFTWARE\\Omni2FA.NPS";
        private const string _noMfaKey = "NoMfaGroups";
        private const string _enableTraceLoggingKey = "EnableTraceLogging";
        private const string _mfaEnabledNpsPolicyKey = "MfaEnabledNPSPolicy";

        /// <summary>
        /// <para>Called by NPS while the service is starting up</para>
        /// <remarks>Use RadiusExtensionInit to perform any initialization operations for the Extension DLL</remarks>
        /// </summary>
        /// <returns>A return value other then 0 will cause NPS to fail to start</returns>
        public static uint RadiusExtensionInit() {
            // Log component initialization with datetime and size
            var moduleInfo = GetModuleInfo();
            WriteEventLog(LogLevel.Information, $"Initializing Omni2FA.Adapter {moduleInfo}");
            WriteEventLog(LogLevel.Trace, "RadiusExtensionInit called");
            
            if (initCount == 0) {
                initCount++;
                _authenticator = new Authenticator();
                try {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(_regPath)) {
                        if (key != null) {
                            // Read trace logging setting
                            var traceValue = key.GetValue(_enableTraceLoggingKey);
                            if (traceValue != null && int.TryParse(traceValue.ToString(), out int traceInt)) {
                                _enableTraceLogging = (traceInt == 1);
                            }

                            // Read MFA-enabled NPS policy name
                            var mfaPolicyValue = key.GetValue(_mfaEnabledNpsPolicyKey) as string;
                            if (!string.IsNullOrEmpty(mfaPolicyValue)) {
                                _mfaEnabledNpsPolicy = mfaPolicyValue.Trim();
                                WriteEventLog(LogLevel.Information, $"MFA-enabled NPS policy set to: {_mfaEnabledNpsPolicy}");
                            } else {
                                WriteEventLog(LogLevel.Information, "MfaEnabledNPSPolicy registry value is empty or missing.");
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
                    bool performMfa = true;
                    var policyName = AttributeLookup(control.Request, RadiusAttributeType.PolicyName);
                    
                    // Check if we should perform MFA based on policy configuration
                    if (!string.IsNullOrEmpty(_mfaEnabledNpsPolicy)) {
                        // MFA policy is configured - only perform MFA if current policy matches
                        if (string.IsNullOrEmpty(policyName) || 
                            !string.Equals(policyName, _mfaEnabledNpsPolicy, StringComparison.OrdinalIgnoreCase)) {
                            performMfa = false;
                            WriteEventLog(LogLevel.Information, $"Policy '{policyName}' does NOT match MFA-enabled policy '{_mfaEnabledNpsPolicy}', skipping MFA.");
                        } else {
                            WriteEventLog(LogLevel.Trace, $"Policy '{policyName}' matches MFA-enabled policy '{_mfaEnabledNpsPolicy}', MFA will be performed.");
                        }
                    } else {
                        // No MFA policy configured - always perform MFA (secure default)
                        WriteEventLog(LogLevel.Trace, $"No MFA-enabled policy configured, MFA will be performed for all requests (secure default).");
                    }

                    if (performMfa) {
                        try {
                            userName = AttributeLookup(control.Request, RadiusAttributeType.UserName).Trim();
                            // Check if user is in NoMFA group by SID
                            PrincipalContext ctx;
                            string samAccountName = userName;
                            string domain = null;
                            var parts = userName.Split('\\');
                            if (parts.Length == 2) {
                                domain = parts[0];
                                samAccountName = parts[1];
                                ctx = new PrincipalContext(ContextType.Domain, domain);
                            }
                            else {
                                ctx = new PrincipalContext(ContextType.Domain);
                            }
                            using (ctx) {
                                UserPrincipal user = UserPrincipal.FindByIdentity(ctx, samAccountName);
                                if (user != null) {
                                    var userGroups = user.GetAuthorizationGroups();
                                    foreach (var group in userGroups) {
                                        var sid = group.Sid?.Value;
                                        if (sid != null && _noMfaGroupSids.Contains(sid)) {
                                            performMfa = false;
                                            WriteEventLog(LogLevel.Information, $"User {userName} is in NoMFA group '{group.Name}', skipping MFA.");
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex) {
                            WriteEventLog(LogLevel.Warning, $"Error checking NoMFA group membership for user '{userName}': {ex.Message}");
                        }
                    }
                    
                    if (performMfa) {
                        bool resMfa = _authenticator.AuthenticateAsync(userName).Result;
                        if (resMfa) {
                            /* Keep final disposition to AccessAccept - Note that could be changed by other extensions */
                            control.ResponseType = RadiusCode.AccessAccept;
                            WriteEventLog(LogLevel.Information, $"MFA succeeded for user {userName}");
                        }
                        else {
                            /* Set final disposition to AccessReject - Note that could be changed by other extensions */
                            control.ResponseType = RadiusCode.AccessReject;
                            WriteEventLog(LogLevel.Warning, $"MFA failed for user {userName}");
                        }
                    } else {
                        control.ResponseType = RadiusCode.AccessAccept;
                        WriteEventLog(LogLevel.Information, $"MFA skipped for user {userName}, accepting request.");
                    }
                }
            }
            return 0;
        }

        private static string sanitizeString(string input) {
            // for some reason, stirng.Trim() does not remove trailing \0 char
            if (input[input.Length - 1] == '\0')
                return input.Substring(0, input.Length - 1); // Remove trailing char
            return input.Trim();
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
                logMessage.Add($"-Connection Request Policy Name: '{AttributeLookup(control.Request, RadiusAttributeType.CRPPolicyName)}'");
                logMessage.Add($"-Network Policy Name: '{AttributeLookup(control.Request, RadiusAttributeType.PolicyName)}'");
            }
            WriteEventLog(LogLevel.Trace, "Authorization request", logMessage);

            string s = "";
            foreach (var attr in AttributesToList(control.Request)) {
                s += " | " + sanitizeString(attr);
            }
            WriteEventLog(LogLevel.Trace, $"Request components: {s}");


            s = "";
            foreach (var attr in AttributesToList(control.Response[RadiusCode.AccessAccept])) {
                s += " ~ " + sanitizeString(attr);
            }
            WriteEventLog(LogLevel.Trace, $"Response components: {s}");
        }

        private static string AttributeLookup(IList<RadiusAttribute> attributesList, RadiusAttributeType attributeType) {
            var a = attributesList.FirstOrDefault(x => x.AttributeId.Equals((int)attributeType));
            if (a == null)
                return string.Empty;
            var ret_val = (a.Value is byte[] val) ? Encoding.Default.GetString(val) : a.Value.ToString();
            return sanitizeString(ret_val);
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

        /// <summary>
        /// Gets module information (datetime and size) for logging
        /// </summary>
        /// <returns>Formatted string with datetime and size</returns>
        private static string GetModuleInfo() {
            try {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var location = assembly.Location;
                if (System.IO.File.Exists(location)) {
                    var fileInfo = new System.IO.FileInfo(location);
                    return $"({fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}, {fileInfo.Length} bytes)";
                }
                return "(info unavailable)";
            } catch {
                return "(info unavailable)";
            }
        }
    }
}

