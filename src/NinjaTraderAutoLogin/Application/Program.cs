using System;
using System.Globalization;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == AutomationWorker.Command)
            {
                return AutomationWorker.Run(args[1]);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            string command = args.Length == 0 ? "--settings" : args[0];
            if (args.Length > 1 || (command != "--settings" && command != "--launch" && command != "--watch"))
            {
                return 2;
            }

            string prefix;
            using (WindowsIdentity current = WindowsIdentity.GetCurrent())
            {
                prefix = @"Local\NinjaTraderAutoLogin." + current.User.Value;
            }

            bool created;
            using (var mutex = new Mutex(true, prefix + ".Owner", out created))
            {
                if (!created)
                {
                    if (command == "--watch")
                    {
                        return 0;
                    }

                    for (int attempt = 0; attempt < 20; attempt++)
                    {
                        try
                        {
                            using (EventWaitHandle signal = EventWaitHandle.OpenExisting(prefix + (command == "--launch" ? ".Launch" : ".Settings")))
                            {
                                signal.Set();
                            }

                            return 0;
                        }
                        catch (WaitHandleCannotBeOpenedException) { Thread.Sleep(100); }
                        catch (UnauthorizedAccessException) { return 3; }
                    }
                    return 3;
                }
                string phase = "Initializing";
                try
                {
                    using (var settingsSignal = new EventWaitHandle(false, EventResetMode.AutoReset, prefix + ".Settings"))
                    using (var launchSignal = new EventWaitHandle(false, EventResetMode.AutoReset, prefix + ".Launch"))
                    using (var context = new LoginContext(command, settingsSignal, launchSignal))
                    {
                        phase = "Running";
                        Application.Run(context);
                        phase = "Closing";
                    }
                }
                catch (Exception error)
                {
                    AppFaults.Record(phase, error);
                    string language = Settings.SystemLanguage(CultureInfo.CurrentUICulture);
                    try
                    {
                        language = SettingsStore.Load().UiLanguage;
                    }
                    catch { }
                    MessageBox.Show(UiText.Choose(language, "The app encountered an error. A diagnostic report was saved; reopen Settings to continue.",
                        "برنامه با خطا مواجه شد. گزارش تشخیصی ثبت شد؛ تنظیمات را دوباره باز کنید."), "NinjaTrader Auto Login", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
                finally { UiFonts.Release(); mutex.ReleaseMutex(); }
            }
            return 0;
        }
    }
}
