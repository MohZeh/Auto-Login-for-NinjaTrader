using System;
using System.IO;

namespace NinjaTraderAutoLogin
{
    internal static class Paths
    {
#if TEST_HOOKS
        public static readonly string Data = TestHooks.RequiredFolder();
#else
        public static readonly string Data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NinjaTraderAutoLogin");
#endif
        public static string Settings
        {
            get
            {
                return Path.Combine(Data, "settings.json");
            }
        }
        public static string Evidence
        {
            get
            {
                return Path.Combine(Data, "evidence");
            }
        }
        public static readonly string Executable = System.Reflection.Assembly.GetExecutingAssembly().Location;
    }
}
