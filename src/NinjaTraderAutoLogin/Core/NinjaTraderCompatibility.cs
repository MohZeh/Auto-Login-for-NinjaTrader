using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;

namespace NinjaTraderAutoLogin
{
    internal enum NinjaTraderVersionStatus
    {
        Tested, Confirmed, Unknown, Invalid
    }

    // The login controls are not a public contract, so each NinjaTrader version is confirmed
    // separately: by a release (Tested), or on this computer by its first complete sign-in
    // (Confirmed). Safety does not depend on this list; unknown controls still stop the sign-in.
    internal static class NinjaTraderCompatibility
    {
        private static readonly string[] TestedVersions = { "8.1.8.4", "8.1.9.0" };
        public const int RememberedVersions = 5;

        public static string LatestTested
        {
            get
            {
                return TestedVersions[TestedVersions.Length - 1];
            }
        }

        public static NinjaTraderVersionStatus Classify(string version)
        {
            return Classify(version, null);
        }

        public static NinjaTraderVersionStatus Classify(string version, IEnumerable<string> confirmed)
        {
            string normal = Normalize(version);
            if (normal == null)
            {
                return NinjaTraderVersionStatus.Invalid;
            }

            if (Array.IndexOf(TestedVersions, normal) >= 0)
            {
                return NinjaTraderVersionStatus.Tested;
            }

            return confirmed != null && confirmed.Contains(normal) ? NinjaTraderVersionStatus.Confirmed : NinjaTraderVersionStatus.Unknown;
        }

        // Adds a version confirmed by a complete sign-in, keeping only the most recent few,
        // so returning to an earlier NinjaTrader does not bring the notice back.
        public static List<string> Remember(IEnumerable<string> confirmed, string version)
        {
            string normal = Normalize(version);
            var result = new List<string>();
            if (confirmed != null)
            {
                foreach (string item in confirmed)
                {
                    string kept = Normalize(item);
                    if (kept != null && kept != normal && !result.Contains(kept))
                    {
                        result.Add(kept);
                    }
                }
            }

            if (normal != null)
            {
                result.Add(normal);
            }

            return result.Count > RememberedVersions ? result.GetRange(result.Count - RememberedVersions, RememberedVersions) : result;
        }

        public static string Normalize(string version)
        {
            Version parsed;
            return String.IsNullOrWhiteSpace(version) || !Version.TryParse(version.Trim(), out parsed) ? null : parsed.ToString();
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
