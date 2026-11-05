using System;
using System.Diagnostics;
using System.IO;

namespace NinjaTraderAutoLogin
{
    internal enum NinjaTraderVersionStatus
    {
        Tested, Unknown, Invalid
    }

    // The login controls are not a public contract, so each NinjaTrader version is confirmed separately.
    internal static class NinjaTraderCompatibility
    {
        public const string TestedVersion = "8.1.8.4";
        private static readonly string[] TestedVersions = { TestedVersion };

        public static NinjaTraderVersionStatus Classify(string version)
        {
            Version parsed;
            if (String.IsNullOrWhiteSpace(version) || !Version.TryParse(version.Trim(), out parsed))
            {
                return NinjaTraderVersionStatus.Invalid;
            }

            return Array.IndexOf(TestedVersions, parsed.ToString()) >= 0 ? NinjaTraderVersionStatus.Tested : NinjaTraderVersionStatus.Unknown;
        }
        public static string ReadVersion(string path)
        {
            try
            {
                return File.Exists(path) ? FileVersionInfo.GetVersionInfo(path).FileVersion : null;
            }
            catch (FileNotFoundException) { return null; }
            catch (ArgumentException) { return null; }
        }
    }
}
