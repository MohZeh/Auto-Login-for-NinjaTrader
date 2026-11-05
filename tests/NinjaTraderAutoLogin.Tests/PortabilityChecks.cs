using System;
using System.Drawing;
using System.IO;
using NinjaTraderAutoLogin;

internal static partial class TestRunner
{
    private static void RunPortabilityChecks()
    {
        string original = Path.Combine(Path.GetTempPath(), "Helper original", StartupCommand.ExecutableName);
        string relocated = Path.Combine(Path.GetTempPath(), "Helper moved", "مسیر ' آزمایشی, x64", StartupCommand.ExecutableName);
        string previous = StartupCommand.Create(original);
        string expected = StartupCommand.Create(relocated);
        Test("Default NinjaTrader path uses the current Windows Program Files folder", delegate
        {
            Require(new Settings().NinjaTraderPath == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NinjaTrader 8", "bin", "NinjaTrader.exe"));
        });
        Test("User settings remain outside the movable application folder", delegate
        {
            Require(Paths.Settings == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NinjaTraderAutoLogin", "settings.json"));
        });
        Test("Owned Watch registration follows a relocated helper with spaces and Unicode", delegate
        {
            Require(StartupCommand.IsOwned(previous) && StartupCommand.IsOwned(expected));
            Require(StartupCommand.Next(previous, relocated, LaunchMode.Watch) == expected);
            Require(StartupCommand.Reconciled(previous, relocated, LaunchMode.Watch, path => false) == expected);
        });
        Test("Switching to Shortcut removes an owned registration from an older folder", delegate
        {
            Require(StartupCommand.Next(previous, relocated, LaunchMode.Shortcut) == null);
            Require(StartupCommand.Reconciled(previous, relocated, LaunchMode.Shortcut, path => false) == null);
        });
        Test("Running another working copy does not take over the startup entry", delegate
        {
            Require(StartupCommand.Reconciled(previous, relocated, LaunchMode.Watch, path => path == original) == previous);
            Require(StartupCommand.Reconciled(previous, relocated, LaunchMode.Shortcut, path => path == original) == previous);
            Require(StartupCommand.Reconciled(previous, original, LaunchMode.Shortcut, path => true) == null);
            Require(StartupCommand.Reconciled(previous, original, LaunchMode.Watch, path => true) == previous);
        });
        Test("Desktop shortcuts are created, repaired and never replace another application's", delegate
        {
            string folder = Path.Combine(Path.GetTempPath(), "AutoLoginShortcuts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                Require(DesktopShortcuts.Inspect(folder, original) == ShortcutState.Missing);
                DesktopShortcuts.Write(folder, original);
                Require(DesktopShortcuts.Inspect(folder, original) == ShortcutState.Ready);
                Require(DesktopShortcuts.Inspect(folder, relocated) == ShortcutState.Stale);
                DesktopShortcuts.Write(folder, relocated);
                Require(DesktopShortcuts.Inspect(folder, relocated) == ShortcutState.Ready);
                string launch = Path.Combine(folder, ProductInfo.EnglishName + ".lnk"), settings = Path.Combine(folder, ProductInfo.EnglishName + " Settings.lnk");
                File.Delete(settings);
                Require(DesktopShortcuts.Inspect(folder, relocated) == ShortcutState.Stale);
                DesktopShortcuts.Write(folder, relocated);
                Require(File.Exists(settings));
                File.Copy(settings, launch, true);
                Require(DesktopShortcuts.Inspect(folder, relocated) == ShortcutState.Foreign);
                byte[] foreign = File.ReadAllBytes(launch);
                bool rejected = false;
                try
                {
                    DesktopShortcuts.Write(folder, relocated);
                }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected);
                byte[] after = File.ReadAllBytes(launch);
                Require(foreign.Length == after.Length);
            }
            finally { Directory.Delete(folder, true); }
        });
        Test("A manually removed startup entry is not recreated by opening the helper", delegate
        {
            Require(StartupCommand.Reconciled(null, relocated, LaunchMode.Watch, path => false) == null);
            Require(StartupCommand.Next(null, relocated, LaunchMode.Watch) == expected);
        });
        Test("Configuration rollback restores the exact previous executable location", delegate
        {
            Require(StartupCommand.Restored(expected, previous) == previous);
            Require(StartupCommand.Restored(expected, null) == null);
            Require(StartupCommand.Restored(null, previous) == previous);
        });
        Test("Unknown startup commands and extra arguments are preserved by rejection", delegate
        {
            foreach (string foreign in new[] { previous + " --extra", previous.Replace("--watch", "--launch"), previous.Replace(StartupCommand.ExecutableName, "Other.exe"), original + " --watch", "" })
            {
                Require(!StartupCommand.IsOwned(foreign));
                bool rejected = false;
                try
                {
                    StartupCommand.Next(foreign, relocated, LaunchMode.Watch);
                }
                catch (InvalidDataException) { rejected = true; }
                Require(rejected);
                rejected = false;
                try
                {
                    StartupCommand.Restored(foreign, previous);
                }
                catch (InvalidDataException) { rejected = true; }
                Require(rejected);
            }
        });
        Test("Startup command rejects relative paths and quoting injection", delegate
        {
            foreach (string invalid in new[] { StartupCommand.ExecutableName, "\\" + StartupCommand.ExecutableName, original + "\" --extra", original + "\n", Path.ChangeExtension(original, ".cmd") })
            {
                bool rejected = false;
                try
                {
                    StartupCommand.Create(invalid);
                }
                catch (InvalidDataException) { rejected = true; }
                Require(rejected);
            }
        });
        Test("Both embedded icons contain the full Windows size range", delegate
        {
            foreach (string name in new[] { "launch.ico", "settings.ico" })
            {
                using (Stream resource = UiAssets.OpenImage(name))
                using (var reader = new BinaryReader(resource))
                {
                    Require(reader.ReadUInt16() == 0 && reader.ReadUInt16() == 1 && reader.ReadUInt16() == 7);
                    foreach (int expectedSize in new[] { 16, 24, 32, 48, 64, 128, 256 })
                    {
                        int width = reader.ReadByte(), height = reader.ReadByte();
                        Require((width == 0 ? 256 : width) == expectedSize && (height == 0 ? 256 : height) == expectedSize);
                        reader.ReadBytes(14);
                    }
                }
                using (Icon icon = UiAssets.LoadIcon(name))
                using (Bitmap image = icon.ToBitmap())
                {
                    Require(image.Width >= 16 && image.Height >= 16);
                }
            }
        });
        Test("Embedded images load from a different working directory without disk assets", delegate
        {
            string workingDirectory = Environment.CurrentDirectory;
            try
            {
                Environment.CurrentDirectory = Path.GetTempPath();
                using (Bitmap launch = UiAssets.LoadBitmap("launch.png"))
                using (Bitmap settings = UiAssets.LoadBitmap("settings.png"))
                {
                    Require(launch.Width == 256 && settings.Width == 256);
                    Require(launch.GetPixel(128, 32).ToArgb() != settings.GetPixel(128, 32).ToArgb());
                }
            }
            finally { Environment.CurrentDirectory = workingDirectory; }
        });
    }
}
