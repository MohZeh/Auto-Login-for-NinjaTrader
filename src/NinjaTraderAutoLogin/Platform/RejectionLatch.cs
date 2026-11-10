using System;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace NinjaTraderAutoLogin
{
    // A second, independent record of a refused password, kept in the registry. The settings
    // file holds the same fact; either one holds the password back, so one failed write,
    // followed by a restart, cannot lose the refusal. The value exists only while a refusal
    // is held. If both writes fail, the refusal lasts only while the app keeps running.
    internal sealed class RejectionLatch
    {
        public const string DefaultKeyPath = @"Software\NinjaTraderAutoLogin";
        private const string ValueName = "CredentialsRejected";
        public static readonly RejectionLatch Current = new RejectionLatch(DefaultKeyPath);
        private readonly string keyPath, parentPath, keyName;
        private readonly Func<string, bool, RegistryKey> open;
        private readonly Func<string, string[]> listSubKeys;

        public RejectionLatch(string keyPath)
            : this(keyPath, OpenCurrentUser, ListCurrentUser)
        {
        }

        // The two delegates let tests inject failures that access rules cannot produce.
        internal RejectionLatch(string keyPath, Func<string, bool, RegistryKey> open, Func<string, string[]> listSubKeys)
        {
            this.keyPath = keyPath;
            int split = keyPath.LastIndexOf('\\');
            parentPath = keyPath.Substring(0, split);
            keyName = keyPath.Substring(split + 1);
            this.open = open;
            this.listSubKeys = listSubKeys;
        }

        public RefusalReading Read()
        {
            try
            {
                using (RegistryKey key = open(keyPath, false))
                {
                    if (key == null)
                    {
                        // Null means either "not there" or "could not be opened". Only a parent
                        // that lists no such key counts as "not there".
                        return Listed() ? RefusalReading.Unavailable : RefusalReading.Known(RefusalState.None);
                    }

                    object value = key.GetValue(ValueName, null);
                    if (value == null)
                    {
                        return RefusalReading.Known(RefusalState.None);
                    }

                    // Anything unexpected still counts as a refusal.
                    return RefusalReading.Known(value is int && (int)value == (int)RefusalState.Uncertain ? RefusalState.Uncertain :
                        value is int && (int)value == (int)RefusalState.None ? RefusalState.None : RefusalState.Rejected);
                }
            }
            catch (Exception error) when (IsRegistryFailure(error))
            {
                AppFaults.Record("ReadRejectionLatch", error);
                return RefusalReading.Unavailable;
            }
        }

        // Returns true only when a successful read afterwards shows exactly the requested state.
        public bool Write(RefusalState state)
        {
            try
            {
                if (state == RefusalState.None)
                {
                    bool empty;
                    using (RegistryKey key = open(keyPath, true))
                    {
                        if (key == null)
                        {
                            return !Listed();
                        }

                        key.DeleteValue(ValueName, false);
                        empty = key.ValueCount == 0 && key.SubKeyCount == 0;
                    }
                    if (empty)
                    {
                        Registry.CurrentUser.DeleteSubKey(keyPath, false);
                    }
                }
                else
                {
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                    {
                        key.SetValue(ValueName, (int)state, RegistryValueKind.DWord);
                    }
                }

                RefusalReading check = Read();
                return check.Readable && check.State == state;
            }
            catch (Exception error) when (IsRegistryFailure(error))
            {
                AppFaults.Record("WriteRejectionLatch", error);
                return false;
            }
        }

        // Throws when the parent cannot be listed, which the callers treat as "could not be read".
        private bool Listed()
        {
            foreach (string name in listSubKeys(parentPath))
            {
                if (String.Equals(name, keyName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRegistryFailure(Exception error)
        {
            return error is SecurityException || error is UnauthorizedAccessException || error is IOException;
        }

        private static RegistryKey OpenCurrentUser(string path, bool writable)
        {
            return Registry.CurrentUser.OpenSubKey(path, writable);
        }

        private static string[] ListCurrentUser(string path)
        {
            using (RegistryKey parent = Registry.CurrentUser.OpenSubKey(path, false))
            {
                if (parent == null)
                {
                    throw new IOException("RejectionLatchParentUnavailable");
                }

                return parent.GetSubKeyNames();
            }
        }
    }
}
