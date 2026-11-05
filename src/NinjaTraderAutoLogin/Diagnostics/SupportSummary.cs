using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace NinjaTraderAutoLogin
{
    // Safe text a user can paste into a support request. Only named fields are copied, so
    // usernames, passwords, file paths and control details can never reach the summary.
    internal static class SupportSummary
    {
        private const int RecentRuns = 5, RecentFaults = 3;

        public static string Build(string folder, string version, string windows)
        {
            var text = new StringBuilder();
            text.AppendLine(ProductInfo.EnglishName + " " + version);
            text.AppendLine("Windows: " + windows);
            bool wroteVersion = false;
            foreach (string path in Newest(folder, "run-*.json", RecentRuns))
            {
                RunRecord run = Read<RunRecord>(path);
                if (run == null)
                {
                    continue;
                }

                if (!wroteVersion)
                {
                    text.AppendLine("NinjaTrader: " + run.NinjaTraderVersion);
                    text.AppendLine("Recent sign-ins (UTC):");
                    wroteVersion = true;
                }
                text.AppendLine(String.Format(CultureInfo.InvariantCulture, "- {0} {1} {2}: {3}, submitted {4}, Simulation {5}, Live {6}{7}",
                    run.StartedUtc, run.Trigger, run.RequestedMode, run.Outcome, run.LoginSubmissions, run.SimulationSelections, run.LiveSelections,
                    run.CauseType == null ? String.Empty : ", " + run.CauseType + " 0x" + run.CauseHResult.ToString("X8", CultureInfo.InvariantCulture)));
            }
            if (!wroteVersion)
            {
                text.AppendLine("Recent sign-ins: none");
            }

            foreach (string path in Newest(folder, "app-fault-*.json", RecentFaults))
            {
                AppFaultRecord fault = Read<AppFaultRecord>(path);
                if (fault != null)
                {
                    text.AppendLine(String.Format(CultureInfo.InvariantCulture, "Fault {0} {1}: {2} 0x{3:X8}", fault.Utc, fault.Phase, fault.ExceptionType, fault.HResult));
                }
            }
            return text.ToString();
        }
        private static IEnumerable<string> Newest(string folder, string pattern, int count)
        {
            if (!Directory.Exists(folder))
            {
                return new string[0];
            }

            string[] files = Directory.GetFiles(folder, pattern);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            Array.Reverse(files);
            var newest = new List<string>();
            for (int index = 0; index < files.Length && index < count; index++)
            {
                newest.Add(files[index]);
            }

            return newest;
        }
        private static T Read<T>(string path) where T : class
        {
            try
            {
                return JsonFiles.Read<T>(path);
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (SerializationException) { return null; }
            catch (InvalidDataException) { return null; }
        }
    }
}
