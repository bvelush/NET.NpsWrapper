// --------------------------------------------------------------------------------------------------------------------
// <copyright>
//   Copyright lestoilfante 2023 (https://github.com/lestoilfante)
//   
//   GNU General Public License version 2.1 (GPLv2.1) 
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Omni2FA.NPS.Adapter.Utils {
    //public enum LogLevel {
    //    Trace,
    //    Information,
    //    Warning,
    //    Error
    //}

    /// <summary>
    /// Helper class for writing to Windows Event Log.
    /// </summary>
    internal static class Log {
        private const string APP_NAME = "Omni2FA.Adapter";
        private static bool _enableTraceLogging = false;

        public enum Level {
            Trace,
            Information,
            Warning,
            Error
        }

        /// <summary>
        /// Sets whether trace logging is enabled.
        /// </summary>
        /// <param name="enabled">True to enable trace logging, false to disable it</param>
        public static void SetTraceLoggingEnabled(bool enabled) {
            _enableTraceLogging = enabled;
        }

        /// <summary>
        /// Writes Windows Event Log (Application)
        /// </summary>
        /// <param name="level">Event Level</param>
        /// <param name="subj">Event first row</param>
        /// <param name="subj_body">Event additional rows to append</param>
        public static void Event(Level level, string subj, List<string> subj_body = null) {
            EventLogEntryType winLevel;
            switch (level) {
                case Level.Trace:
                    if (!_enableTraceLogging) {
                        return;
                    }
                    subj = "[TRACE] " + subj;
                    winLevel = EventLogEntryType.Information;
                    break;
                case Level.Information:
                    winLevel = EventLogEntryType.Information;
                    break;
                case Level.Warning:
                    winLevel = EventLogEntryType.Warning;
                    break;
                case Level.Error:
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
