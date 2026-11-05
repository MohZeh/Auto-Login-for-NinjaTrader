using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using NinjaTraderAutoLogin;

internal sealed class FakeClock : IClock
{
    private DateTime now = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc);
    public DateTime UtcNow
    {
        get
        {
            return now;
        }
    }
    public Action OnWait;
    public void Wait(CancellationToken token)
    {
        if (OnWait != null)
        {
            OnWait();
        }

        token.ThrowIfCancellationRequested();
        now = now.AddSeconds(1);
    }
}
internal sealed class FakeCredentials : ICredentials
{
    public int Reads;
    public bool Missing;
    public Secret Read()
    {
        Reads++;
        return Missing ? null : new Secret("TEST-USER-DO-NOT-EXPORT", "TEST-PASSWORD-DO-NOT-EXPORT");
    }
}
internal sealed class FakeUi : ILoginUi
{
    public readonly List<UiSnapshot> Script = new List<UiSnapshot>();
    public int Inspections, Submissions, Selections;
    public readonly List<TradingMode> SelectedModes = new List<TradingMode>();
    public Outcome? Failure;
    public Exception Crash;
    public UiSnapshot Inspect()
    {
        if (Crash != null)
        {
            throw Crash;
        }

        if (Failure.HasValue)
        {
            throw new LoginProblem(Failure.Value);
        }

        int index = Math.Min(Inspections++, Script.Count - 1);
        return Script[index];
    }
    public void Submit(Secret credentials, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!credentials.Valid)
        {
            throw new Exception("No test secret");
        }

        Submissions++;
    }
    public void SelectMode(TradingMode mode, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Selections++;
        SelectedModes.Add(mode);
    }
}
internal sealed class FakeSettingsCredentials : ICredentialSettings
{
    public CredentialMetadata ReadMetadata()
    {
        return new CredentialMetadata("TEST-USER", true);
    }
    public void Delete()
    {
    }
}
internal sealed class FakeEvidence : IRunEvidence
{
    public bool Writable = true;
    public readonly List<EvidenceEvent> Events = new List<EvidenceEvent>();
    public bool Event(Stage stage, string action, UiSnapshot snapshot)
    {
        Events.Add(new EvidenceEvent { Stage = stage.ToString(), Action = action, Controls = snapshot == null ? null : snapshot.Controls });
        return Writable;
    }
    public int CompletedSimulation, CompletedLive;
    public Exception LastCause;
    public void Cause(Exception error)
    {
        LastCause = error;
    }
    public bool Complete(Outcome outcome, int login, int simulation, int live = 0)
    {
        CompletedSimulation = simulation;
        CompletedLive = live;
        return Writable;
    }
}
[DataContract]
internal sealed class Check
{
    [DataMember] public string Name; [DataMember] public bool Passed; [DataMember] public string Failure;
}
[DataContract]
internal sealed class Report
{
    [DataMember] public string Candidate = BuildInfo.Version; [DataMember] public string Scope = "Headless model and serialization only; no Native/UI acceptance"; [DataMember] public List<Check> Checks = new List<Check>();
}

internal static partial class TestRunner
{
    private static readonly Report report = new Report();
    private static void Require(bool condition, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        if (!condition)
        {
            throw new Exception("Assertion failed at line " + line);
        }
    }
    private static double Luminance(Color color)
    {
        double[] values = { color.R / 255.0, color.G / 255.0, color.B / 255.0 };
        for (int i = 0; i < 3; i++)
        {
            values[i] = values[i] <= 0.04045 ? values[i] / 12.92 : Math.Pow((values[i] + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * values[0] + 0.7152 * values[1] + 0.0722 * values[2];
    }
    private static double Contrast(Color a, Color b)
    {
        double x = Luminance(a), y = Luminance(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }
    private static void Test(string name, Action body)
    {
        var check = new Check { Name = name };
        try
        {
            body();
            check.Passed = true;
        }
        catch (Exception ex) { check.Failure = ex.GetType().Name + ": " + ex.Message; }
        report.Checks.Add(check);
        Console.WriteLine((check.Passed ? "PASS " : "FAIL ") + name);
    }
    private static FakeUi Script(params UiSnapshot[] snapshots)
    {
        var ui = new FakeUi();
        ui.Script.AddRange(snapshots);
        return ui;
    }
    private static LoginResult Run(FakeUi ui, FakeCredentials credentials, FakeClock clock, FakeEvidence evidence, bool existing, CancellationToken token)
    {
        return new LoginEngine(ui, credentials, clock, evidence, TimeSpan.FromSeconds(4)).Run(existing, token);
    }
    private static LoginResult Run(FakeUi ui)
    {
        return Run(ui, new FakeCredentials(), new FakeClock(), new FakeEvidence(), false, CancellationToken.None);
    }
    private static LoginResult Replay(string path, bool existing)
    {
        RunRecord record = JsonFiles.Read<RunRecord>(path);
        var ui = new FakeUi();
        foreach (EvidenceEvent item in record.Events)
        {
            if (item.Action == "Observed" && item.Controls != null)
            {
                ui.Script.Add(ControlContract.Evaluate(item.Controls));
            }
        }

        Require(ui.Script.Count > 0);
        return new LoginEngine(ui, new FakeCredentials(), new FakeClock(), new FakeEvidence(), TimeSpan.FromSeconds(120)).Run(existing, CancellationToken.None);
    }
    private static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--worker-fixture")
        {
            return RunWorkerFixture(args[1], args[2]);
        }

        if (args.Length == 3 && args[0] == "--fake-target")
        {
            return FakeTargetChecks.Run(args[1], args[2]);
        }

        Test("Login -> Simulation -> Control Center invokes each action once", delegate
        {
            var ui = Script(new UiSnapshot { LoginReady = true }, new UiSnapshot { SimulationReady = true }, new UiSnapshot { ControlCenter = true });
            var result = Run(ui);
            Require(result.Outcome == Outcome.Success && ui.Submissions == 1 && ui.Selections == 1);
        });
        Test("Delayed UI and network do not resubmit credentials", delegate
        {
            var ui = Script(new UiSnapshot(), new UiSnapshot { LoginReady = true }, new UiSnapshot { LoginReady = true }, new UiSnapshot(), new UiSnapshot { SimulationReady = true }, new UiSnapshot { ControlCenter = true });
            var result = Run(ui);
            Require(result.Outcome == Outcome.Success && ui.Submissions == 1 && ui.Selections == 1);
        });
        Test("Already-open main window does not read credentials or change mode", delegate
        {
            var ui = Script(new UiSnapshot { ControlCenter = true });
            var credentials = new FakeCredentials();
            var result = Run(ui, credentials, new FakeClock(), new FakeEvidence(), true, CancellationToken.None);
            Require(result.Outcome == Outcome.AlreadyOpen && credentials.Reads == 0 && ui.Submissions == 0 && ui.Selections == 0);
        });
        Test("Existing trading-mode window continues without credential read", delegate
        {
            var ui = Script(new UiSnapshot { SimulationReady = true }, new UiSnapshot { ControlCenter = true });
            var credentials = new FakeCredentials();
            var result = Run(ui, credentials, new FakeClock(), new FakeEvidence(), true, CancellationToken.None);
            Require(result.Outcome == Outcome.Success && credentials.Reads == 0 && ui.Selections == 1);
        });
        Test("Direct main window after launch is not Simulation success", delegate
        {
            var result = Run(Script(new UiSnapshot { ControlCenter = true }));
            Require(result.Outcome == Outcome.ModeNotConfirmed);
        });
        Test("Authentication error stops without retry", delegate
        {
            var ui = Script(new UiSnapshot { LoginReady = true }, new UiSnapshot { AuthenticationError = true, LoginReady = true });
            Require(Run(ui).Outcome == Outcome.AuthenticationFailed && ui.Submissions == 1 && ui.Selections == 0);
        });
        Test("Manual challenge blocks credential submission", delegate
        {
            var ui = Script(new UiSnapshot { ManualRequired = true, LoginReady = true });
            var credentials = new FakeCredentials();
            Require(Run(ui, credentials, new FakeClock(), new FakeEvidence(), false, CancellationToken.None).Outcome == Outcome.NeedsManual && credentials.Reads == 0);
        });
        Test("Ambiguous controls fail closed and preserve metadata", delegate
        {
            var ui = Script(new UiSnapshot { Ambiguous = true, LoginReady = true });
            var evidence = new FakeEvidence();
            Require(Run(ui, new FakeCredentials(), new FakeClock(), evidence, false, CancellationToken.None).Outcome == Outcome.UnsupportedUi && ui.Submissions == 0 && evidence.Events.Count >= 2);
        });
        Test("Missing credentials never submit", delegate
        {
            var ui = Script(new UiSnapshot { LoginReady = true });
            var credentials = new FakeCredentials { Missing = true };
            Require(Run(ui, credentials, new FakeClock(), new FakeEvidence(), false, CancellationToken.None).Outcome == Outcome.CredentialsMissing && ui.Submissions == 0);
        });
        Test("Timeout never resubmits or selects Simulation", delegate
        {
            var ui = Script(new UiSnapshot { LoginReady = true });
            Require(Run(ui).Outcome == Outcome.TimedOut && ui.Submissions == 1 && ui.Selections == 0);
        });
        Test("Cancellation between stages stops subsequent actions", delegate
        {
            var ui = Script(new UiSnapshot { LoginReady = true }, new UiSnapshot { SimulationReady = true });
            using (var source = new CancellationTokenSource())
            {
                var clock = new FakeClock { OnWait = delegate { source.Cancel(); } };
                Require(Run(ui, new FakeCredentials(), clock, new FakeEvidence(), false, source.Token).Outcome == Outcome.Cancelled && ui.Selections == 0);
            }
        });
        Test("Unwritable evidence prevents credential access", delegate
        {
            var ui = Script(new UiSnapshot { LoginReady = true });
            var credentials = new FakeCredentials();
            Require(Run(ui, credentials, new FakeClock(), new FakeEvidence { Writable = false }, false, CancellationToken.None).Outcome == Outcome.EvidenceUnavailable && credentials.Reads == 0);
        });
        Test("Briefly overlapping windows delay observation without any action", delegate
        {
            var ui = Script(new UiSnapshot { Ambiguous = true, LoginReady = true }, new UiSnapshot { LoginReady = true }, new UiSnapshot { SimulationReady = true }, new UiSnapshot { Ambiguous = true }, new UiSnapshot { ControlCenter = true });
            var evidence = new FakeEvidence();
            var result = Run(ui, new FakeCredentials(), new FakeClock(), evidence, false, CancellationToken.None);
            Require(result.Outcome == Outcome.Success && ui.Submissions == 1 && ui.Selections == 1);
        });
        Test("Lasting ambiguity after submission stops without a second submission", delegate
        {
            var ui = Script(new UiSnapshot { LoginReady = true }, new UiSnapshot { Ambiguous = true, LoginReady = true });
            var result = new LoginEngine(ui, new FakeCredentials(), new FakeClock(), new FakeEvidence(), TimeSpan.FromSeconds(120)).Run(false, CancellationToken.None);
            Require(result.Outcome == Outcome.UnsupportedUi && ui.Submissions == 1 && ui.Selections == 0);
        });
        Test("A sign-in window that stays ready after submission is reported without retry", delegate
        {
            var ui = Script(new UiSnapshot { LoginReady = true });
            var result = new LoginEngine(ui, new FakeCredentials(), new FakeClock(), new FakeEvidence(), TimeSpan.FromSeconds(120)).Run(false, CancellationToken.None);
            Require(result.Outcome == Outcome.LoginNotAccepted && ui.Submissions == 1 && ui.Selections == 0);
            foreach (string language in new[] { "en", "fa" })
            {
                Require(Messages.For(Outcome.LoginNotAccepted, language) != Messages.For(Outcome.UnexpectedFailure, language));
                Require(Messages.For(Outcome.MultipleInstances, language) != Messages.For(Outcome.UnexpectedFailure, language));
            }
        });
        Test("A sign-in window that clears before the grace period keeps waiting", delegate
        {
            var ui = Script(new UiSnapshot { LoginReady = true }, new UiSnapshot { LoginReady = true }, new UiSnapshot { LoginReady = true }, new UiSnapshot(), new UiSnapshot { LoginReady = true }, new UiSnapshot { LoginReady = true }, new UiSnapshot { SimulationReady = true }, new UiSnapshot { ControlCenter = true });
            var result = new LoginEngine(ui, new FakeCredentials(), new FakeClock(), new FakeEvidence(), TimeSpan.FromSeconds(120)).Run(false, CancellationToken.None);
            Require(result.Outcome == Outcome.Success && ui.Submissions == 1 && ui.Selections == 1);
        });
        Test("Grace periods expire only when a state lasts without clearing", delegate
        {
            var grace = new ProblemGrace(TimeSpan.FromSeconds(10));
            var start = new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc);
            Require(!grace.Expired(start) && !grace.Expired(start.AddSeconds(9)) && grace.Expired(start.AddSeconds(10)));
            grace.Clear();
            Require(!grace.Expired(start.AddSeconds(11)) && grace.Expired(start.AddSeconds(21)));
        });
        Test("An unexpected failure records its type and code without its message", delegate
        {
            var ui = Script(new UiSnapshot());
            ui.Crash = new InvalidCastException("PRIVATE-TEXT-DO-NOT-EXPORT");
            var evidence = new FakeEvidence();
            Require(Run(ui, new FakeCredentials(), new FakeClock(), evidence, false, CancellationToken.None).Outcome == Outcome.UnexpectedFailure && evidence.LastCause == ui.Crash);
            var record = new RunRecord();
            RunEvidence.ApplyCause(record, ui.Crash);
            using (var memory = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(RunRecord)).WriteObject(memory, record);
                string json = Encoding.UTF8.GetString(memory.ToArray());
                Require(record.CauseType == typeof(InvalidCastException).FullName && record.CauseHResult == ui.Crash.HResult && !json.Contains("PRIVATE-TEXT"));
            }
        });
        Test("Target process exit is terminal", delegate
        {
            var ui = Script(new UiSnapshot());
            ui.Failure = Outcome.ProcessExited;
            Require(Run(ui).Outcome == Outcome.ProcessExited);
        });
        Test("Coordinator excludes overlap and watcher retries; explicit retry is permitted", delegate
        {
            var gate = new AttemptGate();
            Require(gate.CanWatch("17:100"));
            Require(gate.Begin("17:100", false));
            Require(!gate.Begin("18:200", true));
            gate.End();
            Require(!gate.CanWatch("17:100"));
            Require(!gate.Begin("17:100", false));
            Require(gate.Begin("17:100", true));
            gate.End();
            Require(gate.Begin("17:101", false));
            gate.End();
        });
        Test("Settings reject unknown trading mode and invalid trigger values", delegate
        {
            bool live = false, mode = false;
            try
            {
                new Settings { TradingMode = "Unknown" }.Validate();
            }
            catch (InvalidDataException) { live = true; }
            try
            {
                new Settings { LaunchMethod = "Other" }.Validate();
            }
            catch (InvalidDataException) { mode = true; }
            Require(live && mode);
        });
        Test("Credential-shaped sentinel values do not enter engine evidence", delegate
        {
            var evidence = new FakeEvidence();
            var ui = Script(new UiSnapshot { LoginReady = true }, new UiSnapshot { SimulationReady = true }, new UiSnapshot { ControlCenter = true });
            Require(Run(ui, new FakeCredentials(), new FakeClock(), evidence, false, CancellationToken.None).Outcome == Outcome.Success);
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(List<EvidenceEvent>)).WriteObject(stream, evidence.Events);
                string json = Encoding.UTF8.GetString(stream.ToArray());
                Require(!json.Contains("TEST-USER") && !json.Contains("TEST-PASSWORD"));
            }
        });
        Test("Settings roundtrip preserves trigger and excludes credential fields", delegate
        {
            string folder = Path.Combine(Path.GetTempPath(), "AutoLoginTest-" + Guid.NewGuid().ToString("N"));
            string file = Path.Combine(folder, "settings.json");
            try
            {
                var settings = new Settings { LaunchMethod = "Watch" };
                JsonFiles.Write(file, settings);
                Settings restored = JsonFiles.Read<Settings>(file);
                restored.Validate();
                Require(restored.Mode == LaunchMode.Watch && restored.TradingMode == "Simulation" && !File.ReadAllText(file).ToLowerInvariant().Contains("password"));
            }
            finally
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }

                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder);
                }
            }
        });
        Test("Captured shortcut failure now completes through exact Custom ControlCenter metadata", delegate
        {
            Require(args.Length == 2);
            LoginResult result = Replay(Path.Combine(args[1], "shortcut-main-window.json"), false);
            Require(result.Outcome == Outcome.Success && result.LoginSubmissions == 1 && result.SimulationSelections == 1);
        });
        Test("Captured watcher failure now completes without changing login actions", delegate
        {
            Require(args.Length == 2);
            LoginResult result = Replay(Path.Combine(args[1], "watcher-main-window.json"), false);
            Require(result.Outcome == Outcome.Success && result.LoginSubmissions == 1 && result.SimulationSelections == 1);
        });
        Test("Captured already-open watcher trace remains AlreadyOpen, never a fresh Simulation claim", delegate
        {
            Require(args.Length == 2);
            LoginResult result = Replay(Path.Combine(args[1], "watcher-already-open.json"), true);
            Require(result.Outcome == Outcome.AlreadyOpen && result.LoginSubmissions == 0 && result.SimulationSelections == 0);
        });
        Test("ControlCenter Custom compatibility does not admit a different class or duplicate target", delegate
        {
            var control = new ControlEvidence { AutomationId = "ControlCenter", ClassName = "Other", ControlType = "ControlType.Custom", Enabled = true };
            UiSnapshot wrong = ControlContract.Evaluate(new List<ControlEvidence> { control });
            Require(wrong.Ambiguous && !wrong.ControlCenter);
            control.ClassName = "ControlCenter";
            UiSnapshot duplicated = ControlContract.Evaluate(new List<ControlEvidence> { control, control });
            Require(duplicated.Ambiguous && !duplicated.ControlCenter);
        });
        Test("Repeated disposal enters cleanup only once", delegate
        {
            var guard = new DisposeOnce();
            int cleanups = 0;
            for (int i = 0; i < 4; i++)
            {
                if (guard.TryBegin())
                {
                    cleanups++;
                }
            }

            Require(cleanups == 1);
        });
        Test("Legacy settings migrate language without changing Watch or Simulation", delegate
        {
            byte[] json = Encoding.UTF8.GetBytes("{\"SchemaVersion\":1,\"LaunchMethod\":\"Watch\",\"TradingMode\":\"Simulation\",\"NinjaTraderPath\":\"C:\\\\Program Files\\\\NinjaTrader 8\\\\bin\\\\NinjaTrader.exe\"}");
            using (var stream = new MemoryStream(json))
            {
                var settings = (Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(stream);
                settings.Validate();
                Require(settings.UiLanguage == "fa" && settings.Mode == LaunchMode.Watch && settings.TradingMode == "Simulation");
            }
        });
        Test("English preference is valid; unknown UI language fails explicitly", delegate
        {
            var settings = new Settings { UiLanguage = "en" };
            settings.Validate();
            Require(settings.UiLanguage == "en");
            bool rejected = false;
            try
            {
                new Settings { UiLanguage = "unknown" }.Validate();
            }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected);
        });
        Test("Application diagnostics exclude exception messages and secret-shaped input", delegate
        {
            AppFaultRecord record = AppFaults.Create("SaveSettings", new Exception("TEST-PASSWORD-DO-NOT-EXPORT"));
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(AppFaultRecord)).WriteObject(stream, record);
                string json = Encoding.UTF8.GetString(stream.ToArray());
                Require(!json.Contains("TEST-PASSWORD") && json.Contains("System.Exception") && json.Contains("SaveSettings"));
            }
        });
        Test("Header PNG owns valid pixels after its decoder has closed", delegate
        {
            using (Bitmap displayed = UiAssets.LoadBitmap("launch.png"))
            using (Stream resource = UiAssets.OpenImage("launch.png"))
            using (Image source = Image.FromStream(resource))
            using (var reference = new Bitmap(source))
            {
                Require(displayed.Width == 256 && displayed.Height == 256);
                foreach (int x in new[] { 0, 32, 64, 128, 192, 224, 255 })
                {
                    foreach (int y in new[] { 0, 32, 64, 128, 192, 224, 255 })
                    {
                        Require(displayed.GetPixel(x, y).ToArgb() == reference.GetPixel(x, y).ToArgb());
                    }
                }
            }
        });
        Test("Settings layout evidence exports geometry without text/value members", delegate
        {
            var layout = new SettingsLayoutRecord { Candidate = BuildInfo.Version, Language = "fa", Reason = "Shown", Dpi = 168, ClientWidth = 980, ClientHeight = 788 };
            layout.Controls.Add(new SettingsLayoutItem { Role = "passwordInput", ParentRole = "account", Kind = "InputFrame", X = 14, Y = 215, Width = 910, Height = 53, ParentWidth = 938, ParentHeight = 350, FontHeight = 22, Visible = true });
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(SettingsLayoutRecord)).WriteObject(stream, layout);
                string json = Encoding.UTF8.GetString(stream.ToArray());
                Require(json.Contains("passwordInput") && json.Contains("\"Width\":910") && !json.Contains("\"Text\"") && !json.Contains("\"Value\"") && !json.Contains("\"Password\"") && !json.Contains("\"Username\""));
            }
        });
        Test("Recorded font-mismatch geometry reproduces the rejected DPI mismatch", delegate
        {
            SettingsLayoutRecord recorded = JsonFiles.Read<SettingsLayoutRecord>(Path.Combine(args[1], "dpi-font-mismatch.json"));
            Require(recorded.Dpi == 96 && recorded.ClientWidth == 980);
            SettingsLayoutItem title = recorded.Controls.Find(delegate (SettingsLayoutItem item)
            {
                return item.Role == "heading";
            });
            SettingsLayoutItem label = recorded.Controls.Find(delegate (SettingsLayoutItem item)
            {
                return item.Role == "usernameLabel";
            });
            Require(title.FontHeight == 41 && title.Height == 25 && label.FontHeight > label.Height);
        });
        Test("Window-native DPI wins over stale managed 96-DPI state", delegate
        {
            Require(UiDpi.Select(168, 96) == 168 && UiDpi.Select(192, 144) == 192);
            Require(UiDpi.Select(0, 144) == 144 && UiDpi.Select(0, 0) == 96);
        });
        Test("Production geometry fits recorded font metrics and has no clipping/overlap at 100-200 percent in both languages", delegate
        {
            SettingsLayoutRecord recorded = JsonFiles.Read<SettingsLayoutRecord>(Path.Combine(args[1], "dpi-font-mismatch.json"));
            foreach (int dpi in new[] { 96, 120, 144, 168, 192 })
            {
                foreach (bool rtl in new[] { false, true })
                {
                    int width = (int)Math.Round(SettingsLayoutPlan.DesignWidth * dpi / 96.0), height = (int)Math.Round(SettingsLayoutPlan.DesignHeight * dpi / 96.0);
                    var plan = new SettingsLayoutPlan(width, height, dpi, rtl, 0);
                    foreach (var entry in plan.Parents)
                    {
                        Rectangle bounds = plan.Bounds[entry.Key];
                        Size parent = entry.Value == "settings" ? new Size(width, height) : plan.Bounds[entry.Value].Size;
                        Require(bounds.X >= 0 && bounds.Y >= 0 && bounds.Width > 0 && bounds.Height > 0 && bounds.Right <= parent.Width && bounds.Bottom <= parent.Height);
                        SettingsLayoutItem measured = recorded.Controls.Find(delegate (SettingsLayoutItem item)
                        {
                            return item.Role == entry.Key;
                        });
                        if (measured != null && (measured.Kind == "Label" || measured.Kind == "Button" || measured.Kind == "RadioButton" || measured.Kind == "ComboBox"))
                        {
                            Require(bounds.Height >= (int)Math.Ceiling(measured.FontHeight * dpi / 168.0));
                        }

                        foreach (var other in plan.Parents)
                        {
                            if (String.CompareOrdinal(entry.Key, other.Key) < 0 && entry.Value == other.Value)
                            {
                                Require(!bounds.IntersectsWith(plan.Bounds[other.Key]));
                            }
                        }
                    }
                    Rectangle user = plan.Bounds["usernameInput"], password = plan.Bounds["passwordInput"];
                    Require(user.X == password.X && user.Width == password.Width && user.Bottom < password.Top);
                }
            }
        });
        Test("Navigation options stay opposite the tabs or stack without fractional-DPI overflow", delegate
        {
            foreach (int dpi in new[] { 96, 120, 144, 168, 192, 216, 240, 288 })
            {
                foreach (int logicalWidth in new[] { 220, 300, 380, 419, 420, 480, 640 })
                {
                    int width = (int)Math.Round(logicalWidth * dpi / 96.0);
                    var left = new SettingsLayoutPlan(width, 1000, dpi, false, 0);
                    var right = new SettingsLayoutPlan(width, 1000, dpi, true, 0);
                    foreach (string role in new[] { "settingsTab", "aboutTab", "options" })
                    {
                        Rectangle ltr = left.Bounds[role], rtl = right.Bounds[role];
                        Require(ltr.Top >= 0 && ltr.Bottom <= left.Bounds["navigation"].Height);
                        Require(ltr.Left >= 0 && ltr.Right <= left.Bounds["navigation"].Width);
                        Require(rtl.Left == right.Bounds["navigation"].Width - ltr.Right && rtl.Size == ltr.Size);
                    }
                    Rectangle options = left.Bounds["options"], about = left.Bounds["aboutTab"];
                    Require(!options.IntersectsWith(about));
                    Require(options.Top >= about.Bottom || options.Left > about.Right);
                }
            }
        });
        Test("Drop-down bounds remain inside the window at either edge and negative monitor origins", delegate
        {
            foreach (Rectangle window in new[] { new Rectangle(0, 0, 220, 120), new Rectangle(400, 200, 840, 960), new Rectangle(-1600, -200, 800, 600) })
            {
                foreach (Point origin in new[] { new Point(window.Left - 180, window.Top + 80), new Point(window.Right - 10, window.Top), new Point(window.Left, window.Bottom - 10) })
                {
                    foreach (Size size in new[] { new Size(180, 100), new Size(1500, 1000) })
                    {
                        Rectangle actual = PopupPlacement.Constrain(new Rectangle(origin, size), window);
                        Require(window.Contains(actual) && actual.Width > 0 && actual.Height > 0);
                        Require(actual.Width == Math.Min(size.Width, window.Width) && actual.Height == Math.Min(size.Height, window.Height));
                    }
                }
            }
        });
        Test("In-window menu placement opens inward and remains inside the client area in both languages", delegate
        {
            foreach (int dpi in new[] { 96, 120, 144, 168, 192, 216, 240, 288 })
            {
                foreach (Size logical in new[] { new Size(220, 120), new Size(480, 528), new Size(800, 600) })
                {
                    var client = new Size(logical.Width * dpi / 96, logical.Height * dpi / 96);
                    int inset = 8 * dpi / 96;
                    foreach (bool rtl in new[] { false, true })
                    {
                        var anchor = new Rectangle(rtl ? inset : client.Width - inset - 96 * dpi / 96, 68 * dpi / 96, 96 * dpi / 96, 30 * dpi / 96);
                        Rectangle menu = PopupPlacement.InWindow(client, anchor, new Size(180 * dpi / 96, 116 * dpi / 96), inset, rtl);
                        Require(new Rectangle(Point.Empty, client).Contains(menu));
                        Require(menu.Left >= inset && menu.Right <= client.Width - inset && menu.Top >= inset && menu.Bottom <= client.Height - inset);
                        if (client.Height > 200 * dpi / 96)
                        {
                            Require(menu.Top >= anchor.Bottom);
                            Require(rtl ? menu.Left == anchor.Left : menu.Right == anchor.Right);
                        }
                    }
                }
            }
            Require(typeof(Panel).IsAssignableFrom(typeof(InWindowOptions)) && !typeof(ToolStripDropDown).IsAssignableFrom(typeof(InWindowOptions)));
        });
        Test("About cards stay inside the page, mirror for Persian and never overlap", delegate
        {
            foreach (int dpi in new[] { 96, 120, 144, 168, 192, 216, 240, 288 })
            {
                foreach (int logical in new[] { 200, 340, 456, 800 })
                {
                    int width = (int)Math.Round(logical * dpi / 96.0);
                    foreach (string result in new[] { "updateStatus", "releaseLink" })
                    {
                        var shown = new[] { result, "supportStatus" };
                        Func<string, int, int> height = (role, available) => 30 * dpi / 96;
                        Func<string, int> button = role => 150 * dpi / 96;
                        var english = new AboutLayoutPlan(width, dpi, false, height, button, shown);
                        var persian = new AboutLayoutPlan(width, dpi, true, height, button, shown);
                        foreach (var item in english.Bounds)
                        {
                            Rectangle left = item.Value, right = persian.Bounds[item.Key];
                            Require(left.Left >= 0 && left.Right <= width && left.Top >= 0 && left.Width > 0 && left.Height > 0);
                            Require(left.Top == right.Top && left.Size == right.Size && left.Left == width - right.Right);
                            foreach (var other in english.Bounds)
                            {
                                Require(other.Key == item.Key || english.Parents[other.Key] != english.Parents[item.Key] || !left.IntersectsWith(other.Value));
                            }
                        }
                        Require(english.Bounds["supportCard"].Bottom == english.Height);
                        // The button starts where the language starts and its result follows on the same row.
                        Rectangle check = english.Bounds["checkUpdates"], outcome = english.Bounds[result], card = english.Bounds["versionCard"];
                        Require(check.Left == english.Bounds["versionHeading"].Left && check.Right < card.Right);
                        Require(persian.Bounds["checkUpdates"].Right == persian.Bounds["versionHeading"].Right);
                        Require(english.Compact || (outcome.Top == check.Top && outcome.Left > check.Right));
                    }
                }
            }
        });
        Test("A new version is one sentence that opens its release page", delegate
        {
            foreach (string language in new[] { "en", "fa" })
            {
                using (var view = new AboutView(language))
                {
                    IntPtr handle = view.Handle;
                    var page = new Uri("https://github.com/" + ProductInfo.RepositoryOwner + "/" + ProductInfo.RepositoryName + "/releases/tag/v0.2.0");
                    Require(ProductInfo.IsReleasePage(page));
                    view.ShowNewVersion("0.2.0", page);
                    AboutLayoutPlan plan = view.PlanFor((int)Math.Round(456 * UiDpi.Get(view) / 96.0), UiDpi.Get(view));
                    Require(plan.Bounds.ContainsKey("releaseLink") && !plan.Bounds.ContainsKey("updateStatus"));
                    Require(plan.Bounds["releaseLink"].Top == plan.Bounds["checkUpdates"].Top);
                    var link = (AddressLink)view.Controls.Find("releaseLink", true)[0];
                    Require(link.Target == page && link.Text.Contains("0.2.0") && !link.Text.Contains("http"));
                }
            }
        });
        Test("A link shows exactly the address it opens", delegate
        {
            using (var source = new AddressLink(new Uri(ProductInfo.SourceRepositoryUrl)))
            using (var profile = new AddressLink(new Uri(ProductInfo.GitHubProfileUrl)))
            using (var mail = new AddressLink(new Uri("mailto:" + ProductInfo.ContactEmail)))
            {
                Require(source.Text == "https://github.com/MohZeh/Auto-Login-for-NinjaTrader" && source.Text == source.Target.AbsoluteUri);
                Require(profile.Text == "https://github.com/MohZeh" && mail.Text == ProductInfo.ContactEmail);
            }
        });
        Test("The About page fits its window at the design size and states it is unofficial", delegate
        {
            foreach (string language in new[] { "en", "fa" })
            {
                foreach (string theme in new[] { "Dark", "Light" })
                {
                    UiAssets.Palette = UiPalette.For(theme);
                    using (var view = new AboutView(language))
                    {
                        IntPtr handle = view.Handle;
                        int dpi = UiDpi.Get(view);
                        var page = new SettingsLayoutPlan((int)Math.Round(SettingsLayoutPlan.DesignWidth * dpi / 96.0), (int)Math.Round(SettingsLayoutPlan.DesignHeight * dpi / 96.0), dpi, language == "fa", 0);
                        int available = page.Bounds["footer"].Top - page.Px(6) - page.Bounds["bodyViewport"].Top - page.Bounds["header"].Height - page.Px(6);
                        AboutLayoutPlan plan = view.PlanFor(page.Bounds["bodyViewport"].Width, dpi);
                        Require(plan.Height <= available);
                    }
                }
            }
            UiAssets.Palette = UiPalette.For("Dark");
            Require(UiText.LegalNotice("en").Contains("Not affiliated with") && UiText.LegalNotice("en").Contains("NinjaTrader, LLC"));
            Require(UiText.LegalNotice("fa").Contains("غیررسمی") && UiText.LegalNotice("fa").Contains("NinjaTrader, LLC"));
            // The duty concerns NinjaTrader's license, not this app's own MIT license.
            Require(UiText.LegalNotice("en").Contains("NinjaTrader's license") && UiText.LegalNotice("fa").Contains("قوانین مجوز نینجاتریدر"));
        });
        Test("A support summary never contains account details or file paths", delegate
        {
            string folder = Path.Combine(Path.GetTempPath(), "AutoLogin-summary-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string empty = SupportSummary.Build(folder, "0.1.0", "Windows");
                Require(empty.Contains(ProductInfo.EnglishName) && empty.Contains("none"));
                JsonFiles.Write(Path.Combine(folder, "run-20261105-001118-622-ab767e4a.json"), new RunRecord
                {
                    StartedUtc = "2026-11-05T00:11:18Z",
                    Trigger = "Watcher",
                    Outcome = "UnexpectedFailure",
                    NinjaTraderPath = @"D:\PrivateName\NinjaTrader.exe",
                    NinjaTraderVersion = "8.1.8.4",
                    CauseType = "System.TimeoutException",
                    CauseHResult = 5,
                    LoginSubmissions = 1
                });
                JsonFiles.Write(Path.Combine(folder, "app-fault-1.json"), new AppFaultRecord { Utc = "2026-11-05T00:12:00Z", Phase = "Worker", ExceptionType = "System.IO.IOException", HResult = 7, Frames = new List<string> { @"D:\PrivateName\file.cs" } });
                string text = SupportSummary.Build(folder, "0.1.0", "Windows 11");
                Require(text.Contains("8.1.8.4") && text.Contains("UnexpectedFailure") && text.Contains("System.TimeoutException") && text.Contains("System.IO.IOException"));
                Require(!text.Contains("PrivateName") && !text.Contains(@"D:\") && !text.Contains(folder));
                File.WriteAllText(Path.Combine(folder, "run-20261106-000000-000-00000000.json"), "not json");
                Require(SupportSummary.Build(folder, "0.1.0", "Windows 11").Contains("UnexpectedFailure"));
            }
            finally { Directory.Delete(folder, true); }
        });
        Test("A rejected password is not sent again until the owner decides", delegate
        {
            bool rejected = true;
            var credentials = new FakeCredentials();
            var guarded = new UnrejectedCredentials(() => rejected, credentials);
            var ui = Script(new UiSnapshot { LoginReady = true });
            LoginResult result = new LoginEngine(ui, guarded, new FakeClock(), new FakeEvidence(), TimeSpan.FromSeconds(4)).Run(false, CancellationToken.None);
            Require(result.Outcome == Outcome.CredentialsRejected && result.LoginSubmissions == 0 && credentials.Reads == 0 && ui.Submissions == 0);
            rejected = false;
            using (Secret secret = guarded.Read())
            {
                Require(secret.Valid && credentials.Reads == 1);
            }
            foreach (string language in new[] { "en", "fa" })
            {
                Require(Messages.For(Outcome.CredentialsRejected, language) != Messages.For(Outcome.UnexpectedFailure, language));
            }
            string folder = Path.Combine(Path.GetTempPath(), "AutoLogin-rejected-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string path = Path.Combine(folder, "settings.json");
                JsonFiles.Write(path, new Settings { CredentialsRejected = true });
                Require(JsonFiles.Read<Settings>(path).CredentialsRejected);
                JsonFiles.Write(path, new Settings());
                Require(!JsonFiles.Read<Settings>(path).CredentialsRejected);
            }
            finally { Directory.Delete(folder, true); }
        });
        Test("A stop keeps the type of the error behind it for diagnosis", delegate
        {
            var ui = Script(new UiSnapshot { LoginReady = true });
            ui.Crash = new LoginProblem(Outcome.ProcessExited, new ArgumentException("duplicate window"));
            var evidence = new FakeEvidence();
            LoginResult result = new LoginEngine(ui, new FakeCredentials(), new FakeClock(), evidence, TimeSpan.FromSeconds(4)).Run(false, CancellationToken.None);
            Require(result.Outcome == Outcome.ProcessExited && evidence.LastCause is ArgumentException && result.LoginSubmissions == 0);
            var plain = Script(new UiSnapshot { LoginReady = true });
            plain.Crash = new LoginProblem(Outcome.ProcessExited);
            var quiet = new FakeEvidence();
            new LoginEngine(plain, new FakeCredentials(), new FakeClock(), quiet, TimeSpan.FromSeconds(4)).Run(false, CancellationToken.None);
            Require(quiet.LastCause == null);
        });
        Test("Watching one process notices its end and refuses a different process", delegate
        {
            using (System.Diagnostics.Process current = System.Diagnostics.Process.GetCurrentProcess())
            {
                long started = current.StartTime.ToUniversalTime().Ticks;
                using (ProcessWatch watch = ProcessWatch.Open(new ProcessIdentity { Id = current.Id, StartedUtcTicks = started }))
                {
                    Require(watch != null && !watch.HasEnded);
                }
                Require(ProcessWatch.Open(new ProcessIdentity { Id = current.Id, StartedUtcTicks = started + 1 }) == null);
            }
            Require(ProcessWatch.Open(new ProcessIdentity { Id = -1, StartedUtcTicks = 1 }) == null);
            var start = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c ping -n 2 127.0.0.1 >nul") { UseShellExecute = false, CreateNoWindow = true };
            using (System.Diagnostics.Process child = System.Diagnostics.Process.Start(start))
            using (ProcessWatch watch = ProcessWatch.Open(new ProcessIdentity { Id = child.Id, StartedUtcTicks = child.StartTime.ToUniversalTime().Ticks }))
            {
                Require(watch != null && !watch.HasEnded);
                child.WaitForExit();
                Require(watch.HasEnded);
            }
        });
        Test("A fresh copy opens only when the copy found was already exiting and nothing was sent", delegate
        {
            TimeSpan quick = TimeSpan.FromSeconds(2);
            var exited = new LoginResult { Outcome = Outcome.ProcessExited };
            Require(RelaunchPolicy.ShouldRelaunch(true, true, exited, quick));
            Require(!RelaunchPolicy.ShouldRelaunch(false, true, exited, quick));
            Require(!RelaunchPolicy.ShouldRelaunch(true, false, exited, quick));
            Require(!RelaunchPolicy.ShouldRelaunch(true, true, exited, RelaunchPolicy.ExitingWindow + TimeSpan.FromSeconds(1)));
            Require(!RelaunchPolicy.ShouldRelaunch(true, true, new LoginResult { Outcome = Outcome.ProcessExited, LoginSubmissions = 1 }, quick));
            Require(!RelaunchPolicy.ShouldRelaunch(true, true, new LoginResult { Outcome = Outcome.ProcessExited, SimulationSelections = 1 }, quick));
            Require(!RelaunchPolicy.ShouldRelaunch(true, true, new LoginResult { Outcome = Outcome.ProcessExited, LiveSelections = 1 }, quick));
            Require(!RelaunchPolicy.ShouldRelaunch(true, true, new LoginResult { Outcome = Outcome.ProcessExited, ActionCompletionUncertain = true }, quick));
            Require(!RelaunchPolicy.ShouldRelaunch(true, true, new LoginResult { Outcome = Outcome.Cancelled }, quick));
        });
        Test("A process without modules counts as still settling, never as a failed sign-in", delegate
        {
            Require(ProcessIdentity.ImagePathOf(() => @"C:\NT\NinjaTrader.exe") == @"C:\NT\NinjaTrader.exe");
            foreach (Func<string> read in new Func<string>[] { () => { throw new NullReferenceException(); }, () => null })
            {
                bool unsettled = false;
                try
                {
                    ProcessIdentity.ImagePathOf(read);
                }
                catch (System.ComponentModel.Win32Exception error) { unsettled = error.NativeErrorCode == 299; }
                Require(unsettled);
            }
        });
        Test("A refused login stays visible with a way to remove it, and its saved password is kept", delegate
        {
            const System.Reflection.BindingFlags Hidden = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            foreach (string language in new[] { "en", "fa" })
            {
                Require(UiText.RejectedHint(language, true) != UiText.RejectedHint(language, false));
                Require(ConfirmationContent.RetryRejected(language, true).Lead != ConfirmationContent.RetryRejected(language, false).Lead);
                foreach (bool uncertain in new[] { false, true })
                {
                    var settings = new Settings { UiLanguage = language, NinjaTraderPath = @"C:\NT\NinjaTrader.exe", CredentialsRejected = true, RejectionUncertain = uncertain };
                    var owner = (LoginContext)FormatterServices.GetUninitializedObject(typeof(LoginContext));
                    GC.SuppressFinalize(owner);
                    typeof(LoginContext).GetField("settings", Hidden).SetValue(owner, settings);
                    typeof(LoginContext).GetField("statusKey", Hidden).SetValue(owner, "ready");
                    typeof(LoginContext).GetField("pause", Hidden).SetValue(owner, new MonitoringPause());
                    var credentials = new FakeSettingsCredentials();
                    using (var form = new SettingsForm(owner, settings, credentials))
                    {
                        typeof(SettingsForm).GetMethod("UpdateEye", Hidden).Invoke(form, null);
                        Control hint = form.Controls.Find("credentialHint", true)[0];
                        Require(hint.Text == UiText.RejectedHint(language, uncertain) && hint.ForeColor == UiAssets.Error);
                        Require(form.Controls.Find("removeRejected", true)[0].Text.Length > 0);
                        settings.CredentialsRejected = settings.RejectionUncertain = false;
                        typeof(SettingsForm).GetMethod("UpdateEye", Hidden).Invoke(form, null);
                        Require(hint.Text != UiText.RejectedHint(language, uncertain) && hint.ForeColor == UiAssets.Muted);
                    }
                }
            }
        });
        Test("Neutral confirmations default to the safe choice and carry no Live badge", delegate
        {
            foreach (string language in new[] { "en", "fa" })
            {
                foreach (ConfirmationContent content in new[] { ConfirmationContent.DiscardChanges(language), ConfirmationContent.WatchWhileOpen(language), ConfirmationContent.RetryRejected(language), ConfirmationContent.RetryRejected(language, true), ConfirmationContent.RemoveLogin(language) })
                {
                    Require(!content.Live && content.Untested == null);
                    using (var dialog = new ConfirmationDialog(language, content))
                    {
                        Require(dialog.AcceptButton == dialog.CancelAction && dialog.CancelButton == dialog.CancelAction && dialog.ConfirmAction.Kind == ActionKind.Primary);
                    }
                }
            }
        });
        Test("User text uses one name for each idea", delegate
        {
            foreach (Outcome outcome in Enum.GetValues(typeof(Outcome)))
            {
                string persian = Messages.For(outcome, "fa"), english = Messages.For(outcome, "en");
                Require(!persian.Contains("شواهد") && !persian.Contains("ابزار") && !english.Contains("helper"));
                Require(!System.Text.RegularExpressions.Regex.IsMatch(persian, "نینجا(?!تریدر)"));
            }
        });
        Test("Framework 4.8 and single-source PerMonitorV2 configuration are explicit", delegate
        {
            var target = (TargetFrameworkAttribute)Attribute.GetCustomAttribute(typeof(SettingsLayoutPlan).Assembly, typeof(TargetFrameworkAttribute));
            Require(target != null && target.FrameworkName == ".NETFramework,Version=v4.8");
            var config = new XmlDocument { XmlResolver = null };
            config.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NinjaTraderAutoLogin.exe.config"));
            XmlNode option = config.SelectSingleNode("/configuration/System.Windows.Forms.ApplicationConfigurationSection/add[@key='DpiAwareness']");
            Require(option != null && option.Attributes["value"].Value == "PerMonitorV2");
            var manifest = new XmlDocument { XmlResolver = null };
            manifest.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-inputs", "app.manifest"));
            Require(manifest.SelectNodes("//*[local-name()='dpiAware' or local-name()='dpiAwareness']").Count == 0);
        });
        Test("Live request selects Live once with both mode buttons available", delegate
        {
            var ui = Script(new UiSnapshot { LoginReady = true }, new UiSnapshot { SimulationReady = true, LiveReady = true }, new UiSnapshot { ControlCenter = true });
            var evidence = new FakeEvidence();
            var result = new LoginEngine(ui, new FakeCredentials(), new FakeClock(), evidence, TimeSpan.FromSeconds(4), TradingMode.Live).Run(false, CancellationToken.None);
            Require(result.Outcome == Outcome.Success && result.RequestedMode == TradingMode.Live && result.LiveSelections == 1 && result.SimulationSelections == 0 && ui.SelectedModes.Count == 1 && ui.SelectedModes[0] == TradingMode.Live && evidence.CompletedLive == 1 && evidence.CompletedSimulation == 0);
        });
        Test("Simulation remains the default when Live is also available", delegate
        {
            var ui = Script(new UiSnapshot { SimulationReady = true, LiveReady = true }, new UiSnapshot { ControlCenter = true });
            var result = Run(ui);
            Require(result.Outcome == Outcome.Success && result.SimulationSelections == 1 && result.LiveSelections == 0 && ui.SelectedModes[0] == TradingMode.Simulation);
        });
        Test("Unavailable Live never falls back to Simulation or retries credentials", delegate
        {
            var ui = Script(new UiSnapshot { LoginReady = true }, new UiSnapshot { SimulationReady = true });
            var result = new LoginEngine(ui, new FakeCredentials(), new FakeClock(), new FakeEvidence(), TimeSpan.FromSeconds(4), TradingMode.Live).Run(false, CancellationToken.None);
            Require(result.Outcome == Outcome.TimedOut && result.LoginSubmissions == 1 && result.LiveSelections == 0 && result.SimulationSelections == 0 && ui.Selections == 0);
        });
        Test("A Live button offering account creation is never invoked", delegate
        {
            var ui = Script(new UiSnapshot { LoginReady = true }, new UiSnapshot { SimulationReady = true, LiveReady = true, LiveAccountUnavailable = true }, new UiSnapshot { ControlCenter = true });
            var evidence = new FakeEvidence();
            var result = new LoginEngine(ui, new FakeCredentials(), new FakeClock(), evidence, TimeSpan.FromSeconds(4), TradingMode.Live).Run(false, CancellationToken.None);
            Require(result.Outcome == Outcome.LiveAccountUnavailable && result.LoginSubmissions == 1 && result.LiveSelections == 0 && result.SimulationSelections == 0 && ui.Selections == 0);
            Require(evidence.CompletedLive == 0 && !evidence.Events.Exists(item => item.Action == "BeforeLive" || item.Action == "BeforeSimulation"));
            foreach (string language in new[] { "en", "fa" })
            {
                Require(Messages.For(Outcome.LiveAccountUnavailable, language) != Messages.For(Outcome.UnexpectedFailure, language));
            }
        });
        Test("A Simulation request ignores the Live account-creation signal", delegate
        {
            var ui = Script(new UiSnapshot { SimulationReady = true, LiveReady = true, LiveAccountUnavailable = true }, new UiSnapshot { ControlCenter = true });
            var result = Run(ui);
            Require(result.Outcome == Outcome.Success && result.SimulationSelections == 1 && result.LiveSelections == 0);
        });
        Test("Each move into Live asks for one confirmation and ordinary saves do not", delegate
        {
            TradingMode sim = TradingMode.Simulation, live = TradingMode.Live;
            LaunchMode shortcut = LaunchMode.Shortcut, watch = LaunchMode.Watch;
            Require(LiveConfirmationPolicy.Required(sim, shortcut, live, shortcut) == LiveConfirmation.EnterLive);
            Require(LiveConfirmationPolicy.Required(sim, watch, live, shortcut) == LiveConfirmation.EnterLive);
            Require(LiveConfirmationPolicy.Required(sim, shortcut, live, watch) == LiveConfirmation.EnterLiveWithWatch);
            Require(LiveConfirmationPolicy.Required(sim, watch, live, watch) == LiveConfirmation.EnterLiveWithWatch);
            Require(LiveConfirmationPolicy.Required(live, shortcut, live, watch) == LiveConfirmation.WatchWhileLive);
            Require(LiveConfirmationPolicy.Required(live, watch, live, watch) == LiveConfirmation.None);
            Require(LiveConfirmationPolicy.Required(live, shortcut, live, shortcut) == LiveConfirmation.None);
            Require(LiveConfirmationPolicy.Required(live, watch, live, shortcut) == LiveConfirmation.None);
            Require(LiveConfirmationPolicy.Required(live, watch, sim, watch) == LiveConfirmation.None);
            Require(LiveConfirmationPolicy.Required(sim, shortcut, sim, watch) == LiveConfirmation.None);
        });
        Test("Live confirmation content names the watch risk only when it applies", delegate
        {
            foreach (string language in new[] { "en", "fa" })
            {
                ConfirmationContent enter = ConfirmationContent.For(language, LiveConfirmation.EnterLive);
                ConfirmationContent both = ConfirmationContent.For(language, LiveConfirmation.EnterLiveWithWatch);
                ConfirmationContent watch = ConfirmationContent.For(language, LiveConfirmation.WatchWhileLive);
                Require(enter.WatchNote == null && both.WatchNote != null && watch.WatchNote == null);
                Require(enter.Lead != watch.Lead && enter.Confirm != watch.Confirm && enter.Title != watch.Title);
            }
        });
        Test("Saving clears a fault pause but keeps a pause the user chose", delegate
        {
            var pause = new MonitoringPause();
            pause.Fault();
            pause.Saved(LaunchMode.Watch);
            Require(!pause.Paused);
            pause.Toggle();
            pause.Saved(LaunchMode.Watch);
            Require(pause.Paused);
            pause.Toggle();
            Require(!pause.Paused);
            pause.Toggle();
            pause.Saved(LaunchMode.Shortcut);
            Require(!pause.Paused);
            pause.Saved(LaunchMode.Watch);
            Require(!pause.Paused);
        });
        Test("Status notices never contradict the saved state or a running operation", delegate
        {
            Require(SettingsNoticePolicy.Select(true, true, true, false, false) == SettingsNotice.None);
            Require(SettingsNoticePolicy.Select(false, true, true, false, false) == SettingsNotice.Unsaved);
            Require(SettingsNoticePolicy.Select(false, false, true, false, true) == SettingsNotice.MissingCredentials);
            Require(SettingsNoticePolicy.Select(false, false, true, true, false) == SettingsNotice.MissingExecutable);
            Require(SettingsNoticePolicy.Select(false, false, true, true, true) == SettingsNotice.None);
            Require(SettingsNoticePolicy.Select(false, false, false, false, false) == SettingsNotice.None);
            var saved = new Settings { LaunchMethod = "Watch", TradingMode = "Simulation", NinjaTraderPath = @"C:\NT\NinjaTrader.exe" };
            Require(!SettingsNoticePolicy.Differs(saved, @" c:\nt\ninjatrader.exe ", LaunchMode.Watch, TradingMode.Simulation, " user ", "user", false));
            Require(SettingsNoticePolicy.Differs(saved, saved.NinjaTraderPath, LaunchMode.Watch, TradingMode.Live, "user", "user", false));
            Require(SettingsNoticePolicy.Differs(saved, saved.NinjaTraderPath, LaunchMode.Shortcut, TradingMode.Simulation, "user", "user", false));
            Require(SettingsNoticePolicy.Differs(saved, @"C:\Other\NinjaTrader.exe", LaunchMode.Watch, TradingMode.Simulation, "user", "user", false));
            Require(SettingsNoticePolicy.Differs(saved, saved.NinjaTraderPath, LaunchMode.Watch, TradingMode.Simulation, "other", "user", false));
            Require(SettingsNoticePolicy.Differs(saved, saved.NinjaTraderPath, LaunchMode.Watch, TradingMode.Simulation, "user", "user", true));
            foreach (string language in new[] { "en", "fa" })
            {
                foreach (SettingsNotice notice in new[] { SettingsNotice.Unsaved, SettingsNotice.MissingCredentials, SettingsNotice.MissingExecutable })
                {
                    Require(!String.IsNullOrEmpty(UiText.Notice(notice, language, TradingMode.Simulation)));
                }
                Require(UiText.Notice(SettingsNotice.None, language, TradingMode.Simulation) == null);
            }
        });
        Test("Evidence retention keeps the newest records and only its own files", delegate
        {
            string folder = Path.Combine(Path.GetTempPath(), "AutoLoginRetention-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                for (int index = 0; index < EvidenceRetention.RunLimit + 5; index++)
                {
                    File.WriteAllText(Path.Combine(folder, "run-" + (1000 + index) + ".json"), "{}");
                }
                for (int index = 0; index < EvidenceRetention.FaultLimit + 3; index++)
                {
                    File.WriteAllText(Path.Combine(folder, "app-fault-" + (1000 + index) + ".json"), "{}");
                }
                foreach (string name in new[] { "settings-layout-a.json", "surface-layout-b.json", "unrelated.json", "run-notes.txt" })
                {
                    File.WriteAllText(Path.Combine(folder, name), "{}");
                }
                EvidenceRetention.Prune(folder, true);
                Require(File.Exists(Path.Combine(folder, "settings-layout-a.json")));
                EvidenceRetention.Prune(folder, false);
                Require(Directory.GetFiles(folder, "run-*.json").Length == EvidenceRetention.RunLimit && Directory.GetFiles(folder, "app-fault-*.json").Length == EvidenceRetention.FaultLimit);
                Require(!File.Exists(Path.Combine(folder, "run-1000.json")) && File.Exists(Path.Combine(folder, "run-" + (1000 + EvidenceRetention.RunLimit + 4) + ".json")));
                Require(Directory.GetFiles(folder, "*-layout-*.json").Length == 0 && File.Exists(Path.Combine(folder, "unrelated.json")) && File.Exists(Path.Combine(folder, "run-notes.txt")));
            }
            finally { Directory.Delete(folder, true); }
        });
        Test("Tab order follows the visible order of the settings page", delegate
        {
            var settings = new Settings { UiLanguage = "en" };
            var owner = (LoginContext)FormatterServices.GetUninitializedObject(typeof(LoginContext));
            GC.SuppressFinalize(owner);
            typeof(LoginContext).GetField("settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(owner, settings);
            typeof(LoginContext).GetField("statusKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(owner, "ready");
            typeof(LoginContext).GetField("pause", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(owner, new MonitoringPause());
            using (var form = new SettingsForm(owner, settings, new FakeSettingsCredentials()))
            {
                string[] roles = { "header", "account", "startMethod", "application", "statusRow" };
                for (int index = 1; index < roles.Length; index++)
                {
                    Require(form.Controls.Find(roles[index - 1], true)[0].TabIndex < form.Controls.Find(roles[index], true)[0].TabIndex);
                }
            }
            UiAssets.Palette = UiPalette.For("Dark");
        });
        Test("Sign-in progress window never takes focus and fits its text", delegate
        {
            foreach (string language in new[] { "en", "fa" })
            {
                foreach (TradingMode mode in new[] { TradingMode.Simulation, TradingMode.Live })
                {
                    using (var window = new LoginProgressWindow(language, mode))
                    {
                        var showWithoutActivation = (bool)typeof(Form).GetProperty("ShowWithoutActivation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(window, null);
                        Require(showWithoutActivation && !window.ShowInTaskbar);
                        foreach (string stage in new[] { null, Stage.WaitingForLogin.ToString(), Stage.WaitingForTradingMode.ToString(), Stage.WaitingForControlCenter.ToString() })
                        {
                            string text = UiText.ProgressStage(language, stage, mode);
                            Require(text.Length > 0);
                            window.SetStage(text);
                            Require(window.CancelAction.Bottom <= window.ClientSize.Height);
                        }
                        Rectangle work = Screen.PrimaryScreen.WorkingArea;
                        Require(window.Right <= work.Right && window.Bottom <= work.Bottom);
                        Require(window.Left > work.Left + work.Width / 2 - window.Width && window.Top > work.Top + work.Height / 2);
                        window.Finish(Messages.For(Outcome.AlreadyOpen, language, mode), 60000, null);
                        Require(!window.CancelAction.Visible || !window.CancelAction.IsHandleCreated);
                        Require(window.Right <= work.Right && window.Bottom <= work.Bottom);
                    }
                }
                Require(UiText.ShortcutAction(language, ShortcutState.Missing) != UiText.ShortcutAction(language, ShortcutState.Ready));
            }
        });
        Test("Footer commands follow the reading direction and keep their order", delegate
        {
            foreach (int dpi in new[] { 96, 120, 144, 168, 192 })
            {
                int width = (int)Math.Round(SettingsLayoutPlan.DesignWidth * dpi / 96.0), height = (int)Math.Round(SettingsLayoutPlan.DesignHeight * dpi / 96.0);
                var left = new SettingsLayoutPlan(width, height, dpi, false, 0);
                var right = new SettingsLayoutPlan(width, height, dpi, true, 0);
                Require(left.Bounds["saveSettings"].Left > left.Bounds["saveAndLaunch"].Left && left.Bounds["saveAndLaunch"].Left > left.Bounds["closeSettings"].Left);
                Require(right.Bounds["saveSettings"].Left < right.Bounds["saveAndLaunch"].Left && right.Bounds["saveAndLaunch"].Left < right.Bounds["closeSettings"].Left);
                foreach (string role in new[] { "saveSettings", "saveAndLaunch", "closeSettings" })
                {
                    Require(left.Bounds[role].Size == right.Bounds[role].Size && left.Bounds[role].Top == right.Bounds[role].Top);
                }
            }
        });
        Test("Settings are grouped into account, login behavior and application", delegate
        {
            foreach (bool rtl in new[] { false, true })
            {
                foreach (int width in new[] { 480, 340 })
                {
                    var plan = new SettingsLayoutPlan(width, 1000, 96, rtl, 0);
                    Require(plan.Parents["usernameInput"] == "account" && plan.Parents["passwordInput"] == "account");
                    Require(plan.Parents["tradingModes"] == "startMethod" && plan.Parents["shortcutOption"] == "startMethod");
                    Require(plan.Parents["pathInput"] == "application" && plan.Parents["targetStatus"] == "application");
                    Require(plan.Bounds["account"].Bottom < plan.Bounds["startMethod"].Top && plan.Bounds["startMethod"].Bottom < plan.Bounds["application"].Top);
                    Rectangle modes = plan.Bounds["tradingModes"];
                    Require(modes.Left >= 0 && modes.Right <= plan.Bounds["startMethod"].Width);
                    foreach (string role in new[] { "simulationOption", "liveOption" })
                    {
                        Require(plan.Bounds[role].Left >= 0 && plan.Bounds[role].Right <= modes.Width);
                    }
                }
            }
        });
        Test("A NinjaTrader version is tested, unknown or invalid, and each has its own message", delegate
        {
            Require(NinjaTraderCompatibility.Classify("8.1.8.4") == NinjaTraderVersionStatus.Tested);
            Require(NinjaTraderCompatibility.Classify(" 8.1.8.4 ") == NinjaTraderVersionStatus.Tested);
            Require(NinjaTraderCompatibility.Classify("8.1.9.0") == NinjaTraderVersionStatus.Unknown);
            foreach (string invalid in new[] { null, "", "  ", "not-a-version", "8" })
            {
                Require(NinjaTraderCompatibility.Classify(invalid) == NinjaTraderVersionStatus.Invalid);
            }
            foreach (string language in new[] { "en", "fa" })
            {
                var messages = new HashSet<string>
                {
                    UiText.TargetStatus(language, new TargetInspection()),
                    UiText.TargetStatus(language, new TargetInspection { Exists = true }),
                    UiText.TargetStatus(language, new TargetInspection { Exists = true, Trusted = true, Status = NinjaTraderVersionStatus.Invalid }),
                    UiText.TargetStatus(language, new TargetInspection { Exists = true, Trusted = true, Version = "8.1.8.4", Status = NinjaTraderVersionStatus.Tested }),
                    UiText.TargetStatus(language, new TargetInspection { Exists = true, Trusted = true, Version = "8.1.9.0", Status = NinjaTraderVersionStatus.Unknown })
                };
                Require(messages.Count == 5);
                Require(Messages.For(Outcome.UntrustedTarget, language) != Messages.For(Outcome.UnexpectedFailure, language));
                Require(Messages.For(Outcome.LiveVersionNotConfirmed, language) != Messages.For(Outcome.UnexpectedFailure, language));
                using (var dialog = new ConfirmationDialog(language, LiveConfirmation.UnknownVersion, "8.1.9.0"))
                {
                    Require(dialog.AccessibleDescription.Contains("8.1.9.0") && dialog.AcceptButton == dialog.CancelAction && dialog.CancelButton == dialog.CancelAction);
                }
            }
        });
        Test("Agreeing to an untested version for Live survives saving settings", delegate
        {
            var settings = new Settings { NinjaTraderPath = @"C:\NT\NinjaTrader.exe", LiveAcceptedVersion = "8.1.9.0" };
            settings.Validate();
            string folder = Path.Combine(Path.GetTempPath(), "AutoLogin-accepted-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string path = Path.Combine(folder, "settings.json");
                JsonFiles.Write(path, settings);
                Require(JsonFiles.Read<Settings>(path).LiveAcceptedVersion == "8.1.9.0");
                JsonFiles.Write(path, new Settings { NinjaTraderPath = settings.NinjaTraderPath });
                Require(JsonFiles.Read<Settings>(path).LiveAcceptedVersion == null);
            }
            finally { Directory.Delete(folder, true); }
        });
        Test("The password is read only after the target file is trusted", delegate
        {
            var credentials = new FakeCredentials();
            string checkedPath = null;
            var untrusted = new TrustedTargetCredentials(@"C:\NT\NinjaTrader.exe", path => { checkedPath = path; return false; }, credentials);
            bool stopped = false;
            try
            {
                untrusted.Read();
            }
            catch (LoginProblem problem) { stopped = problem.Outcome == Outcome.UntrustedTarget; }
            Require(stopped && credentials.Reads == 0 && checkedPath == @"C:\NT\NinjaTrader.exe");
            using (Secret secret = new TrustedTargetCredentials("x", path => true, credentials).Read())
            {
                Require(secret.Valid && credentials.Reads == 1);
            }
        });
        Test("Only a valid NinjaTrader publisher signature is trusted", delegate
        {
            Require(NinjaTraderSignature.IsTrustedPublisher("NinjaTrader, LLC"));
            foreach (string other in new[] { null, "", "ninjatrader, llc", "NinjaTrader", "NinjaTrader, LLC Fake", "Microsoft Corporation" })
            {
                Require(!NinjaTraderSignature.IsTrustedPublisher(other));
            }
            Require(!NinjaTraderSignature.IsTrusted(Path.Combine(Path.GetTempPath(), "AutoLogin-missing-" + Guid.NewGuid().ToString("N") + ".exe")));
            // Read-only, and only where NinjaTrader happens to be installed. CI has no such file.
            string installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NinjaTrader 8", "bin", "NinjaTrader.exe");
            if (File.Exists(installed))
            {
                Require(NinjaTraderSignature.IsTrusted(installed));
            }

            // Validly signed by another publisher, then a plain unsigned file.
            Require(!NinjaTraderSignature.IsTrusted(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "csc.exe")));
            string unsigned = Path.Combine(Path.GetTempPath(), "AutoLogin-unsigned-" + Guid.NewGuid().ToString("N") + ".exe");
            File.WriteAllBytes(unsigned, new byte[] { 0x4D, 0x5A, 0, 0 });
            try
            {
                Require(!NinjaTraderSignature.IsTrusted(unsigned));
            }
            finally { File.Delete(unsigned); }
        });
        Test("Previous shortcuts are replaced once and never when they belong to another application", delegate
        {
            string folder = Path.Combine(Path.GetTempPath(), "AutoLogin-rename-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string exe = @"C:\Apps\NinjaTraderAutoLogin.exe";
                DesktopShortcuts.Write(folder, exe);
                string current = Path.Combine(folder, ProductInfo.EnglishName + ".lnk"), settings = Path.Combine(folder, ProductInfo.EnglishName + " Settings.lnk");
                string previous = Path.Combine(folder, "NinjaTrader Auto Login.lnk"), previousSettings = Path.Combine(folder, "NinjaTrader Login Settings.lnk");
                Require(File.Exists(current) && File.Exists(settings));
                File.Move(current, previous);
                File.Move(settings, previousSettings);
                Require(DesktopShortcuts.Inspect(folder, exe) == ShortcutState.Stale);
                DesktopShortcuts.Write(folder, exe);
                Require(DesktopShortcuts.Inspect(folder, exe) == ShortcutState.Ready && !File.Exists(previous) && !File.Exists(previousSettings));
                // Same old name, but it does not start the login, so it is not ours to remove.
                File.Copy(settings, previous);
                DesktopShortcuts.Write(folder, exe);
                Require(File.Exists(previous));
            }
            finally { Directory.Delete(folder, true); }
        });
        Test("A ready shortcut reads as a confirmation and offers no command", delegate
        {
            using (var link = new LinkAction { Text = "Update shortcut" })
            {
                Require(link.Enabled && !link.Ready);
                link.Ready = true;
                Require(!link.Enabled && link.Ready);
                link.Ready = false;
                Require(link.Enabled);
            }
        });
        Test("A missing stage file never stops the sign-in display", delegate
        {
            Require(RunEvidence.LastStage(Path.Combine(Path.GetTempPath(), "AutoLogin-missing-" + Guid.NewGuid().ToString("N") + ".json")) == null);
        });
        Test("First-run language follows the Windows display language", delegate
        {
            foreach (string name in new[] { "fa-IR", "fa", "prs-AF" })
            {
                Require(Settings.SystemLanguage(new System.Globalization.CultureInfo(name)) == "fa");
            }
            foreach (string name in new[] { "en-US", "en-GB", "ar-SA", "de-DE", "tr-TR" })
            {
                Require(Settings.SystemLanguage(new System.Globalization.CultureInfo(name)) == "en");
            }
            System.Globalization.CultureInfo previous = Thread.CurrentThread.CurrentUICulture;
            try
            {
                Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("fa-IR");
                Require(new Settings().UiLanguage == "fa");
                Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
                Require(new Settings().UiLanguage == "en");
                var saved = new Settings { UiLanguage = "fa" };
                saved.Validate();
                Require(saved.UiLanguage == "fa");
            }
            finally { Thread.CurrentThread.CurrentUICulture = previous; }
        });
        Test("Live confirmation dialog defaults to cancel and fits every text", delegate
        {
            foreach (string language in new[] { "en", "fa" })
            {
                foreach (string theme in new[] { "Dark", "Light" })
                {
                    UiAssets.Palette = UiPalette.For(theme);
                    foreach (LiveConfirmation kind in new[] { LiveConfirmation.EnterLive, LiveConfirmation.EnterLiveWithWatch, LiveConfirmation.WatchWhileLive })
                    {
                        using (var dialog = new ConfirmationDialog(language, kind))
                        {
                            Require(dialog.AcceptButton == dialog.CancelAction && dialog.CancelButton == dialog.CancelAction && dialog.ActiveControl == dialog.CancelAction);
                            Require(dialog.CancelAction.DialogResult == DialogResult.No && dialog.ConfirmAction.DialogResult == DialogResult.Yes);
                            Require(dialog.BackColor == UiAssets.Canvas);
                            foreach (Button action in new[] { dialog.CancelAction, dialog.ConfirmAction })
                            {
                                Require(TextRenderer.MeasureText(action.Text, action.Font).Width < action.Width && action.Bottom < dialog.ClientSize.Height);
                            }
                            Require(!dialog.CancelAction.Bounds.IntersectsWith(dialog.ConfirmAction.Bounds));
                            using (var plain = new ConfirmationDialog(language, LiveConfirmation.EnterLive))
                            {
                                Require(kind != LiveConfirmation.EnterLiveWithWatch || dialog.ClientSize.Height > plain.ClientSize.Height);
                            }
                        }
                    }
                }
            }
            UiAssets.Palette = UiPalette.For("Dark");
        });
        Test("Already-open Live request makes no mode or credential claim", delegate
        {
            var ui = Script(new UiSnapshot { ControlCenter = true });
            var credentials = new FakeCredentials();
            var result = new LoginEngine(ui, credentials, new FakeClock(), new FakeEvidence(), TimeSpan.FromSeconds(4), TradingMode.Live).Run(true, CancellationToken.None);
            Require(result.Outcome == Outcome.AlreadyOpen && credentials.Reads == 0 && result.LiveSelections == 0 && result.SimulationSelections == 0);
        });
        Test("Live selector contract requires unique enabled invokable Button metadata", delegate
        {
            var live = new ControlEvidence { AutomationId = "btnLiveTrading", ControlType = "ControlType.Button", Enabled = true, InvokePattern = true };
            var controls = new List<ControlEvidence> { live };
            Require(ControlContract.Evaluate(controls).LiveReady);
            live.Enabled = false;
            Require(!ControlContract.Evaluate(controls).LiveReady);
            live.Enabled = true;
            live.ControlType = "ControlType.Edit";
            Require(ControlContract.Evaluate(controls).Ambiguous);
            live.ControlType = "ControlType.Button";
            controls.Add(live);
            Require(ControlContract.Evaluate(controls).Ambiguous);
        });
        Test("Settings persist Live and Light; old settings migrate to Dark without changing Simulation", delegate
        {
            var settings = new Settings { TradingMode = "Live", UiTheme = "Light" };
            settings.Validate();
            Require(settings.TradingSelection == TradingMode.Live);
            using (var memory = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(Settings));
                serializer.WriteObject(memory, settings);
                memory.Position = 0;
                var restored = (Settings)serializer.ReadObject(memory);
                restored.Validate();
                Require(restored.TradingMode == "Live" && restored.UiTheme == "Light");
            }
            var old = new Settings { UiTheme = null };
            old.Validate();
            Require(old.UiTheme == "Dark" && old.TradingSelection == TradingMode.Simulation);
            bool rejected = false;
            try
            {
                new Settings { UiTheme = "Unknown" }.Validate();
            }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected);
        });
        Test("Vazir Regular and Bold load from embedded bytes with no file/installed-font lookup", delegate
        {
            foreach (string resource in new[] { UiFonts.RegularResource, UiFonts.BoldResource })
            {
                using (Stream stream = typeof(UiFonts).Assembly.GetManifestResourceStream(resource))
                {
                    Require(stream != null && stream.Length > 90000);
                }
            }

            Font regular = UiFonts.Get("fa", 9.25F, FontStyle.Regular), bold = UiFonts.Get("fa", 12.5F, FontStyle.Bold);
            Require(regular.FontFamily.Name == "Vazir" && bold.FontFamily.Name == "Vazir" && bold.Bold);
            Require(Object.ReferenceEquals(regular, UiFonts.Get("fa", 9.25F, FontStyle.Regular)));
        });
        Test("Embedded Persian and English font metrics fit production rows at all supported scales", delegate
        {
            foreach (int dpi in new[] { 96, 120, 144, 168, 192 })
            {
                foreach (string language in new[] { "fa", "en" })
                {
                    var plan = new SettingsLayoutPlan((int)Math.Round(SettingsLayoutPlan.DesignWidth * dpi / 96.0), (int)Math.Round(SettingsLayoutPlan.DesignHeight * dpi / 96.0), dpi, language == "fa", 0);
                    foreach (string role in new[] { "accountHeading", "pathLabel", "usernameLabel", "passwordLabel", "startMethodHeading", "environmentLabel", "startMethodLabel" })
                    {
                        Require(plan.Bounds[role].Height >= Math.Ceiling(UiFonts.Get(language, 9.5F, FontStyle.Regular).GetHeight(dpi)));
                    }

                    Require(plan.Bounds["heading"].Height >= Math.Ceiling(UiFonts.Get(language, 12.5F, FontStyle.Bold).GetHeight(dpi)));
                    foreach (string role in new[] { "shortcutOption", "watchOption", "simulationOption", "liveOption" })
                    {
                        Require(plan.Bounds[role].Height >= Math.Ceiling(UiFonts.Get(language, 9.25F, FontStyle.Regular).GetHeight(dpi)));
                    }

                    foreach (string role in new[] { "credentialHint", "startMethodHint", "targetStatus" })
                    {
                        Require(plan.Bounds[role].Height >= Math.Ceiling(UiFonts.Get(language, 9, FontStyle.Regular).GetHeight(dpi)));
                    }

                    Require(plan.Bounds["status"].Height >= 2 * Math.Ceiling(UiFonts.Get(language, 9.25F, FontStyle.Regular).GetHeight(dpi)));
                }
            }
        });
        Test("Both theme palettes retain readable text, accents and primary button contrast", delegate
        {
            foreach (string theme in new[] { "Dark", "Light" })
            {
                UiPalette p = UiPalette.For(theme);
                Require(p.Dark == (theme == "Dark"));
                foreach (Color background in new[] { p.Canvas, p.Surface, p.Input })
                {
                    Require(Contrast(p.Ink, background) >= 4.5 && Contrast(p.Muted, background) >= 4.5);
                }
                Require(Contrast(p.Accent, p.Canvas) >= 4.5 && Contrast(p.Accent, p.Surface) >= 4.5 && Contrast(Color.White, p.Primary) >= 4.5);
            }
            Require(UiPalette.For("Light").Canvas != UiPalette.For("Dark").Canvas);
        });
        Test("Simulation and Live colors are readable and clearly different in both themes", delegate
        {
            foreach (string theme in new[] { "Dark", "Light" })
            {
                UiPalette p = UiPalette.For(theme);
                Require(Contrast(p.Simulation, p.Canvas) >= 4.5 && Contrast(p.Live, p.Canvas) >= 4.5 && Contrast(p.LiveBadgeText, p.Live) >= 4.5);
                Require(p.Simulation.G > p.Simulation.R && p.Live.R > p.Live.G);
                Require(p.CaptionSimulation.G > p.CaptionSimulation.R && p.CaptionLive.R > p.CaptionLive.G);
                Require(p.FrameSimulation.G > p.FrameSimulation.R && p.FrameLive.R > p.FrameLive.G);
                Require(Contrast(p.FrameSimulation, p.Canvas) >= 3 && Contrast(p.FrameLive, p.Canvas) >= 3);
            }
        });
        Test("Live result serialization records its mode and excludes Simulation success", delegate
        {
            var record = new RunRecord { SchemaVersion = 2, RequestedMode = "Live", Outcome = "Success", LoginSubmissions = 1, LiveSelections = 1, SimulationSelections = 0 };
            using (var memory = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(RunRecord));
                serializer.WriteObject(memory, record);
                memory.Position = 0;
                var restored = (RunRecord)serializer.ReadObject(memory);
                Require(restored.RequestedMode == "Live" && restored.LiveSelections == 1 && restored.SimulationSelections == 0);
            }
        });
        Test("Adaptive plan keeps normal appearance and stacks narrow choices", delegate
        {
            var wide = new SettingsLayoutPlan(480, 484, 96, false, 0);
            Require(!wide.Compact && !wide.FooterInScroll && wide.Parents["header"] == "bodyViewport" && wide.Parents["statusRow"] == "settings");
            var narrow = new SettingsLayoutPlan(300, 480, 96, true, 17);
            Require(narrow.Compact);
            Require(narrow.Bounds["watchOption"].Top > narrow.Bounds["shortcutOption"].Bottom && narrow.Bounds["liveOption"].Top > narrow.Bounds["simulationOption"].Bottom);
            Require(narrow.Bounds["saveAndLaunch"].Top > narrow.Bounds["saveSettings"].Bottom);
        });
        Test("Short viewports scroll commands instead of overlapping page content", delegate
        {
            var plan = new SettingsLayoutPlan(480, 160, 96, false, 17);
            Require(plan.FooterInScroll && plan.Parents["footer"] == "bodyViewport");
            Require(plan.Bounds["bodyViewport"].Bottom <= 160 && plan.PageContentHeight > plan.Bounds["bodyViewport"].Height && plan.Bounds["footer"].Top > plan.Bounds["statusRow"].Bottom);
        });
        Test("Window fit handles smaller monitors, negative origins and changed work area", delegate
        {
            var work = new Rectangle(-1280, 100, 1280, 620);
            WindowFit fit = WindowFitPolicy.Calculate(work, new Rectangle(1000, 0, 1800, 1300), new Size(32, 78), 192, false);
            Require(work.Contains(fit.Bounds) && fit.Bounds.Left >= work.Left + 12 && fit.Bounds.Bottom <= work.Bottom - 12);
            Require(fit.MinimumOuter.Width <= fit.Bounds.Width && fit.MinimumOuter.Height <= fit.Bounds.Height);
            WindowFit changed = WindowFitPolicy.Calculate(new Rectangle(0, 0, 800, 456), fit.Bounds, new Size(48, 117), 288, false);
            Require(changed.Bounds.Width <= 776 && changed.Bounds.Height <= 432);
        });
        Test("Historical rounding boundary remains contained and current design size absorbs DPI rounding", delegate
        {
            Require(args.Length == 2);
            SettingsLayoutRecord recorded = JsonFiles.Read<SettingsLayoutRecord>(Path.Combine(args[1], "dpi-transition-rounding.json"));
            Require(recorded.Dpi == 144 && recorded.HorizontalScrollbar && recorded.VerticalScrollbar);
            SettingsLayoutItem oldViewport = recorded.Controls.Find(delegate (SettingsLayoutItem item)
            {
                return item.Role == "bodyViewport";
            });
            Require(recorded.PageContentHeight == oldViewport.Height + 1);
            foreach (bool rtl in new[] { false, true })
            {
                var repaired = new SettingsLayoutPlan(recorded.ClientWidth, recorded.ClientHeight, recorded.Dpi, rtl, 0);
                Require(repaired.PageContentHeight > 0 && repaired.Bounds["bodyViewport"].Height > 0);
                Require(repaired.PageContentWidth == repaired.Bounds["bodyViewport"].Width);
                Require(repaired.Bounds["statusRow"].Top - repaired.Bounds["bodyViewport"].Bottom >= repaired.Px(4));
            }
            foreach (int dpi in new[] { 96, 120, 144, 168, 192, 216, 240, 288 })
            {
                foreach (int loss in new[] { 0, 1 })
                {
                    var plan = new SettingsLayoutPlan((int)Math.Round(480 * dpi / 96.0) - loss, (int)Math.Round(SettingsLayoutPlan.DesignHeight * dpi / 96.0) - loss, dpi, true, 0);
                    Require(plan.PageContentHeight <= plan.Bounds["bodyViewport"].Height);
                }
            }
        });
        Test("Required vertical scrolling fits both native and legacy-framework widths across DPI pairs", delegate
        {
            foreach (int dpi in new[] { 96, 120, 144, 168, 192, 216, 240, 288 })
            {
                foreach (int frameworkWidth in new[] { 17, 21, 26, 30, 34, 38, 42, 51 })
                {
                    int nativeWidth = UiDpi.Scrollbar(dpi), reserve = UiDpi.ScrollbarReserve(nativeWidth, frameworkWidth);
                    var plan = new SettingsLayoutPlan((int)Math.Round(480 * dpi / 96.0), (int)Math.Round(160 * dpi / 96.0), dpi, true, reserve);
                    Require(plan.PageContentHeight > plan.Bounds["bodyViewport"].Height);
                    int actualClientWidth = plan.Bounds["bodyViewport"].Width - nativeWidth;
                    int frameworkClientWidth = plan.Bounds["bodyViewport"].Width - frameworkWidth;
                    foreach (var parent in plan.Parents)
                    {
                        if (parent.Value == "bodyViewport")
                        {
                            Require(plan.Bounds[parent.Key].Right <= actualClientWidth && plan.Bounds[parent.Key].Right <= frameworkClientWidth);
                        }
                    }
                }
            }
        });
        RunPublicationChecks();
        RunPortabilityChecks();
        RunPasswordEntryChecks();
        RunReleaseChecks();
        if (args.Length >= 1)
        {
            JsonFiles.Write(Path.GetFullPath(args[0]), report);
        }

        int failed = report.Checks.FindAll(delegate (Check item)
        {
            return !item.Passed;
        }).Count;
        Console.WriteLine(report.Checks.Count + " checks; " + failed + " failures. Native acceptance is still pending.");
        UiFonts.Release();
        return failed == 0 ? 0 : 1;
    }
}
