using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Net;

namespace Omni2FA.AuthClient {
    public enum LogLevel {
        Trace, 
        Information,
        Warning,
        Error
    }

    public class Authenticator {

        const string APP_NAME = "Omni2FA.AuthClient";
        private readonly HttpClient _httpClient;

        private int _authTimeout = 60; // seconds
        private string _serviceUrl = "http://localhost:8000";
        private int _waitBeforePoll = 10; // seconds
        private int _pollInterval = 1; // seconds
        private int _pollMaxSeconds = 60; // seconds
        private bool _enableTraceLogging = false;
        private bool _ignoreSslErrors = false;
        private string _basicAuthUsername = "";
        private string _basicAuthPassword = "";

        private const string _regPath = @"SOFTWARE\Omni2FA.NPS";
        private const string _authTimeoutKey = "AuthTimeout";
        private const string _serviceUrlKey = "ServiceUrl";
        private const string _waitBeforePollKey = "WaitBeforePoll";
        private const string _pollIntervalKey = "PollInterval";
        private const string _pollMaxSecondsKey = "PollMaxSeconds";
        private const string _enableTraceLoggingKey = "EnableTraceLogging";
        private const string _ignoreSslErrorsKey = "IgnoreSslErrors";
        private const string _basicAuthUsernameKey = "BasicAuthUsername";
        private const string _basicAuthPasswordKey = "BasicAuthPassword";

        public Authenticator() {
            // Log component initialization with datetime and size
            var moduleInfo = GetModuleInfo();
            WriteEventLog(LogLevel.Information, $"Initializing Omni2FA.Auth {moduleInfo}");
            
            // Read settings from registry
            try {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(_regPath)) {
                    if (key != null) {
                        _authTimeout = GetIntRegistryValue(key, _authTimeoutKey, 60);
                        _serviceUrl = GetStringRegistryValue(key, _serviceUrlKey, "http://localhost:8000");
                        _waitBeforePoll = GetIntRegistryValue(key, _waitBeforePollKey, 10);
                        _pollInterval = GetIntRegistryValue(key, _pollIntervalKey, 1);
                        _pollMaxSeconds = GetIntRegistryValue(key, _pollMaxSecondsKey, 60);
                        _enableTraceLogging = GetBoolRegistryValue(key, _enableTraceLoggingKey, false);
                        _ignoreSslErrors = GetBoolRegistryValue(key, _ignoreSslErrorsKey, false);
                        _basicAuthUsername = GetStringRegistryValue(key, _basicAuthUsernameKey, "");
                        _basicAuthPassword = GetStringRegistryValue(key, _basicAuthPasswordKey, "");
                    }
                }
            } catch (Exception ex) {
                WriteEventLog(LogLevel.Warning, $"Error reading settings from registry: {ex.Message}");
            }

            // Create HttpClientHandler with SSL and auth configuration
            var handler = new HttpClientHandler();
            
            // Configure SSL certificate validation
            if (_ignoreSslErrors) {
                handler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) => {
                    WriteEventLog(LogLevel.Warning, "SSL certificate validation bypassed due to IgnoreSslErrors setting");
                    return true; // Accept all certificates
                };
                WriteEventLog(LogLevel.Information, "SSL certificate validation disabled");
            }

            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(_authTimeout);

            // Configure basic authentication if credentials are provided
            if (!string.IsNullOrEmpty(_basicAuthUsername) && !string.IsNullOrEmpty(_basicAuthPassword)) {
                var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_basicAuthUsername}:{_basicAuthPassword}"));
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
                WriteEventLog(LogLevel.Information, $"Basic authentication configured for user: {_basicAuthUsername}");
            }

            WriteEventLog(LogLevel.Information, $"Omni2FA.Auth initialized with service URL: {_serviceUrl}");
        }

        /// <summary>
        /// Gets module information (datetime and size) for logging
        /// </summary>
        /// <returns>Formatted string with datetime and size</returns>
        private string GetModuleInfo() {
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

        private int GetIntRegistryValue(RegistryKey key, string valueName, int defaultValue)
        {
            var val = key.GetValue(valueName);
            if (val != null && int.TryParse(val.ToString(), out int result))
                return result;
            return defaultValue;
        }

        private bool GetBoolRegistryValue(RegistryKey key, string valueName, bool defaultValue)
        {
            var val = key.GetValue(valueName);
            if (val != null && int.TryParse(val.ToString(), out int result))
                return result == 1;
            return defaultValue;
        }

        private string GetStringRegistryValue(RegistryKey key, string valueName, string defaultValue)
        {
            var val = key.GetValue(valueName);
            return val != null ? val.ToString() : defaultValue;
        }

        private enum AuthStatusEnum {
            FAILED = -2,
            REJECTED = -1,
            PENDING = 0,
            AUTH_SUCCESS = 1,
            PREAUTH_SUCCESS = 2
        }

        private class AuthResultResponse {
            public AuthStatusEnum status { get; set; }
        }

        public async Task<bool> AuthenticateAsync(string samid) {
            try {
                // TODO: lets generate requestid here, send auth request, then poll for result
                //var requestId = Guid.NewGuid().ToString();
                var authRequestJson = JsonConvert.SerializeObject(new { samid = samid, requestor = "SMK-RDG" });
                WriteEventLog(LogLevel.Trace, $"Sending authentication request for user: {samid} to {_serviceUrl}/Authenticate");
                var authenticateResponse = await _httpClient.PostAsync(
                    $"{_serviceUrl}/Authenticate", 
                    new StringContent(authRequestJson, Encoding.UTF8, "application/json")
                );
                if (!authenticateResponse.IsSuccessStatusCode) {
                    var responseContent = await authenticateResponse.Content.ReadAsStringAsync();
                    WriteEventLog(LogLevel.Error, $"Service responded with status: {authenticateResponse.StatusCode}, content: {responseContent}");
                    return false;
                }
                var authenticateResponseJson = await authenticateResponse.Content.ReadAsStringAsync();
                WriteEventLog(LogLevel.Trace, $"Received authentication response for user: {samid}, response: {authenticateResponseJson}");
                var authenticateResponseObj = JsonConvert.DeserializeObject<AuthResultResponse>(authenticateResponseJson);
                WriteEventLog(LogLevel.Trace, $"Deserialized authentication response for user: {samid}, status: {authenticateResponseObj?.status}");
                if (authenticateResponseObj == null) {
                    WriteEventLog(LogLevel.Error, $"Invalid response from service for user: {samid}");
                    return false;
                }
                if (authenticateResponseObj.status < 0) { // early answer, no need of polling
                    WriteEventLog(LogLevel.Trace, $"Authentication failed for user: {samid} without polling, status: {authenticateResponseObj.status}");
                    return false;
                }
                if (authenticateResponseObj.status > 0) { // early success, no need of polling
                    WriteEventLog(LogLevel.Trace, $"Authentication succeeded for user: {samid} without polling, status: {authenticateResponseObj.status}");
                    return true;
                }

                await Task.Delay(_waitBeforePoll * 1000);
                for (int i = 0; i < _pollMaxSeconds; i++) {
                    try {
                        WriteEventLog(LogLevel.Trace, $"Polling AuthResult for user: {samid}, attempt: {i + 1}");
                        var authResultResponse = await _httpClient.PostAsync(
                            $"{_serviceUrl}/AuthResult",
                            new StringContent(authRequestJson, Encoding.UTF8, "application/json")
                        );
                        var authResultResponseContent = await authResultResponse.Content.ReadAsStringAsync();
                        if (!authResultResponse.IsSuccessStatusCode) {
                            WriteEventLog(LogLevel.Warning, $"AuthResult responded with status: {authResultResponse.StatusCode}, content: {authResultResponseContent}");
                            return false;
                        }
                        var authResultResponseJson = JsonConvert.DeserializeObject<AuthResultResponse>(authResultResponseContent);
                        WriteEventLog(LogLevel.Trace, $"Polled AuthResult for user: {samid}, response: {authResultResponseContent}");
                        if (authResultResponseJson == null) {
                            WriteEventLog(LogLevel.Error, $"Invalid AuthResult response for user {samid}");
                            return false;
                        }
                        if (authResultResponseJson.status > 0) { // auth success
                            WriteEventLog(LogLevel.Trace, $"Authentication succeeded for user: {samid}");
                            return true;
                        }
                        if (authResultResponseJson.status < 0) { // auth failure
                            WriteEventLog(LogLevel.Trace, $"Authentication failed for user: {samid}");
                            return false;
                        }
                        // result == 1 (pending), continue polling
                        await Task.Delay(_pollInterval * 1000);
                    }
                    catch (TaskCanceledException ex) {
                        WriteEventLog(LogLevel.Error, $"Timeout reached while polling AuthResult for user {samid}", ex);
                        return false;
                    }
                    catch (HttpRequestException ex) {
                        WriteEventLog(LogLevel.Error, $"MFA Service is unreachable while polling AuthResult for user {samid}", ex);
                        return false;
                    }
                    catch (Exception ex) {
                        WriteEventLog(LogLevel.Error, $"Error polling AuthResult for user {samid}", ex);
                        return false;
                    }
                    await Task.Delay(1000); // Wait 1 second before next poll
                }
                WriteEventLog(LogLevel.Error, $"Authentication result not received in time for user: {samid}");
                return false;
            }
            catch (TaskCanceledException ex) {
                WriteEventLog(LogLevel.Error, $"Timeout reached while authenticating user {samid}", ex);
                return false;
            }
            catch (HttpRequestException ex) {
                WriteEventLog(LogLevel.Error, $"MFA Service is unreachable while authenticating user {samid}", ex);
                return false;
            }
            catch (Exception ex) {
                WriteEventLog(LogLevel.Error, $"Error authenticating user {samid}", ex);
                return false;
            }
        }

        private void WriteEventLog(LogLevel level, string subj, List<string> subj_body = null) {
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

        private void WriteEventLog(LogLevel level, string subj, Exception ex) {
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
            
            WriteEventLog(level, subj, exceptionDetails);
        }
    }
}
