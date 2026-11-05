using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;

namespace NinjaTraderAutoLogin
{
    [DataContract]
    internal sealed class Settings
    {
        [DataMember] public int SchemaVersion = 1;
        [DataMember] public string NinjaTraderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NinjaTrader 8", "bin", "NinjaTrader.exe");
        [DataMember] public string LaunchMethod = "Shortcut";
        [DataMember] public string TradingMode = "Simulation";
        [DataMember] public string UiLanguage = SystemLanguage(CultureInfo.CurrentUICulture);
        [DataMember] public string UiTheme = "Dark";
        // The NinjaTrader version the owner agreed to use for Live although it has not been tested.
        [DataMember(EmitDefaultValue = false)] public string LiveAcceptedVersion;
        // NinjaTrader refused the saved password; it is not sent again until the owner decides.
        [DataMember(EmitDefaultValue = false)] public bool CredentialsRejected;
        // The sign-in window only stayed open, so the cause may be the connection instead of the password.
        [DataMember(EmitDefaultValue = false)] public bool RejectionUncertain;
        public static string SystemLanguage(CultureInfo culture)
        {
            string name = culture.TwoLetterISOLanguageName;
            return name == "fa" || name == "prs" ? "fa" : "en";
        }
        public LaunchMode Mode
        {
            get
            {
                return LaunchMethod == "Watch" ? LaunchMode.Watch : LaunchMode.Shortcut;
            }
        }
        public TradingMode TradingSelection
        {
            get
            {
                return TradingMode == "Live" ? NinjaTraderAutoLogin.TradingMode.Live : NinjaTraderAutoLogin.TradingMode.Simulation;
            }
        }
        public void Validate()
        {
            if (String.IsNullOrEmpty(UiLanguage))
            {
                UiLanguage = "fa"; // Existing candidate-1 files have no language slot.
            }

            if (String.IsNullOrEmpty(UiTheme))
            {
                UiTheme = "Dark";
            }

            if (UiLanguage != "fa" && UiLanguage != "en")
            {
                throw new InvalidDataException("UnsupportedLanguage");
            }

            if (UiTheme != "Dark" && UiTheme != "Light")
            {
                throw new InvalidDataException("UnsupportedTheme");
            }

            if (SchemaVersion != 1 || (LaunchMethod != "Shortcut" && LaunchMethod != "Watch") || (TradingMode != "Simulation" && TradingMode != "Live"))
            {
                throw new InvalidDataException("UnsupportedSettings");
            }

            if (String.IsNullOrWhiteSpace(NinjaTraderPath) || !Path.IsPathRooted(NinjaTraderPath) ||
                !String.Equals(Path.GetFileName(NinjaTraderPath), "NinjaTrader.exe", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("InvalidExecutablePath");
            }

            NinjaTraderPath = Path.GetFullPath(NinjaTraderPath);
        }
    }
}
