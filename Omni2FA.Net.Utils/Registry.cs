using Microsoft.Win32;
using System;

namespace Omni2FA.Net.Utils {
    public class Registry : IDisposable {

        private RegistryKey baseKey;
        private bool disposed = false;
        public string BaseKeyPath { get; private set; }

        public Registry(string baseKeyPath) {
            BaseKeyPath = baseKeyPath;
            try {                 
                baseKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(BaseKeyPath, false);
                if (baseKey == null) {
                    Log.Event(Log.Level.Warning, $"Registry base key not found: {BaseKeyPath}");
                }
            }
            catch (Exception ex) {
                Log.Event(Log.Level.Warning, $"Error reading settings from registry: {ex.Message}");
            }
        }

        public int GetIntRegistryValue(string valueName, int defaultValue) {
            if (disposed) throw new ObjectDisposedException(nameof(Registry));
            var val = baseKey?.GetValue(valueName);
            if (val != null && int.TryParse(val.ToString(), out int result))
                return result;
            return defaultValue;
        }

        public bool GetBoolRegistryValue(string valueName, bool defaultValue) {
            if (disposed) throw new ObjectDisposedException(nameof(Registry));
            var val = baseKey?.GetValue(valueName);
            if (val != null && int.TryParse(val.ToString(), out int result))
                return result == 1;
            return defaultValue;
        }

        public string GetStringRegistryValue(string valueName, string defaultValue) {
            if (disposed) throw new ObjectDisposedException(nameof(Registry));
            var val = baseKey?.GetValue(valueName);
            return val != null ? val.ToString() : defaultValue;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    baseKey?.Dispose();
                }
                disposed = true;
            }
        }

        ~Registry() {
            Dispose(false);
        }
    }
}
