

namespace Omni2FA.Net.Utils {
    internal class Str {
        public static string sanitize(string input) {
            // for some reason, stirng.Trim() does not remove trailing \0 char
            if (input[input.Length - 1] == '\0')
                return input.Substring(0, input.Length - 1); // Remove trailing char
            return input.Trim();
        }

    }
}
