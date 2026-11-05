using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using NinjaTraderAutoLogin;

internal sealed class ConfigurationFixture : ISettingsWriter, IStartupSettings, ICredentialWriter
{
    public readonly List<Settings> Saved = new List<Settings>();
    public readonly HashSet<int> FailSaveCalls = new HashSet<int>();
    public string Startup = "previous-registration";
    public string Restored;
    public int CredentialWrites;
    public bool FailApply, FailCredentials, FailRestore;

    public void Save(Settings settings)
    {
        Saved.Add(settings);
        if (FailSaveCalls.Contains(Saved.Count))
        {
            throw new IOException("SimulatedSettingsFailure");
        }
    }
    public string Read()
    {
        return Startup;
    }
    public void Apply(LaunchMode mode)
    {
        if (FailApply)
        {
            throw new IOException("SimulatedStartupFailure");
        }

        Startup = mode.ToString();
    }
    public void Restore(string previous)
    {
        Restored = previous;
        if (FailRestore)
        {
            throw new IOException("SimulatedRecoveryFailure");
        }

        Startup = previous;
    }
    public void Save(string username, string password)
    {
        CredentialWrites++;
        if (FailCredentials)
        {
            throw new IOException("SimulatedCredentialFailure");
        }
    }
}

internal static partial class TestRunner
{
    private static void RunPublicationChecks()
    {
        Test("Configuration commits all selected settings after credential write succeeds", delegate
        {
            var fixture = new ConfigurationFixture();
            var previous = new Settings();
            var next = new Settings { LaunchMethod = "Watch" };
            new ConfigurationTransaction(fixture, fixture, fixture).Save(previous, next, "TEST-USER", "TEST-SECRET");
            Require(fixture.Saved.Count == 1 && Object.ReferenceEquals(next, fixture.Saved[0]));
            Require(fixture.CredentialWrites == 1 && fixture.Startup == "Watch");
        });
        Test("Blank password retains credentials without accessing the credential writer", delegate
        {
            var fixture = new ConfigurationFixture();
            new ConfigurationTransaction(fixture, fixture, fixture).Save(new Settings(), new Settings(), "TEST-USER", "");
            Require(fixture.CredentialWrites == 0);
        });
        Test("Startup failure does not write settings or credentials", delegate
        {
            var fixture = new ConfigurationFixture { FailApply = true };
            ConfigurationSaveException error = FailedSave(fixture);
            Require(!error.RecoveryIncomplete && fixture.Saved.Count == 0 && fixture.CredentialWrites == 0);
        });
        Test("Settings write failure attempts exact previous settings and startup recovery", delegate
        {
            var fixture = new ConfigurationFixture();
            fixture.FailSaveCalls.Add(1);
            ConfigurationSaveException error = FailedSave(fixture);
            Require(!error.RecoveryIncomplete && fixture.Saved.Count == 2 && fixture.Restored == "previous-registration");
            Require(fixture.CredentialWrites == 0 && fixture.Saved[1].LaunchMethod == "Shortcut");
        });
        Test("Credential failure restores both earlier configuration writes", delegate
        {
            var fixture = new ConfigurationFixture { FailCredentials = true };
            ConfigurationSaveException error = FailedSave(fixture);
            Require(!error.RecoveryIncomplete && fixture.Saved.Count == 2 && fixture.Startup == "previous-registration");
        });
        Test("Rollback failures remain explicit and both recovery steps are attempted", delegate
        {
            var fixture = new ConfigurationFixture { FailCredentials = true, FailRestore = true };
            fixture.FailSaveCalls.Add(2);
            ConfigurationSaveException error = FailedSave(fixture);
            Require(error.RecoveryIncomplete && error.RecoveryFailures.Length == 2 && fixture.Restored == "previous-registration");
            Require(!String.Join("|", error.RecoveryFailures).Contains("TEST-SECRET"));
        });
        Test("Recovery diagnostics retain failure types without credential values", delegate
        {
            var fixture = new ConfigurationFixture { FailCredentials = true, FailRestore = true };
            ConfigurationSaveException failure = FailedSave(fixture);
            AppFaultRecord diagnostic = AppFaults.Create("ConfigurationRecovery", failure);
            Require(diagnostic.CauseExceptionType == typeof(IOException).FullName);
            Require(diagnostic.RecoveryFailures.Count == 1 && !String.Join("|", diagnostic.RecoveryFailures).Contains("TEST-SECRET"));
        });
        Test("Worker request roundtrip excludes credential slots", delegate
        {
            byte[] frame = WorkerFrames.Serialize(new WorkerRequest
            {
                ParentId = 1,
                ProcessId = 2,
                ParentStartedTicks = 3,
                ProcessStartedTicks = 4,
                TargetPath = @"C:\Program Files\NinjaTrader 8\bin\NinjaTrader.exe",
                RunId = "fixture-run",
                RequestedMode = "Simulation"
            });
            string json = Encoding.UTF8.GetString(frame).ToLowerInvariant();
            Require(!json.Contains("password") && !json.Contains("username") && !json.Contains("secret"));
            WorkerRequest roundtrip = WorkerFrames.Deserialize<WorkerRequest>(frame);
            Require(roundtrip.ProcessId == 2 && roundtrip.RequestedMode == "Simulation");
        });
        Test("Worker framing rejects oversized and incomplete messages", delegate
        {
            foreach (byte[] input in new[] { BitConverter.GetBytes(WorkerFrames.MaximumBytes + 1), new byte[] { 3, 0, 0, 0, 1 } })
            {
                bool rejected = false;
                try
                {
                    using (var stream = new MemoryStream(input))
                    {
                        WorkerFrames.Read(stream);
                    }
                }
                catch (Exception error) when (error is IOException || error is InvalidDataException) { rejected = true; }
                Require(rejected);
            }
        });
        Test("Process supervisor receives a valid reply and waits for clean exit", delegate
        {
            WorkerProcessResult value = FixtureProcess("reply", TimeSpan.FromSeconds(5), CancellationToken.None);
            Require(value.Reason == WorkerStopReason.Completed && Encoding.UTF8.GetString(value.Response) == "{}");
            Require(ProcessGone(value.ProcessId));
        });
        Test("Worker request bytes do not depend on the console input encoding", delegate
        {
            byte[] expected = { 2, 0, 0, 0, (byte)'{', (byte)'}' };
            foreach (Encoding encoding in new Encoding[] { new UTF8Encoding(true), new UnicodeEncoding(false, true), new UTF8Encoding(false) })
            {
                WithConsoleInputEncoding(encoding, delegate
                {
                    WorkerProcessResult raw = FixtureProcess("raw", TimeSpan.FromSeconds(5), CancellationToken.None);
                    Require(raw.Reason == WorkerStopReason.Completed && raw.Response.SequenceEqual(expected));
                    WorkerProcessResult framed = FixtureProcess("reply", TimeSpan.FromSeconds(5), CancellationToken.None);
                    Require(framed.Reason == WorkerStopReason.Completed && Encoding.UTF8.GetString(framed.Response) == "{}");
                });
            }
        });
        Test("Worker mode without a request pipe exits before any helper work", delegate
        {
            string helper = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), StartupCommand.ExecutableName);
            using (Process process = Process.Start(new ProcessStartInfo(helper, AutomationWorker.Command) { UseShellExecute = false, CreateNoWindow = true }))
            {
                Require(process.WaitForExit(5000) && process.ExitCode == 2);
            }
        });
        Test("An invalid request pipe handle is rejected", delegate
        {
            bool rejected = false;
            try
            {
                WorkerFrames.ReadRequest("not-a-handle");
            }
            catch (ArgumentException) { rejected = true; }
            Require(rejected);
        });
        Test("Blocked child is terminated at watchdog deadline after its ready reply", delegate
        {
            var elapsed = Stopwatch.StartNew();
            WorkerProcessResult value = FixtureProcess("hang", TimeSpan.FromMilliseconds(750), CancellationToken.None);
            Require(value.Reason == WorkerStopReason.TimedOut && value.Response != null && ProcessGone(value.ProcessId));
            Require(elapsed.Elapsed < TimeSpan.FromSeconds(5));
        });
        Test("Cancellation terminates the blocked child before any later work", delegate
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.CancelAfter(750);
                WorkerProcessResult value = FixtureProcess("hang", TimeSpan.FromSeconds(10), cancellation.Token);
                Require(value.Reason == WorkerStopReason.Cancelled && value.Response != null && ProcessGone(value.ProcessId));
            }
        });
        Test("Malformed child output fails without leaving a worker running", delegate
        {
            WorkerProcessResult value = FixtureProcess("malformed", TimeSpan.FromSeconds(5), CancellationToken.None);
            Require(value.Reason == WorkerStopReason.Failed && ProcessGone(value.ProcessId));
        });
        Test("Closing the owner job terminates a waiting automation child", delegate
        {
            using (var requestPipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable))
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--worker-fixture hang " + requestPipe.GetClientHandleAsString())
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                try
                {
                    using (WorkerJob job = WorkerJob.Create())
                    {
                        process.Start();
                        requestPipe.DisposeLocalCopyOfClientHandle();
                        job.Attach(process);
                        WorkerFrames.Write(requestPipe, Encoding.UTF8.GetBytes("{}"));
                        requestPipe.Dispose();
                        Require(Encoding.UTF8.GetString(WorkerFrames.Read(process.StandardOutput.BaseStream)) == "{}");
                    }
                    Require(process.WaitForExit(2000));
                }
                finally
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
            }
        });
        Test("Pre-cancelled supervision does not start a child", delegate
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                bool cancelled = false;
                try
                {
                    FixtureProcess("reply", TimeSpan.FromSeconds(5), cancellation.Token);
                }
                catch (OperationCanceledException) { cancelled = true; }
                Require(cancelled);
            }
        });
        Test("Interrupted action records uncertainty instead of inventing completed selections", delegate
        {
            var value = new RunRecord();
            value.Events.Add(new EvidenceEvent { Action = "BeforeSubmit" });
            value.Events.Add(new EvidenceEvent { Action = "LoginSubmitted" });
            value.Events.Add(new EvidenceEvent { Action = "BeforeSimulation" });
            RunEvidence.UpdateInterrupted(value, Outcome.Cancelled);
            Require(value.ActionCompletionUncertain && value.LoginSubmissions == 1 && value.SimulationSelections == 0 && value.LiveSelections == 0);
        });
        Test("Known completed action counts survive worker interruption", delegate
        {
            var value = new RunRecord();
            foreach (string action in new[] { "BeforeSubmit", "LoginSubmitted", "BeforeLive", "LiveInvoked" })
            {
                value.Events.Add(new EvidenceEvent { Action = action });
            }

            RunEvidence.UpdateInterrupted(value, Outcome.TimedOut);
            Require(!value.ActionCompletionUncertain && value.LoginSubmissions == 1 && value.LiveSelections == 1 && value.SimulationSelections == 0);
        });
    }

    private static ConfigurationSaveException FailedSave(ConfigurationFixture fixture)
    {
        try
        {
            new ConfigurationTransaction(fixture, fixture, fixture).Save(new Settings(), new Settings { LaunchMethod = "Watch" }, "TEST-USER", "TEST-SECRET");
        }
        catch (ConfigurationSaveException error) { return error; }
        throw new Exception("ExpectedConfigurationFailure");
    }

    private static WorkerProcessResult FixtureProcess(string mode, TimeSpan timeout, CancellationToken cancellation)
    {
        return WorkerProcess.Run(Assembly.GetExecutingAssembly().Location, "--worker-fixture " + mode,
            Encoding.UTF8.GetBytes("{}"), timeout, cancellation);
    }

    private static bool ProcessGone(int processId)
    {
        try
        {
            using (var process = Process.GetProcessById(processId))
            {
                return process.HasExited;
            }
        }
        catch (ArgumentException) { return true; }
    }

    // Process.Start reads this cached value, which makes the check independent of
    // the host console and of the machine's system locale.
    private static void WithConsoleInputEncoding(Encoding encoding, Action body)
    {
        FieldInfo cached = typeof(Console).GetField("_inputEncoding", BindingFlags.NonPublic | BindingFlags.Static);
        object previous = cached.GetValue(null);
        cached.SetValue(null, encoding);
        try
        {
            Require(Console.InputEncoding.GetPreamble().Length == encoding.GetPreamble().Length);
            body();
        }
        finally { cached.SetValue(null, previous); }
    }

    private static int RunWorkerFixture(string mode, string requestPipe)
    {
        if (mode == "raw")
        {
            using (var input = new AnonymousPipeClientStream(PipeDirection.In, requestPipe))
            using (var received = new MemoryStream())
            using (Stream output = Console.OpenStandardOutput())
            {
                input.CopyTo(received);
                WorkerFrames.Write(output, received.ToArray());
            }
            return 0;
        }

        WorkerFrames.ReadRequest(requestPipe);

        using (Stream output = Console.OpenStandardOutput())
        {
            if (mode == "malformed")
            {
                output.Write(BitConverter.GetBytes(WorkerFrames.MaximumBytes + 1), 0, 4);
                return 0;
            }
            WorkerFrames.Write(output, Encoding.UTF8.GetBytes("{}"));
            if (mode == "hang")
            {
                Thread.Sleep(Timeout.Infinite);
            }
        }
        return 0;
    }
}
