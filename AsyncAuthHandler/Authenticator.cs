using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AsyncAuthHandler
{
    public class Authenticator {

        const string APP_NAME = "NPS-AsyncAuthHandler";

        public async Task<bool> AuthenticateAsync(string samid)
        {
            WriteEventLog(EventLogEntryType.Information, $"Starting async authentication for user: {samid}");
            // Simulate an asynchronous authentication process
            await Task.Delay(1000); // Simulating network delay
            WriteEventLog(EventLogEntryType.Information, $"Completed async authentication for user: {samid}");
            // For demonstration purposes, let's assume the credentials are valid if both are "admin"
            return false;
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
