using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TgBot
{
    public class TgAgent
    {
        private const string APP_NAME = "NPS-TgMfaAgent";

        // DLL initialization
        //[DllImport("kernel32.dll")]
        //private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);
        //private delegate bool ConsoleCtrlDelegate(int CtrlType);

        static TgAgent()
        {
            //LoadEnv();
            //_botName = Environment.GetEnvironmentVariable("TELEGRAM_BOT_NAME");
        }

        public static async Task<bool> AuthenticateUser(string userName)
        {
            WriteEventLog(EventLogEntryType.Information, $"Starting authentication for user {userName}");
            var instance = new TgAgent();
            return await instance.Authenticate(userName);
        }

        private static void WriteEventLog(EventLogEntryType level, string subj, List<string> subj_body = null)
        {
            if (subj_body == null)
            {
                subj_body = new List<string>();
            }
            using (EventLog eventLog = new EventLog("Application"))
            {
                eventLog.Source = APP_NAME;
                EventInstance eventInstance = new EventInstance(0, 0, level);
                var body = string.Join(Environment.NewLine, subj_body);
                EventLog.WriteEvent(eventLog.Source, eventInstance, new List<string>() { subj + Environment.NewLine + body }.ToArray());
            }
        }

        private async Task<bool> Authenticate(string userName)
        {
            await Task.Yield();
            return true;
        }
    }
}
