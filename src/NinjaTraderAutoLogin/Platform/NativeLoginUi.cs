using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;

namespace NinjaTraderAutoLogin
{
    internal sealed class NativeLoginUi : ILoginUi
    {
        private readonly ProcessIdentity process;
        private static readonly string[] Ids = { "tbUserName", "passwordBox", "btnLogin", "btnSimulation", "btnLiveTrading", "ControlCenter" };
        private static readonly string[] AuthenticationErrors = {
            "unsuccessful login", "incorrect username", "incorrect password", "invalid username",
            "invalid password", "invalid credentials", "login failed", "failed to log in", "unable to log in"
        };
        private static readonly string[] Challenges = {
            "verification code", "authentication code", "two-factor", "two factor", "captcha",
            "verify your identity", "accept the terms", "accept terms", "authentication required"
        };
        private static readonly string[] AccountCreation = { "open account", "open an account", "create account" };
        private static readonly Condition AnyKnownControl = new OrCondition(
            new PropertyCondition(AutomationElement.AutomationIdProperty, Ids[0]), new PropertyCondition(AutomationElement.AutomationIdProperty, Ids[1]),
            new PropertyCondition(AutomationElement.AutomationIdProperty, Ids[2]), new PropertyCondition(AutomationElement.AutomationIdProperty, Ids[3]),
            new PropertyCondition(AutomationElement.AutomationIdProperty, Ids[4]), new PropertyCondition(AutomationElement.AutomationIdProperty, Ids[5]));
        // Each UI Automation query runs on NinjaTrader's own window thread. A loaded workspace makes
        // them slow, so the pause grows with the cost and the diagnostic walk runs only now and then.
        private const int MaximumPauseMilliseconds = 1500;
        private static readonly TimeSpan DiagnosticInterval = TimeSpan.FromSeconds(5);
        private DateTime nextDiagnostics = DateTime.MinValue;
        public NativeLoginUi(ProcessIdentity process)
        {
            this.process = process;
        }

        private List<AutomationElement> Windows()
        {
            process.Verify();
            using (Process target = Process.GetProcessById(process.Id))
            {
                ProcessSecurity.Verify(target);
            }

            AutomationElementCollection matches = AutomationElement.RootElement.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id));
            if (matches.Count > 64)
            {
                throw new LoginProblem(Outcome.UnsupportedUi);
            }

            var result = new List<AutomationElement>();
            foreach (AutomationElement item in matches)
            {
                result.Add(item);
            }

            return result;
        }

        private List<AutomationElement> Find(string id, List<AutomationElement> windows)
        {
            var result = new List<AutomationElement>();
            var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, id);
            foreach (AutomationElement window in windows)
            {
                AutomationElementCollection found = window.FindAll(TreeScope.Subtree, condition);
                if (found.Count > 8)
                {
                    throw new LoginProblem(Outcome.UnsupportedUi);
                }

                foreach (AutomationElement element in found)
                {
                    if (element.Current.ProcessId == process.Id)
                    {
                        result.Add(element);
                    }
                }
            }
            return result;
        }

        // One walk per window finds all the known controls, in place of one walk for each of them.
        private Dictionary<AutomationElement, List<AutomationElement>> FindKnown(List<AutomationElement> windows)
        {
            var result = new Dictionary<AutomationElement, List<AutomationElement>>();
            foreach (AutomationElement window in windows)
            {
                var matches = new List<AutomationElement>();
                var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (AutomationElement element in window.FindAll(TreeScope.Subtree, AnyKnownControl))
                {
                    string id = element.Current.AutomationId;
                    int count;
                    counts.TryGetValue(id, out count);
                    counts[id] = count + 1;
                    if (count + 1 > 8)
                    {
                        throw new LoginProblem(Outcome.UnsupportedUi);
                    }

                    if (element.Current.ProcessId == process.Id)
                    {
                        matches.Add(element);
                    }
                }
                // The window list can repeat a window while NinjaTrader is opening new ones.
                if (!result.ContainsKey(window))
                {
                    result.Add(window, matches);
                }
            }

            return result;
        }

        private static bool Visible(AutomationElement element)
        {
            return !element.Current.IsOffscreen;
        }

        private AutomationElement Unique(string id, ControlType type, bool password, bool valuePattern, bool invokePattern)
        {
            var visible = new List<AutomationElement>();
            foreach (AutomationElement element in Find(id, Windows()))
            {
                if (Visible(element))
                {
                    visible.Add(element);
                }
            }

            if (visible.Count != 1)
            {
                throw new LoginProblem(Outcome.UnsupportedUi);
            }

            AutomationElement match = visible[0];
            if (match.Current.ControlType != type || !match.Current.IsEnabled || match.Current.IsPassword != password)
            {
                throw new LoginProblem(Outcome.UnsupportedUi);
            }

            object pattern;
            if (valuePattern && (!match.TryGetCurrentPattern(ValuePattern.Pattern, out pattern) || ((ValuePattern)pattern).Current.IsReadOnly))
            {
                throw new LoginProblem(Outcome.UnsupportedUi);
            }

            if (invokePattern && !match.TryGetCurrentPattern(InvokePattern.Pattern, out pattern))
            {
                throw new LoginProblem(Outcome.UnsupportedUi);
            }

            return match;
        }

        public UiSnapshot Inspect()
        {
            var cost = Stopwatch.StartNew();
            try
            {
                return InspectOnce();
            }
            finally
            {
                Thread.Sleep((int)Math.Min(MaximumPauseMilliseconds, cost.ElapsedMilliseconds));
            }
        }

        private UiSnapshot InspectOnce()
        {
            try
            {
                var snapshot = new UiSnapshot();
                List<AutomationElement> windows = Windows();
                Dictionary<AutomationElement, List<AutomationElement>> found = FindKnown(windows);
                foreach (string id in Ids)
                {
                    foreach (AutomationElement item in found.SelectMany(window => window.Value).Where(element => element.Current.AutomationId == id))
                    {
                        object ignored;
                        var control = new ControlEvidence
                        {
                            AutomationId = id,
                            ClassName = item.Current.ClassName,
                            ControlType = item.Current.ControlType.ProgrammaticName,
                            Enabled = item.Current.IsEnabled,
                            Offscreen = item.Current.IsOffscreen,
                            IsPassword = item.Current.IsPassword,
                            ValuePattern = item.TryGetCurrentPattern(ValuePattern.Pattern, out ignored),
                            InvokePattern = item.TryGetCurrentPattern(InvokePattern.Pattern, out ignored)
                        };
                        snapshot.Controls.Add(control);
                    }
                }
                snapshot = ControlContract.Evaluate(snapshot.Controls);
                if (snapshot.ControlCenter && !snapshot.Ambiguous)
                {
                    return snapshot;
                }

                snapshot.LiveAccountUnavailable = snapshot.LiveReady && OffersAccountCreation(windows);
                foreach (AutomationElement window in windows)
                {
                    if (window.Current.IsOffscreen)
                    {
                        continue;
                    }

                    object pattern;
                    if (!snapshot.LoginReady && !snapshot.SimulationReady && !snapshot.LiveReady && !snapshot.ControlCenter &&
                        window.TryGetCurrentPattern(WindowPattern.Pattern, out pattern) && ((WindowPattern)pattern).Current.IsModal)
                    {
                        snapshot.ManualRequired = true;
                    }
                    // Inspect labels only inside the login form, never account/chart/workspace text.
                    AutomationElement loginButton = found[window].Find(element => element.Current.AutomationId == "btnLogin");
                    if (loginButton == null || loginButton.Current.IsOffscreen)
                    {
                        continue;
                    }
                    // Read text labels only for fixed classifications. Never export their contents.
                    AutomationElementCollection labels = window.FindAll(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
                    if (labels.Count > 300)
                    {
                        continue;
                    }

                    foreach (AutomationElement label in labels)
                    {
                        if (label.Current.IsOffscreen || label.Current.IsPassword || label.Current.ProcessId != process.Id)
                        {
                            continue;
                        }

                        string text = label.Current.Name ?? String.Empty;
                        if (ContainsAny(text, AuthenticationErrors))
                        {
                            snapshot.AuthenticationError = true;
                        }

                        if (ContainsAny(text, Challenges))
                        {
                            snapshot.ManualRequired = true;
                        }
                    }
                }
                if (!snapshot.LoginReady && !snapshot.SimulationReady && !snapshot.LiveReady && !snapshot.ControlCenter && DateTime.UtcNow >= nextDiagnostics)
                {
                    nextDiagnostics = DateTime.UtcNow + DiagnosticInterval;
                    // Bounded diagnostic fallback for changed selectors. No Name, Value or Text reads.
                    foreach (AutomationElement window in windows)
                    {
                        var inputCondition = new OrCondition(
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
                        AutomationElementCollection inputs = window.FindAll(TreeScope.Descendants, inputCondition);
                        if (inputs.Count > 40)
                        {
                            continue;
                        }

                        foreach (AutomationElement item in inputs)
                        {
                            if (item.Current.IsOffscreen || item.Current.ProcessId != process.Id)
                            {
                                continue;
                            }

                            string id = item.Current.AutomationId ?? String.Empty;
                            bool known = Array.IndexOf(Ids, id) >= 0;
                            if (known || id.Length > 64)
                            {
                                continue;
                            }

                            object ignored;
                            snapshot.Controls.Add(new ControlEvidence
                            {
                                AutomationId = id,
                                ClassName = item.Current.ClassName,
                                ControlType = item.Current.ControlType.ProgrammaticName,
                                Enabled = item.Current.IsEnabled,
                                Offscreen = false,
                                IsPassword = item.Current.IsPassword,
                                ValuePattern = item.TryGetCurrentPattern(ValuePattern.Pattern, out ignored),
                                InvokePattern = item.TryGetCurrentPattern(InvokePattern.Pattern, out ignored)
                            });
                        }
                    }
                }
                return snapshot;
            }
            catch (ElementNotAvailableException) { return new UiSnapshot(); }
            catch (InvalidOperationException) { return new UiSnapshot(); }
            catch (COMException) { return new UiSnapshot(); }
            catch (TimeoutException) { return new UiSnapshot(); }
            catch (ArgumentException error) { throw new LoginProblem(Outcome.ProcessExited, error); }
            catch (UnauthorizedAccessException error) { throw new LoginProblem(Outcome.AccessDenied, error); }
        }

        // An account without Live offers account creation on this button. The text
        // is read only for this fixed classification and is never exported.
        private bool OffersAccountCreation(List<AutomationElement> windows)
        {
            foreach (AutomationElement button in Find("btnLiveTrading", windows))
            {
                if (Visible(button) && OffersAccountCreation(button))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsAny(string text, string[] patterns)
        {
            foreach (string pattern in patterns)
            {
                if (text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        public void Submit(Secret credentials, CancellationToken cancellation)
        {
            try
            {
                AutomationElement user = Unique("tbUserName", ControlType.Edit, false, true, false);
                AutomationElement password = Unique("passwordBox", ControlType.Edit, true, true, false);
                Unique("btnLogin", ControlType.Button, false, false, true);
                cancellation.ThrowIfCancellationRequested();
                process.Verify();
                ((ValuePattern)user.GetCurrentPattern(ValuePattern.Pattern)).SetValue(credentials.Username);
                cancellation.ThrowIfCancellationRequested();
                process.Verify();
                ((ValuePattern)password.GetCurrentPattern(ValuePattern.Pattern)).SetValue(credentials.Password);
                cancellation.ThrowIfCancellationRequested();
                AutomationElement login = Unique("btnLogin", ControlType.Button, false, false, true);
                ((InvokePattern)login.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            }
            catch (ElementNotAvailableException) { throw new LoginProblem(Outcome.UnsupportedUi); }
            catch (InvalidOperationException) { throw new LoginProblem(Outcome.UnsupportedUi); }
            catch (UnauthorizedAccessException) { throw new LoginProblem(Outcome.AccessDenied); }
        }

        private static bool OffersAccountCreation(AutomationElement button)
        {
            if (ContainsAny(button.Current.Name ?? String.Empty, AccountCreation))
            {
                return true;
            }

            AutomationElementCollection labels = button.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
            foreach (AutomationElement label in labels)
            {
                if (ContainsAny(label.Current.Name ?? String.Empty, AccountCreation))
                {
                    return true;
                }
            }

            return false;
        }

        public void SelectMode(TradingMode mode, CancellationToken cancellation)
        {
            try
            {
                AutomationElement selected = Unique(mode == TradingMode.Live ? "btnLiveTrading" : "btnSimulation", ControlType.Button, false, false, true);
                // The earlier inspection saw a real Live account; the button itself is checked again
                // so a label that changed in between still never opens an account page.
                if (mode == TradingMode.Live && OffersAccountCreation(selected))
                {
                    throw new LoginProblem(Outcome.LiveAccountUnavailable);
                }

                cancellation.ThrowIfCancellationRequested();
                process.Verify();
                ((InvokePattern)selected.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            }
            catch (ElementNotAvailableException) { throw new LoginProblem(Outcome.UnsupportedUi); }
            catch (InvalidOperationException) { throw new LoginProblem(Outcome.UnsupportedUi); }
            catch (UnauthorizedAccessException) { throw new LoginProblem(Outcome.AccessDenied); }
        }
    }
}
