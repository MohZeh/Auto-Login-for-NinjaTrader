#if TEST_HOOKS
using System;
using System.IO;

namespace NinjaTraderAutoLogin
{
    // Compiled only into the TestHooks build, which CI uses to run the real worker end to end.
    // The release build has none of this: its data folder, registry key, Credential Manager
    // name and NinjaTrader signature check are fixed. This build uses test-only ones and trusts
    // only the fake target CI names, and it does not start without them.
    internal static class TestHooks
    {
        public const string DataVariable = "NTAL_TEST_DATA";
        public const string TargetVariable = "NTAL_TEST_TARGET";
        public const string CredentialTarget = "NinjaTraderAutoLogin.Tests/worker";
        public const string LatchKeyPath = @"Software\NinjaTraderAutoLogin.Tests\Worker";

        public static bool Configured
        {
            get
            {
                return Folder(DataVariable) != null && Folder(TargetVariable) != null;
            }
        }

        public static string RequiredFolder()
        {
            string data = Folder(DataVariable);
            if (data == null)
            {
                throw new InvalidOperationException("TestHooksNeedData");
            }

            return data;
        }

        public static string TrustedTarget
        {
            get
            {
                string target = Folder(TargetVariable);
                if (target == null)
                {
                    throw new InvalidOperationException("TestHooksNeedTarget");
                }

                return target;
            }
        }

        public static bool IsTrustedTarget(string path)
        {
            try
            {
                return path != null && File.Exists(path) &&
                    String.Equals(Path.GetFullPath(path), TrustedTarget, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
        }

        private static string Folder(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return String.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value) ? null : Path.GetFullPath(value);
        }
    }
}
#endif
