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
using SDOIASLib;
using System.IO;
using System.Xml.XPath;
using System.Threading.Tasks;
using AsyncAuthHandler;

namespace NpsWrapperNET {
    /// <summary>
    /// Provides the entry points for the NPS service (indirectly called by the C++/CLR wrapper).
    /// </summary>
    public class NpsWrapper {
        const string APP_NAME = "NPS-Wrapper.NET";

        private static int initCount = 0;
        // Add a static field for the authenticator
        private static Authenticator _authenticator;

        /// <summary>
        /// <para>Called by NPS while the service is starting up</para>
        /// <remarks>Use RadiusExtensionInit to perform any initialization operations for the Extension DLL</remarks>
        /// </summary>
        /// <returns>A return value other then 0 will cause NPS to fail to start</returns>
        public static uint RadiusExtensionInit() {
            WriteEventLog(EventLogEntryType.Information, "RadiusExtensionInit called");
            if (initCount == 0) {
                initCount++;
                // Initialize the authenticator here
                _authenticator = new Authenticator();
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
                WriteEventLog(EventLogEntryType.Information, "RadiusExtensionInit called");
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
                    userName = AttributeLookup(control.Request, RadiusAttributeType.UserName);
                    

                    bool resMfa = _authenticator.AuthenticateAsync(userName).Result;
                    if (resMfa) {
                        /* Keep final disposition to AccessAccept - Note that could be changed by other extensions */
                        control.ResponseType = RadiusCode.AccessAccept;
                        WriteEventLog(EventLogEntryType.Information, $"MFA succeeded for user {userName}");
                    }
                    else {
                        /* Set final disposition to AccessReject - Note that could be changed by other extensions */
                        control.ResponseType = RadiusCode.AccessReject;
                        WriteEventLog(EventLogEntryType.Warning, $"MFA failed for user {userName}");
                    }
                }
            }
            return 0;
        }

        private static void logRequest(ExtensionControl control) {
            List<string> logMessage = new List<string>
{
                "NPS request start",
                "-ExtensionPoint: " + control.ExtensionPoint.ToString(),
                "-RequestType: " + control.RequestType.ToString(),
                "-ResponseType: " + control.ResponseType.ToString()
            };

            WriteEventLog(EventLogEntryType.Warning, "RadiusExtensionProcess2 called with params:", logMessage);

            var userName = AttributeLookup(control.Request, RadiusAttributeType.UserName);
            if (!string.IsNullOrEmpty(userName)) {
                logMessage.Add("-UserName: " + userName);
                logMessage.Add("-NAS IPAddress: " + AttributeLookup(control.Request, RadiusAttributeType.NASIPAddress));
                logMessage.Add("-Src IPAddress: " + AttributeLookup(control.Request, RadiusAttributeType.SrcIPAddress));
                logMessage.Add("-Connection Request Policy Name: " + AttributeLookup(control.Request, RadiusAttributeType.CRPPolicyName));
                logMessage.Add("-Network Policy Name: " + AttributeLookup(control.Request, RadiusAttributeType.PolicyName));
            }
            WriteEventLog(EventLogEntryType.Warning, "Authorization request", logMessage);

            string s = "";
            foreach (var attr in AttributesToList(control.Request)) {
                s += " | " + attr;
            }
            WriteEventLog(EventLogEntryType.Warning, $"Request components: {s}");

            s = "";
            foreach (var attr in AttributesToList(control.Response[RadiusCode.AccessAccept])) {
                s += " ~ " + attr;
            }
            WriteEventLog(EventLogEntryType.Warning, $"Response components: {s}");
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
        private static void WriteEventLog(EventLogEntryType level, string subj, List<string> subj_body = null) {
            if (subj_body == null) {
                subj_body = new List<string>();
            }
            using (EventLog eventLog = new EventLog("Application")) {
                eventLog.Source = APP_NAME;
                EventInstance eventInstance = new EventInstance(0, 0, level);
                var body = string.Join(Environment.NewLine, subj_body);
                EventLog.WriteEvent(eventLog.Source, eventInstance, new List<string>() { subj + Environment.NewLine + body }.ToArray());
            }
        }
    }
}
