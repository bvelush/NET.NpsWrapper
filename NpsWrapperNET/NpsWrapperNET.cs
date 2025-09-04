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
using System.Data.Common;
//using Auth_WatchGuard;
using TgBot;

namespace NpsWrapperNET {
    /// <summary>
    /// Provides the entry points for the NPS service (indirectly called by the C++/CLR wrapper).
    /// </summary>
    public class NpsWrapper {
        const string APP_NAME = "NPS-Wrapper.NET";

        private static int initCount = 0;
        /// <summary>
        /// <para>Called by NPS while the service is starting up</para>
        /// <remarks>Use RadiusExtensionInit to perform any initialization operations for the Extension DLL</remarks>
        /// </summary>
        /// <returns>A return value other then 0 will cause NPS to fail to start</returns>
        public static uint RadiusExtensionInit() {
            WriteEventLog(EventLogEntryType.Information, "RadiusExtensionInit called");
            if (initCount == 0) {
                initCount++;
                if (GetRADIUSClientsXMLconf().Count > 0)
                    WriteEventLog(EventLogEntryType.Information, "Initialized");
                else
                    WriteEventLog(EventLogEntryType.Warning, "Initialized but 'GetRADIUSClientsXMLconf' count is 0");
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
            List<string> logMessage = new List<string>
            {
                "NPS request start",
                "-ExtensionPoint: " + control.ExtensionPoint.ToString(),
                "-RequestType: " + control.RequestType.ToString(),
                "-ResponseType: " + control.ResponseType.ToString()
            };
            WriteEventLog(EventLogEntryType.Warning, "RadiusExtensionProcess2 called with params:", logMessage);
            /* 
             * Authentication request 
             *      -ExtensionPoint: Authentication
             *      -RequestType: AccessRequest
             *      -ResponseType: Unknown
             */
            if ((control.ExtensionPoint == RadiusExtensionPoint.Authentication) && (control.RequestType == RadiusCode.AccessRequest)) {
                if (control.ResponseType == RadiusCode.Unknown) {
                    try {
                        /* Check if this request will be proxied by NPS */
                        if (AttributeLookup(control.Request, RadiusAttributeType.Provider) == "2") {
                            /* Get NAS Client actual src IP and set NASIdentifier attrib with name defined on local config */
                            var clientIpVal = AttributeLookup(control.Request, RadiusAttributeType.SrcIPAddress);
                            if (!string.IsNullOrEmpty(clientIpVal) && GetRADIUSClientsXMLconf().TryGetValue(clientIpVal, out string clientLocalName)) {
                                var nasIdent = control.Request.FirstOrDefault(x => x.AttributeId.Equals((int)RadiusAttributeType.NASIdentifier));
                                if (nasIdent != null) {
                                    ((RadiusAttributeList)control.Request).Remove(nasIdent);
                                }
                                ((RadiusAttributeList)control.Request).Add(new RadiusAttribute(RadiusAttributeType.NASIdentifier, clientLocalName));
                            }
                        }
                    }
                    catch (Exception ex) {
                        logMessage.Add($"Exception: {ex}");
                        WriteEventLog(EventLogEntryType.Error, "Authentication error:", logMessage);
                    }
                }
                else {
                    WriteEventLog(EventLogEntryType.Error, $"Control.ResponseType is '{control.RequestType}'");
                }
            }
            /* 
             * Authorization request 
             *      -ExtensionPoint: Authorization
             *      -RequestType: AccessRequest
             *      -ResponseType: AccessAccept
             */
            else if ((control.ExtensionPoint == RadiusExtensionPoint.Authorization) && (control.RequestType == RadiusCode.AccessRequest)) {
                if (control.ResponseType == RadiusCode.AccessAccept) {
                    /*
                     * Call MFA on already authorized request
                     * If MFA returns OK keep original disposition -> AccessAccept disposition
                     * If MFA returns KO override original disposition -> AccessReject 
                     */
                    userName = AttributeLookup(control.Request, RadiusAttributeType.UserName);
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

                    bool resMfa = TgAgent.AuthenticateUser(userName).Result;
                    if (!resMfa) {
                        logMessage.Add("-MFA Process fail, setting AccessReject to NPS");
                        logMessage.Add("+NPS request end");
                        /* Set final disposition to AccessReject - Note that could be changed by other extensions */
                        control.ResponseType = RadiusCode.AccessReject;
                    }
                    else {
                        logMessage.Add("-MFA Process success");
                        logMessage.Add("+NPS request end");
                    }
                }
            }
            return 0;
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

        /// <summary>
        /// Get clients defined in NPS by Server Data Object interface.
        /// Unfortunately call to GetServiceSDO(IASDATASTORE.DATA_STORE_LOCAL, "IAS") returns ACCESS DENIED so it's useless without finding further documentation
        /// </summary>
        private static Dictionary<string, string> GetRADIUSClientsSDO() {
            /* Attach to the local computer */
            var localServer = new SdoMachine();
            localServer.Attach(null);
            /* Retrieve a service SDO */
            var sdoServiceSDO = localServer.GetServiceSDO(IASDATASTORE.DATA_STORE_LOCAL, "IAS");
            /* Retrieve the protocols collection */
            var sdoCollProtocols = sdoServiceSDO.GetProperty(IASPROPERTIES.PROPERTY_IAS_PROTOCOLS_COLLECTION);
            /* Retrieve the RADIUS protocol */
            var sdoRadius = sdoCollProtocols.Item("Microsoft Radius Protocol");
            /* Retrieve the clients collection */
            var ClientsCollection = sdoRadius.GetProperty(RADIUSPROPERTIES.PROPERTY_RADIUS_CLIENTS_COLLECTION);
            var result = new Dictionary<string, string>();
            foreach (var client in ClientsCollection) {
                var name = client.GetProperty(IASCOMMONPROPERTIES.PROPERTY_SDO_NAME);
                var enabled = client.GetProperty(CLIENTPROPERTIES.PROPERTY_CLIENT_ENABLED);
                var ip = client.GetProperty(CLIENTPROPERTIES.PROPERTY_CLIENT_ADDRESS);
                result.Add(ip, name);
            }
            return result;
        }
        /// <summary>
        /// Get clients defined in NPS by reading xml config file.
        /// </summary>
        private static Dictionary<string, string> GetRADIUSClientsXMLconf() {
            return RADIUSClientsXMLconf.Clients();
        }

        /// <summary>
        /// Helper class for XML config parsing.
        /// Returns clients defined in XML or a local cache based on "cacheTime" defined value.
        /// </summary>
        internal static class RADIUSClientsXMLconf {
            private const int cacheTime = 1;
            private const string xmlRadClientsNode = "/Root/Children/Microsoft_Internet_Authentication_Service/Children/Protocols/Children/Microsoft_Radius_Protocol/Children/Clients/Children/*";
            private readonly static string configFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "ias", "ias.xml");
            private static DateTime configModTime;
            private static DateTime lastCheckTime;
            private static Dictionary<string, string> clients = new Dictionary<string, string>();

            public static Dictionary<string, string> Clients() {
                var now = DateTime.UtcNow;
                if (now > lastCheckTime.AddMinutes(cacheTime)) {
                    lastCheckTime = now;
                    var lastmod = File.GetLastWriteTimeUtc(configFile);
                    if (lastmod > configModTime) {
                        configModTime = lastmod;
                        clients = ParseXmlClients();
                    }
                }
                return clients;
            }

            private static Dictionary<string, string> ParseXmlClients() {
                var c = new Dictionary<string, string>();
                using (var fileStream = new FileStream(configFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    XPathDocument document = new XPathDocument(fileStream);
                    XPathNavigator navigator = document.CreateNavigator();
                    XPathNodeIterator nodeIterator = navigator.Select(xmlRadClientsNode);

                    while (nodeIterator.MoveNext()) {
                        XPathNavigator node = nodeIterator.Current;
                        string name = node.GetAttribute("name", string.Empty);
                        string ip = node.SelectSingleNode("Properties/IP_Address")?.Value;
                        string enabled = node.SelectSingleNode("Properties/Radius_Client_Enabled")?.Value;
                        if (enabled == "1") {
                            c.Add(ip, name);
                        }
                    }
                }
                return c;
            }
        }
    }
}
