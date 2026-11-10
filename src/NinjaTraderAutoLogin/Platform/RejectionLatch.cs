using System;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace NinjaTraderAutoLogin
{
    internal enum RefusalState
    {
        None = 0,
        Rejected = 1,
        Uncertain = 2
    }

    // A second, independent record of a refused password, kept in the registry. The settings
    // file holds the same fact; either one holds the password back, so one failed write,
    // followed by a restart, cannot lose the refusal. The value exists only while a refusal
    // is held. If both writes fail, the refusal lasts only while the app keeps running.
    internal sealed class RejectionLatch
    {
        public const string DefaultKeyPath = @"Software\NinjaTraderAutoLogin";
        private const string ValueName = "CredentialsRejected";
        public static readonly RejectionLatch Current = new RejectionLatch(DefaultKeyPath);
        private readonly string keyPath;

        public RejectionLatch(string keyPath)
        {
            this.keyPath = keyPath;
        }

        // A certain refusal outranks an uncertain one, and either outranks none.
        public static RefusalState Combine(RefusalState first, RefusalState second)
        {
            if (first == RefusalState.Rejected || second == RefusalState.Rejected)
            {
                return RefusalState.Rejected;
            }

            return first == RefusalState.Uncertain || second == RefusalState.Uncertain ? RefusalState.Uncertain : RefusalState.None;
        }

        public static RefusalState Of(Settings settings)
        {
            return !settings.CredentialsRejected ? RefusalState.None : settings.RejectionUncertain ? RefusalState.Uncertain : RefusalState.Rejected;
        }

        public RefusalState Read()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath, false))
                {
                    object value = key == null ? null : key.GetValue(ValueName, null);
                    if (value == null)
                    {
                        return RefusalState.None;
                    }

                    // Anything unexpected still counts as a refusal.
                    return value is int && (int)value == (int)RefusalState.Uncertain ? RefusalState.Uncertain :
                        value is int && (int)value == (int)RefusalState.None ? RefusalState.None : RefusalState.Rejected;
                }
            }
            catch (Exception error) when (error is SecurityException || error is UnauthorizedAccessException || error is IOException)
            {
                AppFaults.Record("ReadRejectionLatch", error);
                return RefusalState.None;
            }
        }

        // Returns true only when the registry now holds exactly the requested state.
        public bool Write(RefusalState state)
        {
            try
            {
                if (state == RefusalState.None)
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath, true))
                    {
                        if (key == null)
                        {
                            return true;
                        }

                        key.DeleteValue(ValueName, false);
                        if (key.ValueCount != 0 || key.SubKeyCount != 0)
                        {
                            return Read() == RefusalState.None;
                        }
                    }
                    Registry.CurrentUser.DeleteSubKey(keyPath, false);
                }
                else
                {
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                    {
                        key.SetValue(ValueName, (int)state, RegistryValueKind.DWord);
                    }
                }

                return Read() == state;
            }
            catch (Exception error) when (error is SecurityException || error is UnauthorizedAccessException || error is IOException)
            {
                AppFaults.Record("WriteRejectionLatch", error);
                return false;
            }
        }
    }
}
