using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AsyncAuthHandler
{
    public class Authenticator {

        const string APP_NAME = "NPS-AsyncAuthHandler";
        const int authTimeout = 60; // seconds
        private readonly string _serviceUrl;
        private readonly HttpClient _httpClient;

        public Authenticator()
        {
            _serviceUrl = "http://localhost:8000/Authenticate";
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(authTimeout);
        }

        public async Task<bool> AuthenticateAsync(string samid)
        {
            WriteEventLog(EventLogEntryType.Information, $"Starting async authentication for user: {samid}");
            var requestObj = new { samid = samid };
            var json = JsonConvert.SerializeObject(requestObj);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try
            {
                var response = await _httpClient.PostAsync(_serviceUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    WriteEventLog(EventLogEntryType.Warning, $"Service responded with status: {response.StatusCode}, content: {responseContent}");
                }
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();
                var resultObj = JsonConvert.DeserializeObject<ResultResponse>(responseJson);
                WriteEventLog(EventLogEntryType.Information, $"Completed async authentication for user: {samid}, result: {resultObj?.result}");
                return resultObj?.result ?? false;
            }
            catch (TaskCanceledException ex)
            {
                WriteEventLog(EventLogEntryType.Error, $"Timeout reached while authenticating user {samid}: {ex.Message}");
                return false;
            }
            catch (HttpRequestException ex)
            {
                WriteEventLog(EventLogEntryType.Error, $"Service unreachable while authenticating user {samid}: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                WriteEventLog(EventLogEntryType.Error, $"Error authenticating user {samid}: {ex.Message}");
                return false;
            }
        }

        private class ResultResponse
        {
            public bool result { get; set; }
        }

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
