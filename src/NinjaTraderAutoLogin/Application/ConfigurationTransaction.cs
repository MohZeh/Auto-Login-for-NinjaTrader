using System;
using System.Collections.Generic;

namespace NinjaTraderAutoLogin
{
    internal interface ISettingsWriter
    {
        void Save(Settings settings);
    }

    internal interface IStartupSettings
    {
        string Read();
        void Apply(LaunchMode mode);
        void Restore(string previous);
    }

    internal interface ICredentialWriter
    {
        void Save(string username, string password);
    }

    internal sealed class ConfigurationSaveException : Exception
    {
        public readonly string[] RecoveryFailures;

        public ConfigurationSaveException(Exception cause, List<string> recoveryFailures)
            : base("ConfigurationSaveFailed", cause)
        {
            RecoveryFailures = recoveryFailures.ToArray();
        }

        public bool RecoveryIncomplete
        {
            get
            {
                return RecoveryFailures.Length != 0;
            }
        }
    }

    internal sealed class ConfigurationTransaction
    {
        private readonly ISettingsWriter settings;
        private readonly IStartupSettings startup;
        private readonly ICredentialWriter credentials;

        public ConfigurationTransaction(ISettingsWriter settings, IStartupSettings startup, ICredentialWriter credentials)
        {
            this.settings = settings;
            this.startup = startup;
            this.credentials = credentials;
        }

        public void Save(Settings previous, Settings next, string username, string password)
        {
            next.Validate();
            string previousStartup = startup.Read();
            bool startupWritten = false;
            bool settingsAttempted = false;
            try
            {
                startup.Apply(next.Mode);
                startupWritten = true;
                settingsAttempted = true;
                settings.Save(next);
                if (!String.IsNullOrEmpty(password))
                {
                    credentials.Save(username, password);
                }
            }
            catch (Exception cause)
            {
                var recovery = new List<string>();
                if (settingsAttempted)
                {
                    try
                    {
                        settings.Save(previous);
                    }
                    catch (Exception error) { recovery.Add("Settings:" + error.GetType().FullName); }
                }
                if (startupWritten)
                {
                    try
                    {
                        startup.Restore(previousStartup);
                    }
                    catch (Exception error) { recovery.Add("Startup:" + error.GetType().FullName); }
                }
                throw new ConfigurationSaveException(cause, recovery);
            }
        }
    }

    internal sealed class ConfigurationSettingsWriter : ISettingsWriter
    {
        public void Save(Settings settings)
        {
            SettingsStore.Save(settings);
        }
    }

    internal sealed class ConfigurationStartupSettings : IStartupSettings
    {
        public string Read()
        {
            return StartupRegistration.Current();
        }
        public void Apply(LaunchMode mode)
        {
            StartupRegistration.Apply(mode);
        }
        public void Restore(string previous)
        {
            StartupRegistration.Restore(previous);
        }
    }
}
