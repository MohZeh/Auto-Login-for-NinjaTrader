using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using NinjaTraderAutoLogin;

// Runs the real UI Automation path against a test window on a private desktop.
// The target is matched by process id and by its own path, so a real NinjaTrader is never touched.
internal static class FakeTargetChecks
{
    private sealed class Scenario
    {
        public string Name, Expected;
        public TradingMode Mode = TradingMode.Simulation;
        public Outcome Outcome;
        public int Submissions, Simulation, Live, TimeoutSeconds = 30;
        public bool RequireTrust, PressLiveDirectly;
    }

    private sealed class Evidence : IRunEvidence
    {
        public bool Event(Stage stage, string action, UiSnapshot snapshot)
        {
            return true;
        }
        public void Cause(Exception error)
        {
            Console.WriteLine("  cause: " + error.GetType().Name);
        }
        public bool Complete(Outcome outcome, int login, int simulation, int live = 0)
        {
            return true;
        }
    }

    private sealed class TestCredentials : ICredentials
    {
        public int Reads;
        public Secret Read()
        {
            Reads++;
            return new Secret("fake-user", "fake-pass-1");
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public string Reserved, Desktop, Title;
        public int X, Y, XSize, YSize, XChars, YChars, FillAttribute, Flags;
        public short ShowWindow, Reserved2;
        public IntPtr Reserved3, Input, Output, Error;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process, Thread;
        public int ProcessId, ThreadId;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr mode, int flags, uint access, IntPtr attributes);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetThreadDesktop(IntPtr desktop);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(string application, string commandLine, IntPtr processAttributes, IntPtr threadAttributes, bool inherit,
        uint flags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInformation information);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    public static int Run(string target, string reportFolder)
    {
        int exitCode = 1;
        var thread = new Thread(delegate ()
        {
            exitCode = RunAll(Path.GetFullPath(target), Path.GetFullPath(reportFolder));
        });
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
        thread.Join();
        return exitCode;
    }
    private static int RunAll(string target, string reportFolder)
    {
        string name = "AutoLoginTests" + Guid.NewGuid().ToString("N").Substring(0, 8);
        IntPtr desktop = CreateDesktop(name, IntPtr.Zero, IntPtr.Zero, 0, 0x10000000, IntPtr.Zero);
        if (desktop == IntPtr.Zero || !SetThreadDesktop(desktop))
        {
            Console.WriteLine("Private desktop unavailable: " + Marshal.GetLastWin32Error());
            return 2;
        }

        var scenarios = new[]
        {
            new Scenario { Name = "normal", Expected = "Simulation sign-in", Outcome = Outcome.Success, Submissions = 1, Simulation = 1 },
            new Scenario { Name = "normal", Expected = "Live sign-in", Mode = TradingMode.Live, Outcome = Outcome.Success, Submissions = 1, Live = 1 },
            new Scenario { Name = "slow-start", Expected = "Late sign-in window", Outcome = Outcome.Success, Submissions = 1, Simulation = 1 },
            new Scenario { Name = "slow-network", Expected = "Delay after login is not a second submission", Outcome = Outcome.Success, Submissions = 1, Simulation = 1 },
            new Scenario { Name = "rejected", Expected = "Rejected password", Outcome = Outcome.AuthenticationFailed, Submissions = 1 },
            new Scenario { Name = "challenge", Expected = "Manual challenge stops before the password is used", Outcome = Outcome.NeedsManual },
            new Scenario { Name = "dialog-after-login", Expected = "Owned modal dialog stops safely at the timeout", Outcome = Outcome.TimedOut, Submissions = 1, TimeoutSeconds = 5 },
            new Scenario { Name = "changed-controls", Expected = "Changed control ids stop without submitting", Outcome = Outcome.TimedOut, TimeoutSeconds = 5 },
            new Scenario { Name = "exit-after-login", Expected = "NinjaTrader exits during sign-in", Outcome = Outcome.ProcessExited, Submissions = 1 },
            new Scenario { Name = "login-returns", Expected = "No screen follows login", Outcome = Outcome.TimedOut, Submissions = 1, TimeoutSeconds = 5 },
            new Scenario { Name = "no-live-account", Expected = "Live button that opens an account is never pressed", Mode = TradingMode.Live, Outcome = Outcome.LiveAccountUnavailable, Submissions = 1 },
            new Scenario { Name = "no-live-account", Expected = "Simulation on an account without Live", Outcome = Outcome.Success, Submissions = 1, Simulation = 1 },
            new Scenario { Name = "normal", Expected = "Unsigned target never receives the password", Outcome = Outcome.UntrustedTarget, RequireTrust = true },
            new Scenario { Name = "modes", Expected = "A signed-in environment screen continues without the password", Outcome = Outcome.Success, Simulation = 1 },
            new Scenario { Name = "modes", Expected = "An unsigned target's environment screen is never clicked", Outcome = Outcome.UntrustedTarget, RequireTrust = true },
            new Scenario { Name = "modes-open-account", Expected = "Pressing Live re-checks the button and refuses Open Account", Mode = TradingMode.Live, Outcome = Outcome.LiveAccountUnavailable, PressLiveDirectly = true }
        };
        int failures = 0;
        var lines = new List<string>();
        foreach (Scenario scenario in scenarios)
        {
            string failure = RunScenario(scenario, target, name, reportFolder);
            string line = (failure == null ? "PASS " : "FAIL ") + scenario.Expected + (failure == null ? "" : ": " + failure);
            Console.WriteLine(line);
            lines.Add(line);
            failures += failure == null ? 0 : 1;
        }
        Directory.CreateDirectory(reportFolder);
        File.WriteAllLines(Path.Combine(reportFolder, "fake-target-checks.txt"), lines);
        Console.WriteLine(scenarios.Length + " fake-target scenarios; " + failures + " failures. This does not replace testing on a real NinjaTrader.");
        CloseDesktop(desktop);
        return failures == 0 ? 0 : 1;
    }
    // Skips the engine's own inspection, so only the check inside SelectMode can stop the press.
    private static LoginResult PressLive(ILoginUi ui)
    {
        var result = new LoginResult { RequestedMode = TradingMode.Live };
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        while (!ui.Inspect().LiveReady)
        {
            if (DateTime.UtcNow > deadline)
            {
                result.Outcome = Outcome.TimedOut;
                return result;
            }

            Thread.Sleep(100);
        }
        try
        {
            ui.SelectMode(TradingMode.Live, CancellationToken.None);
            result.LiveSelections = 1;
            result.Outcome = Outcome.Success;
        }
        catch (LoginProblem problem) { result.Outcome = problem.Outcome; }
        return result;
    }
    private static string RunScenario(Scenario scenario, string target, string desktop, string reportFolder)
    {
        Directory.CreateDirectory(reportFolder);
        string record = Path.Combine(reportFolder, "fake-target-" + Guid.NewGuid().ToString("N") + ".log");
        Process process = null;
        try
        {
            process = Start(target, scenario.Name, record, desktop);
            ProcessIdentity identity = Settled(process, target);
            if (identity == null)
            {
                return "target identity was not recognized";
            }

            var credentials = new TestCredentials();
            ICredentials used = scenario.RequireTrust
                ? (ICredentials)new TrustedTargetCredentials(target, NinjaTraderSignature.IsTrusted, credentials)
                : credentials;
            ILoginUi ui = scenario.RequireTrust
                ? (ILoginUi)new TrustedTargetUi(new NativeLoginUi(identity), target, NinjaTraderSignature.IsTrusted)
                : new NativeLoginUi(identity);
            LoginResult result = scenario.PressLiveDirectly
                ? PressLive(ui)
                : new LoginEngine(ui, used, new SystemClock(), new Evidence(), TimeSpan.FromSeconds(scenario.TimeoutSeconds), scenario.Mode).Run(false, CancellationToken.None);
            string[] actions = File.Exists(record) ? File.ReadAllLines(record) : new string[0];
            if (result.Outcome != scenario.Outcome)
            {
                return "outcome " + result.Outcome + ", expected " + scenario.Outcome;
            }

            if (result.LoginSubmissions != scenario.Submissions || result.SimulationSelections != scenario.Simulation || result.LiveSelections != scenario.Live)
            {
                return "counts " + result.LoginSubmissions + "/" + result.SimulationSelections + "/" + result.LiveSelections;
            }

            int logins = 0, simulation = 0, live = 0;
            foreach (string action in actions)
            {
                if (action == "login 9 11")
                {
                    logins++;
                }
                else if (action == "mode Simulation")
                {
                    simulation++;
                }
                else if (action == "mode Live")
                {
                    live++;
                }
                else
                {
                    return "unexpected action in the target";
                }
            }
            if (logins != scenario.Submissions || simulation != scenario.Simulation || live != scenario.Live)
            {
                return "target saw " + logins + "/" + simulation + "/" + live;
            }

            if ((scenario.Submissions == 0 && credentials.Reads > 0 && scenario.Outcome != Outcome.NeedsManual) || (scenario.RequireTrust && credentials.Reads != 0))
            {
                return "the password was read";
            }

            return null;
        }
        catch (Exception error) when (error is InvalidOperationException || error is System.ComponentModel.Win32Exception || error is ArgumentException)
        {
            return error.GetType().Name;
        }
        finally
        {
            if (process != null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                    }
                }
                catch (InvalidOperationException) { }
                process.Dispose();
            }
            if (File.Exists(record))
            {
                File.Delete(record);
            }
        }
    }
    // The same wait production uses: a just-started process can be unreadable for a moment.
    private static ProcessIdentity Settled(Process process, string target)
    {
        var settle = new ProblemGrace(TimeSpan.FromSeconds(BuildInfo.ProcessSettleSeconds));
        while (true)
        {
            try
            {
                return ProcessIdentity.From(process, target);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                if (settle.Expired(DateTime.UtcNow))
                {
                    throw;
                }

                Thread.Sleep(50);
            }
        }
    }
    private static Process Start(string target, string scenario, string record, string desktop)
    {
        var startup = new StartupInfo { Size = Marshal.SizeOf(typeof(StartupInfo)), Desktop = desktop };
        ProcessInformation information;
        string commandLine = "\"" + target + "\" " + scenario + " \"" + record + "\"";
        if (!CreateProcess(target, commandLine, IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero, Path.GetDirectoryName(target), ref startup, out information))
        {
            throw new InvalidOperationException("TargetDidNotStart:" + Marshal.GetLastWin32Error());
        }

        CloseHandle(information.Thread);
        CloseHandle(information.Process);
        return Process.GetProcessById(information.ProcessId);
    }
}
