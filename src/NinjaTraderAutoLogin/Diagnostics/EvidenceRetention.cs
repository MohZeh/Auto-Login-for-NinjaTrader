using System;
using System.IO;
using System.Linq;

namespace NinjaTraderAutoLogin
{
    // File names start with a sortable UTC time, so the newest records sort last.
    internal static class EvidenceRetention
    {
        public const int RunLimit = 50, FaultLimit = 20;

        public static void Prune(string directory, bool keepLayoutRecords)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            KeepNewest(directory, "run-*.json", RunLimit);
            KeepNewest(directory, "app-fault-*.json", FaultLimit);
            if (!keepLayoutRecords)
            {
                KeepNewest(directory, "settings-layout-*.json", 0);
                KeepNewest(directory, "surface-layout-*.json", 0);
            }
        }
        private static void KeepNewest(string directory, string pattern, int limit)
        {
            foreach (string file in Directory.GetFiles(directory, pattern).OrderByDescending(name => name, StringComparer.Ordinal).Skip(limit))
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
