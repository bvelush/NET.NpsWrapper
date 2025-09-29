using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace AsyncAuthHandler {
    public enum LogLevel {
        Trace, 
        Information,
        Warning,
        Error
    }

    public class Authenticator {

        const string APP_NAME = "NPS-AsyncAuthHandler";
        private readonly HttpClient _httpClient;

        private int _authTimeout = 60; // seconds
        private string _serviceUrl = "http://localhost:8000";
        private int _waitBeforePoll = 10; // seconds
        private int _pollInterval = 1; // seconds
        private int _pollMaxSeconds = 60; // seconds
        private bool _enableTraceLogging = false;

        private const string _regPath = @"SOFTWARE\NpsWrapperNET";
        private const string _authTimeoutKey = "AuthTimeout";
        private const string _serviceUrlKey = "ServiceUrl";
        private const string _waitBeforePollKey = "WaitBeforePoll";
        private const string _pollIntervalKey = "PollInterval";
        private const string _pollMaxSecondsKey = "PollMaxSeconds";
        private const string _enableTraceLoggingKey = "EnableTraceLogging";

        public Authenticator() {
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
                    }
                }
            } catch (Exception ex) {
                WriteEventLog(LogLevel.Warning, $"Error reading settings from registry: {ex.Message}");
            }

            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(_authTimeout);
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
                        WriteEventLog(LogLevel.Error, $"Timeout reached while polling AuthResult for user {samid}: {ex.Message}");
                        return false;
                    }
                    catch (HttpRequestException ex) {
                        WriteEventLog(LogLevel.Error, $"MFA Service is unreachable while polling AuthResult for user {samid}: {ex.Message}");
                        return false;
                    }
                    catch (Exception ex) {
                        WriteEventLog(LogLevel.Error, $"Error polling AuthResult for user {samid}: {ex.Message}, {ex.StackTrace}");
                        return false;
                    }
                    await Task.Delay(1000); // Wait 1 second before next poll
                }
                WriteEventLog(LogLevel.Error, $"Authentication result not received in time for user: {samid}");
                return false;
            }
            catch (TaskCanceledException ex) {
                WriteEventLog(LogLevel.Error, $"Timeout reached while authenticating user {samid}: {ex.Message}");
                return false;
            }
            catch (HttpRequestException ex) {
                WriteEventLog(LogLevel.Error, $"MFA Service is unreachable while authenticating user {samid}: {ex.Message}");
                return false;
            }
            catch (Exception ex) {
                WriteEventLog(LogLevel.Error, $"Error authenticating user {samid}: {ex.Message}, {ex.StackTrace}");
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
    }
}
