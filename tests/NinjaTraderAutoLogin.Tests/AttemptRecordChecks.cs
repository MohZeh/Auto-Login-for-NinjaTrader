using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using Microsoft.Win32;
using NinjaTraderAutoLogin;

internal sealed class KeptSecretCredentials : ICredentials
{
    public Secret Last;
    public Secret Read()
    {
        Last = new Secret("TEST-USER-DO-NOT-EXPORT", "TEST-PASSWORD-DO-NOT-EXPORT");
        return Last;
    }
}

internal static partial class TestRunner
{
    private static void RunAttemptRecordChecks()
    {
        Test("After a run, the records follow what it showed about the password", delegate
        {
            // No password left, or the run record could not be read: the registry decides.
            Require(RefusalPolicy.AfterRun(Outcome.AlreadyOpen, true, 0, 0, true) == null);
            Require(RefusalPolicy.AfterRun(Outcome.RefusalUnavailable, true, 0, 0, false) == null);
            Require(RefusalPolicy.AfterRun(Outcome.EvidenceUnavailable, false, 0, 0, false) == null);
            // Answers the worker saw to the end.
            Require(RefusalPolicy.AfterRun(Outcome.AuthenticationFailed, true, 1, 1, false) == RefusalState.Rejected);
            Require(RefusalPolicy.AfterRun(Outcome.LoginNotAccepted, true, 1, 1, false) == RefusalState.Uncertain);
            Require(RefusalPolicy.AfterRun(Outcome.Success, true, 1, 1, true) == RefusalState.None);
            Require(RefusalPolicy.AfterRun(Outcome.TimedOut, true, 1, 1, false) == RefusalState.None);
            Require(RefusalPolicy.AfterRun(Outcome.Cancelled, true, 1, 1, false) == RefusalState.None);
            // The send itself failed part way, so the password may have left.
            Require(RefusalPolicy.AfterRun(Outcome.UnexpectedFailure, true, 1, 0, false) == RefusalState.Uncertain);
            // The worker stopped before it saw an answer.
            Require(RefusalPolicy.AfterRun(Outcome.Cancelled, false, 1, 1, false) == RefusalState.Uncertain);
            Require(RefusalPolicy.AfterRun(Outcome.TimedOut, false, 1, 0, false) == RefusalState.Uncertain);
            Require(RefusalPolicy.AfterRun(Outcome.AuthenticationFailed, false, 1, 1, false) == RefusalState.Uncertain);
            // The environment screen follows only an accepted password, even if the worker stopped.
            Require(RefusalPolicy.AfterRun(Outcome.TimedOut, false, 1, 1, true) == RefusalState.None);
        });
        Test("The run record tells whether a password send started and the environment screen appeared", delegate
        {
            var value = new RunRecord();
            foreach (string action in new[] { "BeforeSubmit", "LoginSubmitted", "BeforeSimulation" })
            {
                value.Events.Add(new EvidenceEvent { Action = action });
            }

            RunEvidence.UpdateInterrupted(value, Outcome.Cancelled);
            LoginResult result = RunEvidence.Result(value, Outcome.Cancelled, TradingMode.Simulation, false);
            Require(result.SubmitsStarted == 1 && result.LoginSubmissions == 1 && result.ModeReached && !result.Concluded);
            var started = new RunRecord();
            started.Events.Add(new EvidenceEvent { Action = "BeforeSubmit" });
            RunEvidence.UpdateInterrupted(started, Outcome.TimedOut);
            result = RunEvidence.Result(started, Outcome.TimedOut, TradingMode.Live, true);
            Require(result.SubmitsStarted == 1 && result.LoginSubmissions == 0 && !result.ModeReached && result.Concluded);
        });
        Test("A password is handed over only after its attempt is recorded", delegate
        {
            FakeUi ui = Script(new UiSnapshot { LoginReady = true }, new UiSnapshot { SimulationReady = true }, new UiSnapshot { ControlCenter = true });
            int records = 0, submittedWhenRecorded = -1;
            var recorded = new RecordedCredentials(() => { records++; submittedWhenRecorded = ui.Submissions; return true; }, new FakeCredentials());
            LoginResult result = new LoginEngine(ui, recorded, new FakeClock(), new FakeEvidence(), TimeSpan.FromSeconds(4)).Run(false, CancellationToken.None);
            Require(result.Outcome == Outcome.Success && records == 1 && submittedWhenRecorded == 0 && ui.Submissions == 1);
            // A record that cannot be written stops the send and clears the secret from memory.
            FakeUi stopped = Script(new UiSnapshot { LoginReady = true });
            var kept = new KeptSecretCredentials();
            result = new LoginEngine(stopped, new RecordedCredentials(() => false, kept), new FakeClock(), new FakeEvidence(), TimeSpan.FromSeconds(4)).Run(false, CancellationToken.None);
            Require(result.Outcome == Outcome.RefusalUnavailable && stopped.Submissions == 0 && result.LoginSubmissions == 0);
            Require(kept.Last != null && kept.Last.Password == null && kept.Last.Username == null);
            // Nothing to send, or no password needed: nothing is recorded.
            records = 0;
            FakeUi missing = Script(new UiSnapshot { LoginReady = true });
            result = new LoginEngine(missing, new RecordedCredentials(() => { records++; return true; }, new FakeCredentials { Missing = true }), new FakeClock(), new FakeEvidence(), TimeSpan.FromSeconds(4)).Run(false, CancellationToken.None);
            Require(result.Outcome == Outcome.CredentialsMissing && records == 0 && missing.Submissions == 0);
            FakeUi modes = Script(new UiSnapshot { SimulationReady = true }, new UiSnapshot { ControlCenter = true });
            result = new LoginEngine(modes, new RecordedCredentials(() => { records++; return true; }, new FakeCredentials()), new FakeClock(), new FakeEvidence(), TimeSpan.FromSeconds(4)).Run(true, CancellationToken.None);
            Require(result.Outcome == Outcome.Success && records == 0);
        });
        Test("A sign-in cut off after the password left asks first, even after a restart", delegate
        {
            string path = TestRegistryKey();
            string latchPath = path + @"\Latch";
            RegistryAccessRule denyWrite = new RegistryAccessRule(WindowsIdentity.GetCurrent().User, RegistryRights.SetValue, AccessControlType.Deny);
            try
            {
                var latch = new RejectionLatch(latchPath);
                // The worker's chain: refusal check, attempt record, then the password.
                Func<FakeCredentials, ICredentials> chain = inner => new UnrejectedCredentials(
                    () => RefusalPolicy.RequireNoRefusal(false, false, latch.Read()),
                    new RecordedCredentials(() => latch.Write(RefusalState.Uncertain), inner));
                var first = new FakeCredentials();
                FakeUi waiting = Script(new UiSnapshot { LoginReady = true }, new UiSnapshot());
                LoginResult sent = new LoginEngine(waiting, chain(first), new FakeClock(), new FakeEvidence(), TimeSpan.FromSeconds(4)).Run(false, CancellationToken.None);
                Require(sent.LoginSubmissions == 1 && waiting.Submissions == 1 && Is(latch.Read(), RefusalState.Uncertain));
                // The app and the worker stop here, so nothing after the send runs. A new latch over
                // the same key, with a settings file that never recorded anything, is the restart.
                var restarted = new RejectionLatch(latchPath);
                Require(RefusalPolicy.AtStartup(RefusalPolicy.Of(new Settings()), restarted.Read()) == RefusalState.Uncertain);
                var second = new FakeCredentials();
                FakeUi again = Script(new UiSnapshot { LoginReady = true });
                LoginResult held = new LoginEngine(again, chain(second), new FakeClock(), new FakeEvidence(), TimeSpan.FromSeconds(4)).Run(false, CancellationToken.None);
                Require(held.Outcome == Outcome.CredentialsRejected && second.Reads == 0 && again.Submissions == 0);
                // The app survived, but the worker stopped before an answer: the app holds it too.
                RefusalUpdate update = RefusalRecords.AfterRun(RefusalState.None,
                    new LoginResult { Outcome = Outcome.Cancelled, Concluded = false, SubmitsStarted = 1, LoginSubmissions = 1 }, restarted);
                Require(update.Hold == RefusalState.Uncertain && update.Save && update.Latched && Is(latch.Read(), RefusalState.Uncertain));
                // A run record that could not be read: the attempt record decides.
                update = RefusalRecords.AfterRun(RefusalState.None, new LoginResult { Outcome = Outcome.EvidenceUnavailable, ActionCompletionUncertain = true }, restarted);
                Require(update.Hold == RefusalState.Uncertain && update.Save);
                // The owner allowed another attempt, which clears the records, and it succeeded.
                Require(latch.Write(RefusalState.None));
                var third = new FakeCredentials();
                FakeUi accepted = Script(new UiSnapshot { LoginReady = true }, new UiSnapshot { SimulationReady = true }, new UiSnapshot { ControlCenter = true });
                LoginResult success = new LoginEngine(accepted, chain(third), new FakeClock(), new FakeEvidence(), TimeSpan.FromSeconds(4)).Run(false, CancellationToken.None);
                Require(success.Outcome == Outcome.Success && Is(latch.Read(), RefusalState.Uncertain));
                update = RefusalRecords.AfterRun(RefusalState.None,
                    new LoginResult { Outcome = Outcome.Success, Concluded = true, SubmitsStarted = 1, LoginSubmissions = 1, SimulationSelections = 1, ModeReached = true }, latch);
                Require(update.Hold == RefusalState.None && !update.Save && Is(latch.Read(), RefusalState.None));
                using (RegistryKey left = Registry.CurrentUser.OpenSubKey(latchPath))
                {
                    Require(left == null);
                }

                update = RefusalRecords.AfterRun(RefusalState.None, new LoginResult { Outcome = Outcome.AlreadyOpen, Concluded = true }, latch);
                Require(update.Hold == RefusalState.None && !update.Save);
                // A refusal whose registry write fails, after a settings save that also fails:
                // the attempt record still holds the password back after a restart.
                Require(latch.Write(RefusalState.Uncertain));
                ChangeAccess(latchPath, denyWrite, true);
                update = RefusalRecords.AfterRun(RefusalState.None,
                    new LoginResult { Outcome = Outcome.AuthenticationFailed, Concluded = true, SubmitsStarted = 1, LoginSubmissions = 1 }, latch);
                Require(update.Hold == RefusalState.Rejected && update.Save && !update.Latched);
                Require(RefusalPolicy.AtStartup(RefusalPolicy.Of(new Settings()), new RejectionLatch(latchPath).Read()) == RefusalState.Uncertain);
                // A known answer whose clear fails keeps what the worker will read.
                update = RefusalRecords.AfterRun(RefusalState.None,
                    new LoginResult { Outcome = Outcome.Success, Concluded = true, SubmitsStarted = 1, LoginSubmissions = 1, SimulationSelections = 1, ModeReached = true }, latch);
                Require(update.Hold == RefusalState.Uncertain && update.Save && update.Latched && Is(latch.Read(), RefusalState.Uncertain));
                ChangeAccess(latchPath, denyWrite, false);
                Require(latch.Write(RefusalState.None));
            }
            finally
            {
                ChangeAccess(latchPath, denyWrite, false);
                RemoveTestRegistryKey(path);
            }
        });
    }
}
