// --------------------------------------------------------------------------------------------------------------------
// <copyright>
//   Copyright lestoilfante 2023 (https://github.com/lestoilfante)
//   
//   GNU General Public License version 2.1 (GPLv2.1) 
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
using OpenCymd.Nps.Plugin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;

namespace Omni2FA.Net.Utils {
    /// <summary>
    /// Helper class for writing to Windows Event 
    /// </summary>
    public static class Log {
        private const string APP_NAME = "Omni2FA.Adapter";
        private static bool _enableTraceLogging = false;

        public enum Level {
            Trace,
            Information,
            Warning,
            Error
        }

        /// <summary>
        /// Sets whether trace logging is enabled.
        /// </summary>
        /// <param name="enabled">True to enable trace logging, false to disable it</param>
        public static void SetTraceLoggingEnabled(bool enabled) {
            _enableTraceLogging = enabled;
        }

        /// <summary>
        /// Writes Windows Event Log (Application)
        /// </summary>
        /// <param name="level">Event Level</param>
        /// <param name="eventCode">Event code for filtering (see EventCodes.md for reference)</param>
        /// <param name="subj">Event first row</param>
        /// <param name="subj_body">Event additional rows to append</param>
        public static void Event(Level level, int eventCode, string subj, List<string> subj_body = null) {
            EventLogEntryType winLevel;
            switch (level) {
                case Level.Trace:
                    if (!_enableTraceLogging) {
                        return;
                    }
                    subj = "[TRACE] " + subj;
                    winLevel = EventLogEntryType.Information;
                    break;
                case Level.Information:
                    winLevel = EventLogEntryType.Information;
                    break;
                case Level.Warning:
                    winLevel = EventLogEntryType.Warning;
                    break;
                case Level.Error:
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
                EventInstance eventInstance = new EventInstance(eventCode, 0, winLevel);
                var body = string.Join(Environment.NewLine, subj_body);
                EventLog.WriteEvent(eventLog.Source, eventInstance, new List<string>() { subj + Environment.NewLine + body }.ToArray());
            }
        }

        public static void Event(Level level, int eventCode, string subj, Exception ex) {
            var exceptionDetails = new List<string>();

            // Unwrap all exception details
            Exception currentEx = ex;
            int level_depth = 0;
            while (currentEx != null) {
                var prefix = level_depth == 0 ? "Exception" : $"Inner Exception [{level_depth}]";
                exceptionDetails.Add($"{prefix}: {currentEx.GetType().FullName}");
                exceptionDetails.Add($"Message: {currentEx.Message}");

                if (currentEx is HttpRequestException httpEx) {
                    // Add specific HttpRequestException details
                    exceptionDetails.Add($"HttpRequestException.HResult: 0x{currentEx.HResult:X8} ({currentEx.HResult})");
                    if (httpEx.Data != null && httpEx.Data.Count > 0) {
                        exceptionDetails.Add("HttpRequestException.Data:");
                        foreach (var key in httpEx.Data.Keys) {
                            exceptionDetails.Add($"  {key}: {httpEx.Data[key]}");
                        }
                    }
                }

                if (currentEx is WebException webEx) {
                    // Add specific WebException details
                    exceptionDetails.Add($"WebException.Status: {webEx.Status}");
                    if (webEx.Response != null) {
                        exceptionDetails.Add($"WebException.Response: {webEx.Response}");
                    }
                }

                exceptionDetails.Add($"Source: {currentEx.Source ?? "N/A"}");
                exceptionDetails.Add($"HResult: 0x{currentEx.HResult:X8} ({currentEx.HResult})");

                if (!string.IsNullOrEmpty(currentEx.StackTrace)) {
                    exceptionDetails.Add($"StackTrace: {currentEx.StackTrace}");
                }

                currentEx = currentEx.InnerException;
                level_depth++;

                if (currentEx != null) {
                    exceptionDetails.Add(""); // Add separator line between exceptions
                }
            }

            Event(level, eventCode, subj, exceptionDetails);
        }

        public static void logRequest(ExtensionControl control) {
            if (!_enableTraceLogging)
                return;

            List<string> logMessage = new List<string> {
                        "NPS request start",
                        "-ExtensionPoint: " + control.ExtensionPoint.ToString(),
                        "-RequestType: " + control.RequestType.ToString(),
                        "-ResponseType: " + control.ResponseType.ToString()
            };

            Event(Level.Trace, 120, "RadiusExtensionProcess2 called with params:", logMessage);

            var userName = Radius.AttributeLookup(control.Request, RadiusAttributeType.UserName);
            if (!string.IsNullOrEmpty(userName)) {
                logMessage.Add("-UserName: " + userName);
                logMessage.Add("-NAS IPAddress: " + Radius.AttributeLookup(control.Request, RadiusAttributeType.NASIPAddress));
                logMessage.Add("-Src IPAddress: " + Radius.AttributeLookup(control.Request, RadiusAttributeType.SrcIPAddress));
                logMessage.Add($"-Connection Request Policy Name: '{Radius.AttributeLookup(control.Request, RadiusAttributeType.CRPPolicyName)}'");
                logMessage.Add($"-Network Policy Name: '{Radius.AttributeLookup(control.Request, RadiusAttributeType.PolicyName)}'");
            }
            Event(Level.Trace, 121, "Authorization request", logMessage);

            string s = "";
            foreach (var attr in Radius.AttributesToList(control.Request)) {
                s += " | " + Str.sanitize(attr);
            }
            Event(Level.Trace, 122, $"Request components: {s}");


            s = "";
            foreach (var attr in Radius.AttributesToList(control.Response[RadiusCode.AccessAccept])) {
                s += " ~ " + Str.sanitize(attr);
            }
            Event(Level.Trace, 123, $"Response components: {s}");
        }

        /// <summary>
        /// Gets module version information from Git-based versioning for logging.
        /// </summary>
        /// <returns>Formatted string with version, commit hash, and build status</returns>
        public static string GetModuleInfo() {
            try {
                var assembly = Assembly.GetExecutingAssembly();
                
                // Try to get the informational version which includes Git info
                var infoVersionAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (infoVersionAttr != null && !string.IsNullOrEmpty(infoVersionAttr.InformationalVersion)) {
                    // Format: "1.0.0+abc1234" or similar from setuptools_scm-like versioning
                    return $"v{infoVersionAttr.InformationalVersion}";
                }
                
                // Fallback to standard version attributes
                var version = assembly.GetName().Version;
                var fileVersionAttr = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
                
                if (fileVersionAttr != null) {
                    // Check if GitVersionInformation class is available (from generated VersionInfo.cs)
                    var gitVersionType = assembly.GetType("GitVersionInformation");
                    if (gitVersionType != null) {
                        var commitHashField = gitVersionType.GetField("CommitHash");
                        var isCleanField = gitVersionType.GetField("IsClean");
                        var distanceField = gitVersionType.GetField("CommitDistance");
                        
                        if (commitHashField != null && isCleanField != null && distanceField != null) {
                            string commitHash = commitHashField.GetValue(null) as string;
                            bool isClean = (bool)isCleanField.GetValue(null);
                            int distance = (int)distanceField.GetValue(null);
                            
                            string cleanStatus = isClean ? "clean" : "dirty";
                            string distanceInfo = distance > 0 ? $"+{distance}" : "";
                            
                            return $"v{fileVersionAttr.Version} ({commitHash}, {cleanStatus}{distanceInfo})";
                        }
                    }
                    
                    return $"v{fileVersionAttr.Version}";
                }
                
                if (version != null) {
                    return $"v{version}";
                }
                
                return "(version unavailable)";
            }
            catch {
                return "(version unavailable)";
            }
        }
    }
}
