using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;

namespace NinjaTraderAutoLogin.FakeTarget
{
    // The surface a fresh NinjaTrader shows after sign-in. UI Automation reads its class name.
    internal sealed class ControlCenter : UserControl
    {
    }

    // Stands in for the sign-in screens so the real UI Automation path runs without a real account.
    // It records only what arrived and the length of each value, never the values.
    internal static class Program
    {
        private static string scenario, record;

        [STAThread]
        private static int Main(string[] args)
        {
            scenario = args[0];
            record = args[1];
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            new DispatcherTimer(TimeSpan.FromSeconds(120), DispatcherPriority.Normal, delegate
            {
                Environment.Exit(3);
            }, application.Dispatcher).Start();
            application.Dispatcher.BeginInvoke((Action)delegate
            {
                if (scenario == "slow-start")
                {
                    After(1500, ShowLogin);
                }
                else
                {
                    ShowLogin();
                }
            });
            application.Run();
            return 0;
        }
        private static void After(int milliseconds, Action action)
        {
            Dispatcher dispatcher = Application.Current.Dispatcher;
            Task.Delay(milliseconds).ContinueWith(delegate
            {
                dispatcher.BeginInvoke(action);
            });
        }
        private static void Note(string line)
        {
            File.AppendAllText(record, line + Environment.NewLine);
        }
        private static Button Command(string id, string text, RoutedEventHandler click)
        {
            var button = new Button { Content = text, Margin = new Thickness(6) };
            AutomationProperties.SetAutomationId(button, id);
            button.Click += click;
            return button;
        }
        private static Window Screen(string title, UIElement content)
        {
            var window = new Window
            {
                Title = title,
                Width = 360,
                Height = 260,
                ShowActivated = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = 40,
                Top = 40,
                Content = content
            };
            window.Show();
            return window;
        }
        private static void ShowLogin()
        {
            bool changedIds = scenario == "changed-controls";
            var panel = new StackPanel();
            var user = new TextBox { Margin = new Thickness(6) };
            AutomationProperties.SetAutomationId(user, changedIds ? "userNameRenamed" : "tbUserName");
            var password = new PasswordBox { Margin = new Thickness(6) };
            AutomationProperties.SetAutomationId(password, "passwordBox");
            panel.Children.Add(user);
            panel.Children.Add(password);
            if (scenario == "challenge")
            {
                panel.Children.Add(new TextBlock { Text = "Enter your verification code" });
            }

            Window window = null;
            var message = new TextBlock();
            panel.Children.Add(message);
            panel.Children.Add(Command("btnLogin", "Login", delegate
            {
                Note("login " + user.Text.Length + " " + password.Password.Length);
                Submitted(window, message);
            }));
            window = Screen("Fake sign-in", panel);
        }
        private static void Submitted(Window login, TextBlock message)
        {
            switch (scenario)
            {
                case "rejected":
                    message.Text = "Unsuccessful login";
                    return;
                case "exit-after-login":
                    Environment.Exit(0);
                    return;
                case "login-returns":
                    return;
                case "dialog-after-login":
                    login.Close();
                    After(200, delegate
                    {
                        Window owner = Screen("Fake splash", new TextBlock { Text = "Loading" });
                        new Window { Owner = owner, Title = "Update available", Width = 300, Height = 160, ShowActivated = false, Content = new TextBlock { Text = "A dialog" } }.ShowDialog();
                    });
                    return;
                default:
                    login.Close();
                    After(scenario == "slow-network" ? 2500 : 300, ShowModes);
                    return;
            }
        }
        private static void ShowModes()
        {
            var panel = new StackPanel();
            Window window = null;
            panel.Children.Add(Command("btnSimulation", "Simulation", delegate
            {
                Note("mode Simulation");
                Opened(window);
            }));
            panel.Children.Add(Command("btnLiveTrading", scenario == "no-live-account" ? "Open Account" : "Live Trading", delegate
            {
                Note("mode Live");
                Opened(window);
            }));
            window = Screen("Fake environment", panel);
        }
        private static void Opened(Window modes)
        {
            modes.Close();
            After(300, delegate
            {
                var center = new ControlCenter { Content = new TextBlock { Text = "Open" } };
                AutomationProperties.SetAutomationId(center, "ControlCenter");
                Screen("Fake Control Center", center);
            });
        }
    }
}
