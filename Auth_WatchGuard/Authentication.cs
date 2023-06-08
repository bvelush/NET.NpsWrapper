// --------------------------------------------------------------------------------------------------------------------
// <copyright>
//   Copyright (C) lestoilfante 2023 (https://github.com/lestoilfante)
//   
//   GNU General Public License version 2.1 (GPLv2.1) 
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Auth_WatchGuard
{
    public class WG
    {
        /// <summary>
        /// Basic settings from xml file
        /// </summary>
        /// <value>WatchGuard Cloud API base URL like "https://api.deu.cloud.watchguard.com/rest/".</value>
        readonly private static string CFG_URL = Properties.Settings.Default.WgUrl;
        /// <value>WatchGuard Cloud API Access ID (Read-write).</value>
        readonly private static string CFG_USER = Properties.Settings.Default.WgUser;
        /// <value>WatchGuard Cloud API readwrite password.</value>
        readonly private static string CFG_PSWD = Properties.Settings.Default.WgPassword;
        /// <value>WatchGuard Cloud API API Key.</value>
        readonly private static string CFG_KEY = Properties.Settings.Default.WgApiKey;
        /// <value>WatchGuard Cloud API Account Id similar to "ACC-XXXXXXX".</value>
        readonly private static string CFG_ACCID = Properties.Settings.Default.WgAccId;
        /// <value>WatchGuard Cloud API RESTful API Client Resource Id.</value>
        readonly private static string CFG_RESID = Properties.Settings.Default.WgRestId;
        /// <value>Whether to be verbose or not during EventLog generation.</value>
        readonly private static bool CFG_DEBUG = Properties.Settings.Default.debug;

        /// <value>EventLog APP source.</value>
        readonly private static string APP_NAME = "Watchguard-RestApi";

        /* Token is valid across each request for 3600 seconds */
        private static string _WgToken;
        private static long _WgTokenExpireAt;

        private HttpClient Client;

        public static async Task<bool> AuthenticateUser(string user, string password, bool useMFA, string mfaText = "") 
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (HttpClient client = new HttpClient())
            {
                WG p = new WG
                {
                    Client = client
                };
                return await p.Authenticate(user, password, useMFA, mfaText);
            }
        }

        private async Task<bool> TokenIsValid(string accountSid, string secretKey)
        {
            /* Check if a new token is needed */
            if (!string.IsNullOrEmpty(_WgToken) && DateTimeOffset.UtcNow.ToUnixTimeSeconds() < _WgTokenExpireAt)
                return true;

            if (CFG_DEBUG) WriteEventLog(APP_NAME, "Token", "retrieving new token", EventLogEntryType.Information);

            var wgId = Convert.ToBase64String(Encoding.UTF8.GetBytes(accountSid + ":" + secretKey));

            try 
            {
                var param = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("scope", "api-access")
                };
                var content = new FormUrlEncodedContent(param);
                var request = new HttpRequestMessage(HttpMethod.Post, $"{CFG_URL}/oauth/token");
                request.Headers.Add("Authorization", $"Basic {wgId}");
                request.Content = content;

                var response = await Client.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();

                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        var data = JsonSerializer.Deserialize<WgToken>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        _WgToken = data.access_token;
                        _WgTokenExpireAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + data.expires_in;
                        WriteEventLog(APP_NAME, "Token", "acquired, expires at UTC " + DateTimeOffset.FromUnixTimeSeconds(_WgTokenExpireAt), EventLogEntryType.Information);
                        return true;
                    default:
                        WriteEventLog(APP_NAME, "Token", "acquisition failed", EventLogEntryType.Error);
                        _WgToken = null;
                        return false;
                }
            }
            catch(Exception e) 
            {
                const string message = "Rest exception:";
                var wgException = message + " " + e.Message;
                _WgToken = null;
                WriteEventLog(APP_NAME, "Exception", wgException, EventLogEntryType.Error);
                return false;
            }
        }
        private HttpRequestMessage AuthRestRequest(HttpRequestMessage request)
        {
            request.Headers.Add("Authorization", $"Bearer {_WgToken}");
            request.Headers.Add("WatchGuard-API-Key", CFG_KEY);
            return request;
        }
        private async Task<bool> Authenticate(string userSid, string userPwd, bool useMFA, string mfaText = "")
        {
            if (! await TokenIsValid(CFG_USER, CFG_PSWD))
                return false;

            try
            {
                var entity = (useMFA) ? "transactions" : "password";
                var request = new HttpRequestMessage(HttpMethod.Post, $"{CFG_URL}authpoint/authentication/v1/accounts/{CFG_ACCID}/resources/{CFG_RESID}/{entity}");
                string json;
                /* Push validation */
                if (useMFA)
                {
                    var clientinfo = new WgClientInfoRequest { machineName = mfaText, osVersion = string.Empty, domain = string.Empty };
                    var param = new WgAuthMfa { login = userSid, password = userPwd, type = "PUSH", originIpAddress = string.Empty, clientInfoRequest = clientinfo };
                    json = JsonSerializer.Serialize(param);
                }
                /* Credential validation */
                else
                {
                    var param = new WgAuth { login = userSid, password = userPwd, originIpAddress = string.Empty };
                    json = JsonSerializer.Serialize(param);
                }
                var content = new StringContent(json);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                request.Content = content;


                var response = await Client.SendAsync(AuthRestRequest(request));

                var responseContent = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<WgTransaction>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        /* auth without MFA */
                        if (data.authenticationResult == "AUTHORIZED")
                        {
                            if (CFG_DEBUG) WriteEventLog(APP_NAME, "Authentication user success " + userSid, "", EventLogEntryType.Information);
                            return true;
                        }
                        /* auth with MFA */
                        if (data.transactionId != null)
                        {
                            if (CFG_DEBUG) WriteEventLog(APP_NAME, "Authentication waiting user " + userSid, "MFA transaction " + data.transactionId, EventLogEntryType.Information);
                            return await MFA(data.transactionId, userSid);
                        }
                        WriteEventLog(APP_NAME, "Authentication user failure " + userSid, data.title + "\n" + data.detail, EventLogEntryType.Warning);
                        return false;
                    case HttpStatusCode.Created:
                    case HttpStatusCode.Accepted:
                        //202 when auth in progress, so awaiting MFA
                        if (CFG_DEBUG) WriteEventLog(APP_NAME, "Authentication waiting user " + userSid, "MFA transaction " + data.transactionId, EventLogEntryType.Information);
                        return await MFA(data.transactionId, userSid);
                    case HttpStatusCode.Unauthorized:   //token expired or invalid
                        _WgToken = null;
                        if (CFG_DEBUG) WriteEventLog(APP_NAME, "Authentication user failure " + userSid, "MFA transaction " + data.transactionId + " failure: 401, check API credentials or token expiration", EventLogEntryType.Warning);
                        return false;
                    case HttpStatusCode.Forbidden:   //invalid, e.g. MFA required but not supplied 
                        WriteEventLog(APP_NAME, "Authentication user failure " + userSid, data.title + "\n" + data.detail, EventLogEntryType.Warning);
                        return false;
                    default:
                        WriteEventLog(APP_NAME, "Authentication user failure " + userSid, "code: " + response.StatusCode, EventLogEntryType.Warning);
                        return false;
                }
            }
            catch (Exception e)
            {
                const string message = "Rest exception:";
                var wgException = message + " " + e.Message;
                WriteEventLog(APP_NAME, "Exception", wgException, EventLogEntryType.Error);
                return false;
            }
        }
        private async Task<bool> MFA(string transactionId, string user) 
        {
            int timer = 5000;
            int checkN = 4; 
            if (CFG_DEBUG) WriteEventLog(APP_NAME, $"MFA user {user} in progress {transactionId}", "max wait is " + ((timer / 1000) * (checkN)), EventLogEntryType.Information);
            for (int i = 0; i < checkN; i++) {
                await Task.Delay(timer);
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, $"{CFG_URL}authpoint/authentication/v1/accounts/{CFG_ACCID}/resources/{CFG_RESID}/transactions/{transactionId}");
                    var response = await Client.SendAsync(AuthRestRequest(request));
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<WgTransactionResult>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    bool waitMFAapprove;
                    switch(response.StatusCode)
                    {
                        case HttpStatusCode.OK:
                            if (data.authenticationResult == "AUTHORIZED" || data.pushResult == "AUTHORIZED")
                            {
                                if (CFG_DEBUG) WriteEventLog(APP_NAME, $"MFA user {user} success {transactionId}", string.Empty, EventLogEntryType.Information);
                                return true;
                            }
                            if (CFG_DEBUG) WriteEventLog(APP_NAME, $"MFA user {user} failure {transactionId}", $"{data.title}\n{data.detail}", EventLogEntryType.Information);
                            return false;
                        case HttpStatusCode.Accepted:   // still pending
                            if (CFG_DEBUG) WriteEventLog(APP_NAME, $"MFA user {user} pending {transactionId}", string.Empty, EventLogEntryType.Information);
                            waitMFAapprove = true;
                            break;
                        case HttpStatusCode.Forbidden:   //with push usually when denied by end user
                            WriteEventLog(APP_NAME, $"MFA user {user} failure {transactionId}", $"status: {response.StatusCode}\n{data.title}", EventLogEntryType.Warning);
                            return false;
                        default:
                            if (CFG_DEBUG) WriteEventLog(APP_NAME, $"MFA user {user} failure {transactionId}", "code: " + response.StatusCode, EventLogEntryType.Information);
                            return false;
                    }
                    if (!waitMFAapprove)
                    {
                        return false;
                    }
                }
                catch (Exception e)
                {
                    const string message = "Rest exception:";
                    var wgException = message + " " + e.Message;
                    WriteEventLog(APP_NAME, "Exception", wgException, EventLogEntryType.Error);
                    return false;
                }
            }
            if (CFG_DEBUG) WriteEventLog(APP_NAME, $"MFA user {user} timeout {transactionId}", string.Empty, EventLogEntryType.Information);
            return false;
        }
        private static void WriteEventLog(string src, string subj, string subj_body, EventLogEntryType level)
        {
            string log = "Application";
            using (EventLog eventLog = new EventLog(log))
            {
                try
                {
                    if (!EventLog.SourceExists(src))
                    {
                        EventLog.CreateEventSource(src, log);
                    }
                    eventLog.Source = src;
                    eventLog.WriteEntry(subj + "\n" + subj_body, level);
                }
                catch (Exception e)
                {
                    /* Could happen if program doesn't have rights to read/write registry
                     *  1   from PS elevated prompt run [System.Diagnostics.EventLog]::CreateEventSource("APP_NAME", "Application")
                     *  2   if needed regedit to "Computer\HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\EventLog\Application\{source} and set permission
                     */
                }
            }
        }
    }
}
