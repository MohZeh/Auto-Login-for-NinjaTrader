using System.IO;

namespace NinjaTraderAutoLogin
{
    internal static class SettingsStore
    {
        public static Settings Load()
        {
            Settings settings = File.Exists(Paths.Settings) ? JsonFiles.Read<Settings>(Paths.Settings) : new Settings();
            if (settings == null)
            {
                throw new InvalidDataException("MissingSettings");
            }

            settings.Validate();
            return settings;
        }
        public static void Save(Settings settings)
        {
            settings.Validate();
            JsonFiles.Write(Paths.Settings, settings);
        }
    }
}
