using System;

namespace NinjaTraderAutoLogin
{
    internal static class BuildInfo
    {
        public static string Version
        {
            get
            {
                var assembly = typeof(BuildInfo).Assembly;
                return ((System.Reflection.AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                    assembly, typeof(System.Reflection.AssemblyInformationalVersionAttribute))).InformationalVersion;
            }
        }
        public const string CredentialTarget = "NinjaTraderAutoLogin/default";
        public const int StageTimeoutSeconds = 120;
        public const int ProcessSettleSeconds = 10;
        // The sign-in window takes longer than this to appear, so a later check loses no time.
        public const int WatchScanMilliseconds = 3500;
    }
}
