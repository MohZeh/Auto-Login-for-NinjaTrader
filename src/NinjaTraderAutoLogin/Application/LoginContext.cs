using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NinjaTraderAutoLogin
{
    internal sealed class OperationCompletion
    {
        public LoginResult Result;
        public string EvidencePath;
        public ProcessIdentity OpenCopy;
    }

    internal sealed class LoginContext : ApplicationContext
    {
        private Settings settings;
        private readonly WindowsCredentials credentials = new WindowsCredentials();
        private readonly AttemptGate gate = new AttemptGate();
        private readonly Control dispatcher = new Control();
        private readonly NotifyIcon tray;
        private readonly System.Windows.Forms.Timer timer;
        private readonly EventWaitHandle settingsSignal, launchSignal;
        private readonly ToolStripMenuItem pauseMenu;
        private readonly ToolStripMenuItem settingsMenu, launchMenu, cancelMenu, exitMenu;
        private readonly DisposeOnce cleanup = new DisposeOnce();
        private SettingsForm form;
        private CancellationTokenSource cancellation;
        private bool active, exiting, firstTick = true, settingsInvalid;
        private readonly MonitoringPause pause = new MonitoringPause();
        private ProcessWatch openCopy;
        private string initialCommand, statusKey = "ready";
        private Outcome? lastOutcome;
        private bool lastActionUncertain;
        private TradingMode lastTradingMode = TradingMode.Simulation;
        private DateTime nextPoll = DateTime.MinValue, nextStagePoll = DateTime.MinValue;
        private LoginProgressWindow progress;
        private volatile string activeEvidence;
        private readonly ProblemGrace watchSettle = new ProblemGrace(TimeSpan.FromSeconds(BuildInfo.ProcessSettleSeconds));
        public bool Running
        {
            get
            {
                return active;
            }
        }
        public bool WatchEnabled
        {
            get
            {
                return settings.Mode == LaunchMode.Watch;
            }
        }
        public TradingMode SavedTradingMode
        {
            get
            {
                return settings.TradingSelection;
            }
        }
        public string SavedExecutablePath
        {
            get
            {
                return settings.NinjaTraderPath;
            }
        }
        public Settings SavedSettings
        {
            get
            {
                return settings;
            }
        }
        public bool StatusIdle
        {
            get
            {
                return statusKey == "ready" && !lastOutcome.HasValue && !lastActionUncertain && !settingsInvalid;
            }
        }
        public LaunchMode SavedLaunchMode
        {
            get
            {
                return settings.Mode;
            }
        }
        public bool Paused
        {
            get
            {
                return pause.Paused;
            }
        }
        public string Language
        {
            get
            {
                return settings.UiLanguage;
            }
        }

        public LoginContext(string command, EventWaitHandle settingsSignal, EventWaitHandle launchSignal)
        {
            this.initialCommand = command;
            this.settingsSignal = settingsSignal;
            this.launchSignal = launchSignal;
            try
            {
                settings = SettingsStore.Load();
            }
            catch { settings = new Settings(); settingsInvalid = true; statusKey = "invalid"; }
            // The settings file may have missed a refusal that the registry kept.
            HoldRefusal(RejectionLatch.Combine(RejectionLatch.Of(settings), RejectionLatch.Current.Read()));
            PruneEvidence();
            // Without saved settings the mode is only a default, so it must not change the startup entry.
            if (!settingsInvalid && File.Exists(Paths.Settings))
            {
                try
                {
                    StartupRegistration.Reconcile(settings.Mode);
                }
                catch (Exception error)
                {
                    settingsInvalid = true;
                    statusKey = "startupError";
                    AppFaults.Record("StartupRelocation", error);
                }
            }
            IntPtr dispatcherHandle = dispatcher.Handle;
            var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };
            settingsMenu = new ToolStripMenuItem();
            settingsMenu.Click += delegate
            {
                OpenSettings();
            };
            menu.Items.Add(settingsMenu);
            launchMenu = new ToolStripMenuItem();
            launchMenu.Click += delegate
            {
                BeginLogin(true, null);
            };
            menu.Items.Add(launchMenu);
            pauseMenu = new ToolStripMenuItem();
            pauseMenu.Click += delegate
            {
                ToggleMonitoring();
            };
            menu.Items.Add(pauseMenu);
            cancelMenu = new ToolStripMenuItem();
            cancelMenu.Click += delegate
            {
                CancelCurrent();
            };
            menu.Items.Add(cancelMenu);
            exitMenu = new ToolStripMenuItem();
            exitMenu.Click += delegate
            {
                RequestExit();
            };
            menu.Items.Add(exitMenu);
            tray = new NotifyIcon { Icon = UiAssets.LoadIcon("launch.ico"), Text = ProductInfo.EnglishName, ContextMenuStrip = menu };
            ApplyMenuLanguage();
            tray.DoubleClick += delegate
            {
                OpenSettings();
            };
            timer = new System.Windows.Forms.Timer { Interval = 250 };
            timer.Tick += Tick;
            timer.Start();
        }

        private void Tick(object sender, EventArgs e)
        {
            if (firstTick)
            {
                firstTick = false;
                if (initialCommand == "--watch" && (settingsInvalid || settings.Mode != LaunchMode.Watch))
                {
                    ExitThread();
                    return;
                }
                tray.Visible = settings.Mode == LaunchMode.Watch;
                pauseMenu.Enabled = settings.Mode == LaunchMode.Watch;
                if (settingsInvalid || initialCommand == "--settings")
                {
                    OpenSettings();
                }
                else if (initialCommand == "--launch")
                {
                    BeginLogin(true, null);
                }
            }
            UpdateProgress();
            if (settingsSignal.WaitOne(0))
            {
                OpenSettings();
            }

            if (launchSignal.WaitOne(0))
            {
                BeginLogin(true, null);
            }

            if (settings.Mode != LaunchMode.Watch || settingsInvalid || pause.Paused || active || exiting || DateTime.UtcNow < nextPoll)
            {
                return;
            }

            nextPoll = openCopy != null ? DateTime.UtcNow.AddSeconds(1) : DateTime.UtcNow.AddMilliseconds(BuildInfo.WatchScanMilliseconds);
            // NinjaTrader runs only one instance, so nothing needs scanning until this one ends.
            if (openCopy != null)
            {
                if (!openCopy.HasEnded)
                {
                    return;
                }

                ReleaseOpenCopy();
            }

            try
            {
                ProcessScan scan = ProcessScan.Find(settings.NinjaTraderPath);
                if (scan.Unsettled)
                {
                    if (watchSettle.Expired(DateTime.UtcNow))
                    {
                        throw scan.Problem();
                    }

                    return;
                }

                watchSettle.Clear();
                if (scan.Process != null)
                {
                    openCopy = ProcessWatch.Open(scan.Process);
                    BeginLogin(false, scan.Process);
                }
            }
            catch (Exception ex)
            {
                watchSettle.Clear();
                pause.Fault();
                pauseMenu.Checked = true;
                Outcome failure = ex is LoginProblem ? ((LoginProblem)ex).Outcome :
                    ex is System.ComponentModel.Win32Exception || ex is UnauthorizedAccessException ? Outcome.AccessDenied : Outcome.UnexpectedFailure;
                try
                {
                    var evidence = new RunEvidence(new ProcessIdentity { ImagePath = settings.NinjaTraderPath, Version = "Unresolved" }, "Watcher", settings.TradingSelection);
                    evidence.Cause(ex is LoginProblem ? ex.InnerException : ex);
                    evidence.Complete(failure, 0, 0);
                }
                catch { }
                SetStatus(failure == Outcome.MultipleInstances ? "watchMultiple" : "watchError");
                tray.ShowBalloonTip(4000, ProductInfo.EnglishName, GetStatus(Language), ToolTipIcon.Warning);
            }
        }

        private void ReleaseOpenCopy()
        {
            if (openCopy != null)
            {
                openCopy.Dispose();
                openCopy = null;
            }
        }
        private static ProcessIdentity SettledProcess(string path, CancellationToken token)
        {
            var settle = new ProblemGrace(TimeSpan.FromSeconds(BuildInfo.ProcessSettleSeconds));
            while (true)
            {
                ProcessScan scan = ProcessScan.Find(path);
                if (!scan.Unsettled)
                {
                    return scan.Process;
                }

                if (settle.Expired(DateTime.UtcNow))
                {
                    throw scan.Problem();
                }

                if (token.WaitHandle.WaitOne(250))
                {
                    token.ThrowIfCancellationRequested();
                }
            }
        }

        public void OpenSettings()
        {
            if (exiting)
            {
                return;
            }

            if (form == null || form.IsDisposed)
            {
                form = new SettingsForm(this, settings, credentials);
                form.FormClosed += delegate
                {
                    form = null;
                    if (settings.Mode == LaunchMode.Shortcut && !active)
                    {
                        ExitThread();
                    }
                };
            }
            form.RefreshStatus();
            form.Show();
            form.Activate();
            form.FocusRejectedPassword();
        }

        public void SaveConfiguration(Settings next, string username, string password)
        {
            if (active)
            {
                throw new InvalidOperationException("OperationActive");
            }

            next.Validate();
            next.LiveAcceptedVersion = settings.LiveAcceptedVersion;
            next.CredentialsRejected = settings.CredentialsRejected && String.IsNullOrEmpty(password);
            next.RejectionUncertain = next.CredentialsRejected && settings.RejectionUncertain;
            if (!File.Exists(next.NinjaTraderPath))
            {
                throw new InvalidDataException("MissingExecutable");
            }

            try
            {
                new ConfigurationTransaction(new ConfigurationSettingsWriter(), new ConfigurationStartupSettings(), credentials)
                    .Save(settings, next, username, password);
            }
            catch (ConfigurationSaveException failure)
            {
                if (failure.RecoveryIncomplete)
                {
                    settingsInvalid = true;
                    pause.Fault();
                    pauseMenu.Checked = true;
                    SetStatus("invalid");
                    AppFaults.Record("ConfigurationRecovery", failure);
                }
                throw;
            }
            settings = next;
            settingsInvalid = false;
            if (!next.CredentialsRejected && !RejectionLatch.Current.Write(RefusalState.None))
            {
                // The file is clear but the registry still refuses; keep refusing until both agree.
                HoldRefusal(RejectionLatch.Current.Read());
            }
            // The path or start method may have changed, so the next check looks afresh.
            ReleaseOpenCopy();
            pause.Saved(next.Mode);
            pauseMenu.Checked = pause.Paused;
            pauseMenu.Enabled = next.Mode == LaunchMode.Watch;
            tray.Visible = next.Mode == LaunchMode.Watch;
            ApplyMenuLanguage();
            SetStatus(next.Mode != LaunchMode.Watch ? "savedShortcut" : pause.Paused ? "paused" : "savedWatch");
        }

        // Language and theme apply at once and do not depend on account details.
        public void SavePreferences(string language, string theme)
        {
            settings.UiLanguage = language;
            settings.UiTheme = theme;
            ApplyMenuLanguage();
            if (settingsInvalid)
            {
                return;
            }

            try
            {
                SettingsStore.Save(settings);
            }
            catch (Exception error) { AppFaults.Record("SavePreferences", error); }
        }
        private static void PruneEvidence()
        {
            try
            {
                EvidenceRetention.Prune(Paths.Evidence, SettingsLayoutEvidence.Enabled);
            }
            catch (Exception error) { AppFaults.Record("PruneEvidence", error); }
        }

        public void LaunchFromSettings()
        {
            BeginLogin(true, null);
        }
        public void CancelCurrent()
        {
            if (cancellation != null)
            {
                cancellation.Cancel();
            }
        }
        public void ToggleMonitoring()
        {
            if (!WatchEnabled)
            {
                return;
            }

            pause.Toggle();
            pauseMenu.Checked = pause.Paused;
            SetStatus(pause.Paused ? "paused" : "watching");
            ApplyMenuLanguage();
        }
        public void ExitApplication()
        {
            RequestExit();
        }
        private void BeginLogin(bool explicitRequest, ProcessIdentity existing)
        {
            if (active || exiting)
            {
                return;
            }

            if (settingsInvalid)
            {
                OpenSettings();
                return;
            }
            if (!explicitRequest && existing != null && !gate.CanWatch(existing.Key))
            {
                return;
            }

            active = true;
            tray.Visible = settings.Mode == LaunchMode.Watch;
            cancellation = new CancellationTokenSource();
            CancellationToken token = cancellation.Token;
            string path = settings.NinjaTraderPath;
            TradingMode requestedMode = settings.TradingSelection;
            lastTradingMode = requestedMode;
            SetStatus("loggingIn");
            activeEvidence = null;
            if (explicitRequest && (form == null || form.IsDisposed || !form.Visible))
            {
                ShowProgress(requestedMode);
            }
            Task.Factory.StartNew(delegate
            {
                return RunOperation(path, existing, explicitRequest, requestedMode, token);
            }, token,
                TaskCreationOptions.None, TaskScheduler.Default).ContinueWith(delegate (Task<OperationCompletion> task)
            {
                OperationCompletion complete = task.Status == TaskStatus.RanToCompletion ? task.Result :
                    new OperationCompletion { Result = new LoginResult { RequestedMode = requestedMode, Outcome = token.IsCancellationRequested ? Outcome.Cancelled : Outcome.UnexpectedFailure } };
                if (!dispatcher.IsDisposed)
                {
                    try
                    {
                        dispatcher.BeginInvoke((Action)delegate
                        {
                            Completed(complete);
                        });
                    }
                    catch (InvalidOperationException) { }
                }
            }, TaskScheduler.Default);
        }

        private OperationCompletion RunOperation(string path, ProcessIdentity existing, bool explicitRequest, TradingMode requestedMode, CancellationToken token)
        {
            var duration = Stopwatch.StartNew();
            OperationCompletion first = RunAttempt(path, existing, explicitRequest, requestedMode, token);
            if (!RelaunchPolicy.ShouldRelaunch(explicitRequest, first.OpenCopy != null, first.Result, duration.Elapsed) || !WaitUntilClosed(first.OpenCopy, token))
            {
                return first;
            }

            return RunAttempt(path, null, explicitRequest, requestedMode, token);
        }
        private static bool WaitUntilClosed(ProcessIdentity copy, CancellationToken token)
        {
            var settle = new ProblemGrace(TimeSpan.FromSeconds(30));
            while (true)
            {
                try
                {
                    using (Process process = Process.GetProcessById(copy.Id))
                    {
                        if (process.HasExited || process.StartTime.ToUniversalTime().Ticks != copy.StartedUtcTicks)
                        {
                            return true;
                        }
                    }
                }
                catch (ArgumentException) { return true; }
                catch (InvalidOperationException) { return true; }
                catch (System.ComponentModel.Win32Exception) { }
                if (settle.Expired(DateTime.UtcNow) || token.WaitHandle.WaitOne(250))
                {
                    return false;
                }
            }
        }
        private OperationCompletion RunAttempt(string path, ProcessIdentity existing, bool explicitRequest, TradingMode requestedMode, CancellationToken token)
        {
            RunEvidence evidence = null;
            bool ownsGate = false;
            try
            {
                token.ThrowIfCancellationRequested();
                if (explicitRequest)
                {
                    ConfirmRejectedRetry();
                }

                ProcessIdentity process = existing ?? SettledProcess(path, token);
                bool alreadyRunning = process != null;
                if (process == null)
                {
                    if (!explicitRequest || !File.Exists(path))
                    {
                        throw new LoginProblem(Outcome.ProcessExited);
                    }

                    RequireKnownVersion(path, requestedMode);
                    // A file that is not signed by NinjaTrader is not started at all.
                    if (!NinjaTraderSignature.IsTrusted(path))
                    {
                        throw new LoginProblem(Outcome.UntrustedTarget);
                    }

                    ProcessScan scan = null;
                    using (Process launched = Process.Start(new ProcessStartInfo(path) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(path) }))
                    {
                        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
                        while (process == null && DateTime.UtcNow < deadline)
                        {
                            token.ThrowIfCancellationRequested();
                            scan = ProcessScan.Find(path);
                            if (!scan.Unsettled)
                            {
                                process = scan.Process;
                            }

                            if (process == null && token.WaitHandle.WaitOne(100))
                            {
                                token.ThrowIfCancellationRequested();
                            }
                        }
                    }
                    if (process == null)
                    {
                        throw scan != null && scan.Unsettled ? scan.Problem() : new LoginProblem(Outcome.ProcessExited);
                    }
                }
                if (!gate.Begin(process.Key, explicitRequest))
                {
                    return new OperationCompletion { Result = new LoginResult { RequestedMode = requestedMode, Outcome = Outcome.Cancelled } };
                }

                ownsGate = true;
                if (alreadyRunning)
                {
                    RequireKnownVersion(path, requestedMode);
                }

                token.ThrowIfCancellationRequested();
                evidence = new RunEvidence(process, explicitRequest ? "ShortcutOrLaunchButton" : "Watcher", requestedMode);
                activeEvidence = evidence.FilePath;
                return new OperationCompletion { Result = AutomationWorker.Execute(process, alreadyRunning, CredentialsRejected, requestedMode, evidence, token), EvidencePath = evidence.FilePath, OpenCopy = alreadyRunning ? process : null };
            }
            catch (Exception ex)
            {
                Outcome outcome = ex is OperationCanceledException ? Outcome.Cancelled :
                    ex is LoginProblem ? ((LoginProblem)ex).Outcome :
                    ex is System.ComponentModel.Win32Exception || ex is UnauthorizedAccessException ? Outcome.AccessDenied : Outcome.UnexpectedFailure;
                var failure = new LoginResult { RequestedMode = requestedMode, Outcome = outcome };
                Exception cause = ex is OperationCanceledException ? null : ex is LoginProblem ? ex.InnerException : ex;
                try
                {
                    if (evidence == null)
                    {
                        evidence = new RunEvidence(new ProcessIdentity { Id = 0, StartedUtcTicks = 0, ImagePath = path, Version = "Unresolved" }, explicitRequest ? "ShortcutOrLaunchButton" : "Watcher", requestedMode);
                        evidence.Cause(cause);
                        if (!evidence.Complete(outcome, 0, 0))
                        {
                            failure.Outcome = Outcome.EvidenceUnavailable;
                        }
                    }
                    else
                    {
                        failure = evidence.Interrupt(outcome, requestedMode, cause);
                    }
                }
                catch
                {
                    failure.Outcome = Outcome.EvidenceUnavailable;
                    failure.ActionCompletionUncertain = true;
                }
                return new OperationCompletion { Result = failure, EvidencePath = evidence == null ? null : evidence.FilePath };
            }
            finally
            {
                if (ownsGate)
                {
                    gate.End();
                }
            }
        }

        // Simulation may continue on an untested version with a notice. Live also needs one agreement per version.
        private void RequireKnownVersion(string path, TradingMode requestedMode)
        {
            string version = NinjaTraderCompatibility.ReadVersion(path);
            NinjaTraderVersionStatus status = NinjaTraderCompatibility.Classify(version);
            if (status == NinjaTraderVersionStatus.Invalid)
            {
                throw new LoginProblem(Outcome.UntrustedTarget);
            }

            if (status == NinjaTraderVersionStatus.Tested)
            {
                return;
            }

            bool allowed = false;
            dispatcher.Invoke((Action)delegate
            {
                allowed = requestedMode != TradingMode.Live || settings.LiveAcceptedVersion == version || ConfirmUnknownVersion(version);
                if (allowed)
                {
                    ShowVersionNotice(version);
                }
            });
            if (!allowed)
            {
                throw new LoginProblem(Outcome.LiveVersionNotConfirmed);
            }
        }
        private bool Agrees(ConfirmationContent content)
        {
            using (var dialog = new ConfirmationDialog(Language, content))
            {
                bool hasOwner = form != null && !form.IsDisposed && form.Visible;
                dialog.StartPosition = hasOwner ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen;
                dialog.TopMost = !hasOwner;
                return (hasOwner ? dialog.ShowDialog(form) : dialog.ShowDialog()) == DialogResult.Yes;
            }
        }
        private bool ConfirmUnknownVersion(string version)
        {
            if (!Agrees(ConfirmationContent.For(Language, LiveConfirmation.UnknownVersion, version)))
            {
                return false;
            }

            settings.LiveAcceptedVersion = version;
            SaveOwnSettings("SaveLiveVersion");
            return true;
        }
        private void ConfirmRejectedRetry()
        {
            bool allowed = true, cleared = true;
            dispatcher.Invoke((Action)delegate
            {
                if (settings.CredentialsRejected)
                {
                    allowed = Agrees(ConfirmationContent.RetryRejected(Language, settings.RejectionUncertain));
                    cleared = allowed && ClearRefusal("SaveRejectedRetry");
                }
            });
            if (!allowed)
            {
                throw new LoginProblem(Outcome.CredentialsRejected);
            }

            if (!cleared)
            {
                throw new LoginProblem(Outcome.RefusalNotCleared);
            }
        }
        // A refusal is lifted only when both records are cleared. Otherwise the app keeps
        // refusing, which matches what the worker will read.
        private bool ClearRefusal(string phase)
        {
            RefusalState before = RejectionLatch.Of(settings);
            settings.CredentialsRejected = settings.RejectionUncertain = false;
            bool saved = SaveOwnSettings(phase);
            bool unlatched = RejectionLatch.Current.Write(RefusalState.None);
            if (saved && unlatched)
            {
                return true;
            }

            HoldRefusal(before);
            return false;
        }
        private void HoldRefusal(RefusalState state)
        {
            settings.CredentialsRejected = state != RefusalState.None;
            settings.RejectionUncertain = state == RefusalState.Uncertain;
        }
        private bool SaveOwnSettings(string phase)
        {
            try
            {
                SettingsStore.Save(settings);
                return true;
            }
            catch (Exception error)
            {
                AppFaults.Record(phase, error);
                return false;
            }
        }
        public void CredentialsRemoved()
        {
            if (settings.CredentialsRejected)
            {
                ClearRefusal("SaveCredentialsRemoved");
            }
        }
        public bool CredentialsRejected
        {
            get
            {
                return settings.CredentialsRejected;
            }
        }
        public bool RejectionUncertain
        {
            get
            {
                return settings.RejectionUncertain;
            }
        }
        public bool Exiting
        {
            get
            {
                return exiting;
            }
        }
        public bool IsNinjaTraderOpen(string path)
        {
            try
            {
                return ProcessScan.Find(path).Process != null;
            }
            catch (Exception error) when (error is ArgumentException || error is IOException || error is NotSupportedException)
            {
                return false;
            }
        }
        private void ShowVersionNotice(string version)
        {
            string text = UiText.UnknownVersionNotice(Language, version);
            if (progress != null)
            {
                progress.SetNotice(text);
            }

            if (tray.Visible)
            {
                tray.ShowBalloonTip(4000, ProductInfo.EnglishName, text, ToolTipIcon.Warning);
            }
        }

        private void Completed(OperationCompletion completed)
        {
            active = false;
            PruneEvidence();
            if (cancellation != null)
            {
                cancellation.Dispose();
                cancellation = null;
            }
            Outcome outcome = completed.Result.Outcome;
            lastOutcome = outcome;
            if ((outcome == Outcome.AuthenticationFailed || outcome == Outcome.LoginNotAccepted) && completed.Result.LoginSubmissions > 0 && !settingsInvalid)
            {
                settings.CredentialsRejected = true;
                settings.RejectionUncertain = outcome == Outcome.LoginNotAccepted;
                // Two independent records, so one failed write cannot lose the refusal on restart.
                bool latched = RejectionLatch.Current.Write(RejectionLatch.Of(settings));
                if (!SaveOwnSettings("SaveCredentialsRejected"))
                {
                    // Watching stops until the owner acts. If the registry write failed too,
                    // the refusal lives only in memory, so a restart must not come first.
                    pause.Fault();
                    pauseMenu.Checked = true;
                    if (!latched)
                    {
                        AppFaults.Record("RefusalNotDurable", new IOException("RefusalNotDurable"));
                    }
                }
            }

            lastActionUncertain = completed.Result.ActionCompletionUncertain;
            lastTradingMode = completed.Result.RequestedMode;
            if (form != null && !form.IsDisposed)
            {
                form.RefreshStatus();
            }

            if (exiting)
            {
                ExitThread();
                return;
            }
            bool showingResult = progress != null && (outcome == Outcome.Success || outcome == Outcome.AlreadyOpen);
            if (showingResult)
            {
                progress.Finish(Messages.For(outcome, Language, completed.Result.RequestedMode), outcome == Outcome.AlreadyOpen ? 6000 : 3000, ProgressResultClosed);
            }
            else
            {
                CloseProgress();
            }

            bool normal = outcome == Outcome.Success || outcome == Outcome.AlreadyOpen || outcome == Outcome.Cancelled;
            if (!normal)
            {
                OpenSettings();
            }
            if (!showingResult && settings.Mode == LaunchMode.Shortcut && (form == null || form.IsDisposed))
            {
                ExitThread();
            }
        }
        private void ShowProgress(TradingMode mode)
        {
            CloseProgress();
            UiAssets.Palette = UiPalette.For(settings.UiTheme);
            progress = new LoginProgressWindow(Language, mode);
            progress.CancelRequested += CancelCurrent;
            progress.Show();
        }
        private void UpdateProgress()
        {
            if (progress == null || !active || DateTime.UtcNow < nextStagePoll)
            {
                return;
            }

            nextStagePoll = DateTime.UtcNow.AddSeconds(1);
            string path = activeEvidence;
            progress.SetStage(UiText.ProgressStage(Language, path == null ? null : RunEvidence.LastStage(path), lastTradingMode));
        }
        private void ProgressResultClosed()
        {
            CloseProgress();
            if (!active && settings.Mode == LaunchMode.Shortcut && (form == null || form.IsDisposed))
            {
                ExitThread();
            }
        }
        private void CloseProgress()
        {
            if (progress != null)
            {
                progress.Dispose();
                progress = null;
            }
        }
        private void SetStatus(string key)
        {
            statusKey = key;
            lastOutcome = null;
            lastActionUncertain = false;
            if (form != null && !form.IsDisposed)
            {
                form.RefreshStatus();
            }
        }
        public string GetStatus(string language)
        {
            if (lastActionUncertain)
            {
                return UiText.Choose(language, "Login stopped. Check NinjaTrader before trying again.", "ورود متوقف شد؛ پیش از تلاش دوباره، وضعیت نینجاتریدر را بررسی کنید.");
            }

            return lastOutcome.HasValue ? Messages.For(lastOutcome.Value, language, lastTradingMode) : UiText.Status(statusKey, language, statusKey == "loggingIn" ? lastTradingMode : settings.TradingSelection);
        }
        public bool StatusIsError
        {
            get
            {
                return settingsInvalid || (lastOutcome.HasValue && lastOutcome.Value != Outcome.Success && lastOutcome.Value != Outcome.AlreadyOpen && lastOutcome.Value != Outcome.Cancelled);
            }
        }
        private void ApplyMenuLanguage()
        {
            string language = Language;
            settingsMenu.Text = UiText.Choose(language, "Settings", "تنظیمات");
            launchMenu.Text = UiText.Choose(language, "Launch NinjaTrader — ", "اجرای نینجاتریدر — ") + UiText.ModeName(settings.TradingSelection, language);
            pauseMenu.Text = pause.Paused ? UiText.Choose(language, "Resume watching", "ادامهٔ پایش") : UiText.Choose(language, "Pause watching", "توقف موقت پایش");
            cancelMenu.Text = UiText.Choose(language, "Cancel current login", "لغو ورود جاری");
            exitMenu.Text = UiText.Choose(language, "Exit", "خروج از برنامه");
            tray.ContextMenuStrip.RightToLeft = language == "fa" ? RightToLeft.Yes : RightToLeft.No;
            tray.ContextMenuStrip.Font = UiFonts.Get(language, tray.ContextMenuStrip.Font.SizeInPoints, FontStyle.Regular);
            tray.Text = ProductInfo.EnglishName + " | " + (settings.TradingSelection == TradingMode.Live ? "LIVE" : settings.TradingMode);
        }
        private void RequestExit()
        {
            exiting = true;
            timer.Stop();
            if (cancellation != null)
            {
                cancellation.Cancel();
                SetStatus("cancelling");
            }
            else
            {
                ExitThread();
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && cleanup.TryBegin())
            {
                exiting = true;
                if (cancellation != null)
                {
                    cancellation.Cancel();
                }

                CloseProgress();
                timer.Stop();
                timer.Dispose();
                ReleaseOpenCopy();
                tray.Visible = false;
                ContextMenuStrip menu = tray.ContextMenuStrip;
                Icon icon = tray.Icon;
                tray.ContextMenuStrip = null;
                tray.Icon = null;
                tray.Dispose();
                if (menu != null)
                {
                    menu.Dispose();
                }

                if (icon != null)
                {
                    icon.Dispose();
                }

                if (form != null && !form.IsDisposed)
                {
                    form.Dispose();
                }

                dispatcher.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
