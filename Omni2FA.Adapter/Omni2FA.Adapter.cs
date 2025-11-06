// --------------------------------------------------------------------------------------------------------------------
// <copyright>
//   Copyright lestoilfante 2023 (https://github.com/lestoilfante)
//   
//   GNU General Public License version 2.1 (GPLv2.1) 
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
using System;
using OpenCymd.Nps.Plugin;
using System.Collections.Generic;
using System.Linq;
using Omni2FA.AuthClient;
using Omni2FA.Net.Utils;

namespace Omni2FA.Adapter {

    /// <summary>
    /// Provides the entry points for the NPS service (indirectly called by the C++/CLR wrapper).
    /// </summary>
    public class NpsAdapter {

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
        private const string _regPath = @"SOFTWARE\Omni2FA.NPS";
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
            var moduleInfo = Log.GetModuleInfo();
            Log.Event(Log.Level.Information, 102, $"Initializing Omni2FA.Adapter {moduleInfo}");
            Log.Event(Log.Level.Trace, 10, "RadiusExtensionInit called");

            if (initCount == 0) {
                initCount++;
                _authenticator = new Authenticator();

                // Initialize the Groups helper
                Groups.Initialize();
                Log.Event(Log.Level.Trace, 12, $"Hostname detected: {Groups.Hostname}");

                using (var registry = new Registry(_regPath)) {
                    // Read trace logging setting
                    _enableTraceLogging = registry.GetBoolRegistryValue(_enableTraceLoggingKey, false);
                    
                    Log.SetTraceLoggingEnabled(_enableTraceLogging);

                    // Read MFA-enabled NPS policy name
                    _mfaEnabledNpsPolicy = registry.GetStringRegistryValue(_mfaEnabledNpsPolicyKey, string.Empty);
                    if (!string.IsNullOrEmpty(_mfaEnabledNpsPolicy)) {
                        _mfaEnabledNpsPolicy = _mfaEnabledNpsPolicy.Trim();
                        Log.Event(Log.Level.Information, 201, $"MFA-enabled NPS policy set to: {_mfaEnabledNpsPolicy}");
                    }
                    else {
                        Log.Event(Log.Level.Information, 202, "MfaEnabledNPSPolicy registry value is empty or missing.");
                    }

                    // Read NoMFA group names and resolve to SIDs
                    var groupNames = registry.GetStringRegistryValue(_noMfaKey, string.Empty);
                    if (!string.IsNullOrEmpty(groupNames)) {
                        foreach (var groupName in groupNames.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)) {
                            string trimmedName = groupName.Trim();
                            var result = Groups.ResolveGroup(trimmedName);

                            if (result != null && result.Success) {
                                _noMfaGroupSids.Add(result.Sid);
                                Log.Event(Log.Level.Information, 140, $"NoMFA group added ({result.ContextName}): {trimmedName} (SID: {result.Sid})");
                            }
                            else if (result != null && !string.IsNullOrEmpty(result.Error)) {
                                Log.Event(Log.Level.Warning, 302, $"Error resolving group '{trimmedName}' in {result.ContextName}: {result.Error}");
                            }
                            else {
                                Log.Event(Log.Level.Warning, 303, $"NoMFA group not found: {trimmedName}");
                            }
                        }
                    }
                    else {
                        Log.Event(Log.Level.Warning, 304, "NoMfaGroups registry value is empty or missing.");
                    }
                } // Registry is properly disposed here
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
                Log.Event(Log.Level.Trace, 11, "RadiusExtensionTerm called");
                
                // Dispose authenticator to free resources
                if (_authenticator != null) {
                    (_authenticator as IDisposable)?.Dispose();
                    _authenticator = null;
                }
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
            Log.logRequest(control);
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
                    Log.Event(Log.Level.Trace, 124, "Processing authorized AccessRequest for MFA");
                    bool performMfa = true;
                    var policyName = Radius.AttributeLookup(control.Request, RadiusAttributeType.PolicyName);

                    // Check if we should perform MFA based on policy configuration
                    if (!string.IsNullOrEmpty(_mfaEnabledNpsPolicy)) {
                        // MFA policy is configured - only perform MFA if current policy matches
                        if (string.IsNullOrEmpty(policyName) ||
                            !string.Equals(policyName, _mfaEnabledNpsPolicy, StringComparison.OrdinalIgnoreCase)) {
                            performMfa = false;
                            Log.Event(Log.Level.Information, 203, $"Policy '{policyName}' does NOT match MFA-enabled policy '{_mfaEnabledNpsPolicy}', skipping MFA.");
                        }
                        else {
                            Log.Event(Log.Level.Trace, 125, $"Policy '{policyName}' matches MFA-enabled policy '{_mfaEnabledNpsPolicy}', MFA will be performed.");
                        }
                    }
                    else {
                        // No MFA policy configured - always perform MFA (secure default)
                        Log.Event(Log.Level.Trace, 126, $"No MFA-enabled policy configured, MFA will be performed for all requests (secure default).");
                    }

                    if (performMfa) {
                        try {
                            userName = Radius.AttributeLookup(control.Request, RadiusAttributeType.UserName).Trim();

                            // Resolve user groups using the helper
                            var userResult = Groups.ResolveUserGroups(userName);

                            if (userResult != null && userResult.Success) {
                                // Check if any of the user's groups are in the NoMFA list
                                var matchingSids = userResult.GroupSids.Intersect(_noMfaGroupSids).ToList();
                                if (matchingSids.Any()) {
                                    performMfa = false;
                                    Log.Event(Log.Level.Information, 141, $"User {userName} is in NoMFA group (matched {matchingSids.Count} SID(s)), skipping MFA.");
                                }
                            }
                            else if (userResult != null && !string.IsNullOrEmpty(userResult.Error)) {
                                Log.Event(Log.Level.Warning, 305, $"Error checking NoMFA group membership for user '{userName}': {userResult.Error}");
                            }
                        }
                        catch (Exception ex) {
                            Log.Event(Log.Level.Warning, 305, $"Error checking NoMFA group membership for user '{userName}': {ex.Message}");
                        }
                    }

                    if (performMfa) {
                        // calling AuthenticateAsync synchronously
                        bool resMfa = _authenticator.AuthenticateAsync(userName).Result;
                        if (resMfa) {
                            /* Keep final disposition to AccessAccept - Note that could be changed by other extensions */
                            control.ResponseType = RadiusCode.AccessAccept;
                            Log.Event(Log.Level.Information, 130, $"MFA succeeded for user {userName}");
                        }
                        else {
                            /* Set final disposition to AccessReject - Note that could be changed by other extensions */
                            control.ResponseType = RadiusCode.AccessReject;
                            Log.Event(Log.Level.Warning, 131, $"MFA failed for user {userName}");
                        }
                    }
                    else {
                        control.ResponseType = RadiusCode.AccessAccept;
                        Log.Event(Log.Level.Information, 132, $"MFA skipped for user {userName}, accepting request.");
                    }
                }
            }
            return 0;
        }
    }
}

