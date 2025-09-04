using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TgMfa
{
    public class TgAgent
    {
        private const string APP_NAME = "NPS-TgMfaAgent";
        private static ITelegramBotClient _botClient;
        //private static string _botName;
        private static string _botKey;

        // DLL initialization
        //[DllImport("kernel32.dll")]
        //private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);
        //private delegate bool ConsoleCtrlDelegate(int CtrlType);

        static TgAgent()
        {
            //LoadEnv();
            //_botName = Environment.GetEnvironmentVariable("TELEGRAM_BOT_NAME");
            _botKey = Environment.GetEnvironmentVariable("TELEGRAM_BOT_KEY");
            if (string.IsNullOrEmpty(_botKey))
            {
                WriteEventLog(EventLogEntryType.Error, "TELEGRAM_BOT_KEY is not defined in environment variables.");
                throw new Exception("TELEGRAM_BOT_KEY is not defined");
            }
            _botClient = new TelegramBotClient(_botKey);
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
            string tgUserId = await UserLookup(userName);
            if (tgUserId == null)
            {
                WriteEventLog(EventLogEntryType.Error, $"User {userName} not found in Telegram.");
                return false;
            }

            var request = new Telegram.Bot.Requests.SendMessageRequest {
                ChatId = new ChatId(tgUserId),
                Text = $"Authenticate? Reply 'yes' to continue.",
                ParseMode = ParseMode.Markdown
            };
            var message = await _botClient.SendRequest(request);

            // Wait for user response (poll for up to 60 seconds)
            bool isAuthenticated = false;
            int pollIntervalMs = 2000;
            int maxWaitMs = 60000;
            int waitedMs = 0;

            while (waitedMs < maxWaitMs)
            {
                // Poll for new messages from the user
                var updatesRequest = new Telegram.Bot.Requests.GetUpdatesRequest();
                var updates = await _botClient.SendRequest(updatesRequest);

                foreach (var update in updates)
                {
                    if (update.Message != null &&
                        update.Message.Chat.Id.ToString() == tgUserId &&
                        !string.IsNullOrEmpty(update.Message.Text))
                    {
                        if (update.Message.Text.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase))
                        {
                            isAuthenticated = true;
                            break;
                        }
                        else
                        {
                            isAuthenticated = false;
                            break;
                        }
                    }
                }

                if (isAuthenticated || waitedMs + pollIntervalMs >= maxWaitMs)
                    break;

                await Task.Delay(pollIntervalMs);
                waitedMs += pollIntervalMs;
            }

            return isAuthenticated;
        }

        private async Task<string> UserLookup(string userName)
        {
            // Stub: return a fixed chat id for testing
            return "123456789"; // Replace with actual lookup logic
        }
    }
}
