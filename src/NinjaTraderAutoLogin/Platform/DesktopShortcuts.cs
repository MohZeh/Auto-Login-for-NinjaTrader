using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace NinjaTraderAutoLogin
{
    internal enum ShortcutState
    {
        Missing, Ready, Stale, Foreign
    }

    // Keeps the ownership rules of setup-shortcuts.ps1, so neither ever replaces another application's shortcut.
    internal static class DesktopShortcuts
    {
        private sealed class Entry
        {
            public string Name, Argument, Kind;
        }

        private static readonly Entry[] Entries =
        {
            new Entry { Name = ProductInfo.EnglishName + ".lnk", Argument = "--launch", Kind = "Launch" },
            new Entry { Name = ProductInfo.EnglishName + " Settings.lnk", Argument = "--settings", Kind = "Settings" }
        };

        // The product was previously named NinjaTrader Auto Login.
        private const string PreviousName = "NinjaTrader Auto Login";
        private static readonly Entry[] PreviousEntries =
        {
            new Entry { Name = PreviousName + ".lnk", Argument = "--launch", Kind = "Launch" },
            new Entry { Name = "NinjaTrader Login Settings.lnk", Argument = "--settings", Kind = "Settings" }
        };

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink
        {
        }

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int count, IntPtr findData, uint flags);
            void GetIDList(out IntPtr list);
            void SetIDList(IntPtr list);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int count);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string value);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int count);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string value);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int count);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string value);
            void GetHotkey(out short value);
            void SetHotkey(short value);
            void GetShowCmd(out int value);
            void SetShowCmd(int value);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int count, out int index);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string value, int index);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string value, uint reserved);
            void Resolve(IntPtr window, uint flags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string value);
        }

        public static string DesktopFolder
        {
            get
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            }
        }

        public static ShortcutState Inspect(string folder, string executable)
        {
            int present = 0, current = 0;
            foreach (Entry entry in Entries)
            {
                string path = Path.Combine(folder, entry.Name);
                if (!File.Exists(path))
                {
                    continue;
                }

                present++;
                string target, arguments, description;
                Read(path, out target, out arguments, out description);
                if (!Owned(entry, target, arguments, description))
                {
                    return ShortcutState.Foreign;
                }

                if (String.Equals(target, executable, StringComparison.OrdinalIgnoreCase))
                {
                    current++;
                }
            }

            if (present == 0)
            {
                return PreviousShortcuts(folder).Count > 0 ? ShortcutState.Stale : ShortcutState.Missing;
            }

            return current == Entries.Length ? ShortcutState.Ready : ShortcutState.Stale;
        }
        private static bool Owned(Entry entry, string target, string arguments, string description)
        {
            bool knownDescription = description == PreviousName || description == PreviousName + " | " + entry.Kind || description == ProductInfo.EnglishName + " | " + entry.Kind;
            return knownDescription && arguments == entry.Argument &&
                String.Equals(Path.GetFileName(target), StartupCommand.ExecutableName, StringComparison.OrdinalIgnoreCase);
        }
        private static System.Collections.Generic.List<string> PreviousShortcuts(string folder)
        {
            var found = new System.Collections.Generic.List<string>();
            foreach (Entry entry in PreviousEntries)
            {
                string path = Path.Combine(folder, entry.Name), target, arguments, description;
                if (File.Exists(path))
                {
                    Read(path, out target, out arguments, out description);
                    if (Owned(entry, target, arguments, description))
                    {
                        found.Add(path);
                    }
                }
            }

            return found;
        }

        public static void Write(string folder, string executable)
        {
            if (Inspect(folder, executable) == ShortcutState.Foreign)
            {
                throw new InvalidOperationException("ForeignShortcut");
            }

            string applicationFolder = Path.GetDirectoryName(executable);
            string settingsIcon = Path.Combine(applicationFolder, "assets", "settings.ico");
            foreach (Entry entry in Entries)
            {
                object instance = new ShellLink();
                try
                {
                    var link = (IShellLinkW)instance;
                    link.SetPath(executable);
                    link.SetArguments(entry.Argument);
                    link.SetWorkingDirectory(applicationFolder);
                    link.SetDescription(ProductInfo.EnglishName + " | " + entry.Kind);
                    link.SetIconLocation(entry.Kind == "Settings" && File.Exists(settingsIcon) ? settingsIcon : executable, 0);
                    ((IPersistFile)instance).Save(Path.Combine(folder, entry.Name), true);
                }
                finally { Marshal.FinalReleaseComObject(instance); }
            }

            foreach (string previous in PreviousShortcuts(folder))
            {
                File.Delete(previous);
            }
        }

        private static void Read(string path, out string target, out string arguments, out string description)
        {
            object instance = new ShellLink();
            try
            {
                ((IPersistFile)instance).Load(path, 0);
                var link = (IShellLinkW)instance;
                var buffer = new StringBuilder(32768);
                link.GetPath(buffer, buffer.Capacity, IntPtr.Zero, 4);
                target = buffer.ToString();
                buffer.Clear();
                link.GetArguments(buffer, buffer.Capacity);
                arguments = buffer.ToString();
                buffer.Clear();
                link.GetDescription(buffer, buffer.Capacity);
                description = buffer.ToString();
            }
            finally { Marshal.FinalReleaseComObject(instance); }
        }
    }
}
