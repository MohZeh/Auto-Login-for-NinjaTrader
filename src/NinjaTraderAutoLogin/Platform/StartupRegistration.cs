using System;
using System.IO;
using Microsoft.Win32;

namespace NinjaTraderAutoLogin
{
    internal static class StartupRegistration
    {
        private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "NinjaTraderAutoLogin";
        public static string Current()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath, false))
            {
                object value = key == null ? null : key.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (value != null && !(value is string))
                {
                    throw new InvalidDataException("StartupRegistrationConflict");
                }
                return value as string;
            }
        }
        public static void Apply(LaunchMode mode)
        {
            Write(StartupCommand.Next(Current(), Paths.Executable, mode));
        }

        public static void Reconcile(LaunchMode mode)
        {
            string previous = Current();
            string next = StartupCommand.Reconciled(previous, Paths.Executable, mode, File.Exists);
            if (!String.Equals(previous, next, StringComparison.OrdinalIgnoreCase))
            {
                Write(next);
            }
        }

        private static void Write(string command)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                if (command != null)
                {
                    key.SetValue(ValueName, command, RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(ValueName, false);
                }
            }
        }

        public static void Restore(string previous)
        {
            Write(StartupCommand.Restored(Current(), previous));
        }
    }
}
