using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
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
        const int authTimeout = 60; // seconds
        private readonly string _serviceUrl = "http://localhost:8000";
        private readonly int _waitBeforePoll = 10; // seconds
        private readonly int _pollInterval = 1; // seconds
        private readonly int _pollMaxSeconds = 60; // seconds
        private readonly HttpClient _httpClient;
        private readonly bool _enableTraceLogging = true;

        public Authenticator() {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(authTimeout);
        }

        private enum AuthStatusEnum {
            PENDING = 1,
            SUCCESS = 2,
            FAILURE = 3
        }

        private class AuthResultResponse {
            public AuthStatusEnum status { get; set; }
        }

        public async Task<bool> AuthenticateAsync(string samid) {
            try {
                // TODO: lets generate requestid here, send auth request, then poll for result
                var requestId = Guid.NewGuid().ToString();
                var authRequestJson = JsonConvert.SerializeObject(new { request_id = requestId, samid = samid, requestor = "SMK-RDG" });
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
                if (authenticateResponseObj.status == AuthStatusEnum.FAILURE) { // early answer, no need of polling
                    WriteEventLog(LogLevel.Trace, $"Authentication failed for user: {samid} without polling");
                    return false;
                }

                if (string.IsNullOrEmpty(requestId)) {
                    WriteEventLog(LogLevel.Error, $"Incorrect response: No request_id received for user: {samid}");
                    return false;
                }
                WriteEventLog(LogLevel.Trace, $"Received request_id: {requestId} for user: {samid}");
                await Task.Delay(_waitBeforePoll * 1000);
                for (int i = 0; i < _pollMaxSeconds; i++) {
                    try {
                        WriteEventLog(LogLevel.Trace, $"Polling AuthResult for user: {samid}, request_id: {requestId}, attempt: {i + 1}");
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
                        WriteEventLog(LogLevel.Trace, $"Polled AuthResult for user: {samid}, request_id: {requestId}, response: {authResultResponseContent}");
                        if (authResultResponseJson == null) {
                            WriteEventLog(LogLevel.Error, $"Invalid AuthResult response for request_id: {requestId}");
                            return false;
                        }
                        if (authResultResponseJson.status == AuthStatusEnum.SUCCESS) { // auth success
                            WriteEventLog(LogLevel.Trace, $"Authentication succeeded for user: {samid}, request_id: {requestId}");
                            return true;
                        }
                        if (authResultResponseJson.status == AuthStatusEnum.FAILURE) { // auth failure
                            WriteEventLog(LogLevel.Trace, $"Authentication failed for user: {samid}, request_id: {requestId}");
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
                        WriteEventLog(LogLevel.Error, $"Service unreachable while polling AuthResult for user {samid}: {ex.Message}");
                        return false;
                    }
                    catch (Exception ex) {
                        WriteEventLog(LogLevel.Error, $"Error polling AuthResult for user {samid}: {ex.Message}, {ex.StackTrace}");
                        return false;
                    }
                    await Task.Delay(1000); // Wait 1 second before next poll
                }
                WriteEventLog(LogLevel.Error, $"Authentication result not received in time for user: {samid}, request_id: {requestId}");
                return false;
            }
            catch (TaskCanceledException ex) {
                WriteEventLog(LogLevel.Error, $"Timeout reached while authenticating user {samid}: {ex.Message}");
                return false;
            }
            catch (HttpRequestException ex) {
                WriteEventLog(LogLevel.Error, $"Service unreachable while authenticating user {samid}: {ex.Message}");
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
