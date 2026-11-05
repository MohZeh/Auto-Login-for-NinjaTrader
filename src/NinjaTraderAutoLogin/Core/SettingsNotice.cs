using System;

namespace NinjaTraderAutoLogin
{
    internal enum SettingsNotice
    {
        None, Unsaved, MissingCredentials, MissingExecutable
    }

    internal static class SettingsNoticePolicy
    {
        public static SettingsNotice Select(bool busyOrFailed, bool unsaved, bool idle, bool hasCredentials, bool executableExists)
        {
            if (busyOrFailed)
            {
                return SettingsNotice.None;
            }

            if (unsaved)
            {
                return SettingsNotice.Unsaved;
            }

            if (!idle)
            {
                return SettingsNotice.None;
            }

            return !hasCredentials ? SettingsNotice.MissingCredentials : !executableExists ? SettingsNotice.MissingExecutable : SettingsNotice.None;
        }
        public static bool Differs(Settings saved, string path, LaunchMode mode, TradingMode tradingMode, string username, string savedUsername, bool hasPasswordDraft)
        {
            return hasPasswordDraft || mode != saved.Mode || tradingMode != saved.TradingSelection ||
                !String.Equals(path.Trim(), saved.NinjaTraderPath, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(username.Trim(), savedUsername ?? String.Empty, StringComparison.Ordinal);
        }
    }
}
