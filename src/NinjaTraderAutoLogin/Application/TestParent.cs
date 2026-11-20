#if TEST_HOOKS
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;

namespace NinjaTraderAutoLogin
{
    [DataContract]
    internal sealed class TestParentResult
    {
        [DataMember] public string Outcome, Hold;
        [DataMember] public int LoginSubmissions, SimulationSelections, SubmitsStarted;
        [DataMember] public bool Concluded, Save, Latched, Saved;
    }

    // Compiled only into the TestHooks build. One sign-in the way LoginContext runs it, without
    // its window, tray and dialogs: the refusal held at startup, the real worker process, and
    // the records applied after the run.
    internal static class TestParent
    {
        public const string Command = "--test-parent";

        public static int Run(string resultPath, string modeName)
        {
            TradingMode mode;
            if (!TestHooks.Configured || !Enum.TryParse(modeName, false, out mode) || !Enum.IsDefined(typeof(TradingMode), mode))
            {
                return 4;
            }

            try
            {
                Settings settings = SettingsStore.Load();
                settings.NinjaTraderPath = TestHooks.TrustedTarget;
                RefusalState held = RefusalPolicy.AtStartup(RefusalPolicy.Of(settings), RejectionLatch.Current.Read());
                ProcessIdentity process = Settled(TestHooks.TrustedTarget);
                var evidence = new RunEvidence(process, "TestParent", mode);
                LoginResult result = AutomationWorker.Execute(process, false, held != RefusalState.None, mode, evidence, CancellationToken.None);
                RefusalUpdate update = RefusalRecords.AfterRun(held, result, RejectionLatch.Current);
                settings.CredentialsRejected = update.Hold != RefusalState.None;
                settings.RejectionUncertain = update.Hold == RefusalState.Uncertain;
                bool saved = true;
                if (update.Save)
                {
                    try
                    {
                        SettingsStore.Save(settings);
                    }
                    catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is InvalidDataException)
                    {
                        AppFaults.Record("TestParentSave", error);
                        saved = false;
                    }
                }

                JsonFiles.Write(resultPath, new TestParentResult
                {
                    Outcome = result.Outcome.ToString(),
                    Hold = update.Hold.ToString(),
                    LoginSubmissions = result.LoginSubmissions,
                    SimulationSelections = result.SimulationSelections,
                    SubmitsStarted = result.SubmitsStarted,
                    Concluded = result.Concluded,
                    Save = update.Save,
                    Latched = update.Latched,
                    Saved = saved
                });
                return 0;
            }
            catch (Exception error)
            {
                AppFaults.Record("TestParent", error);
                return 1;
            }
        }

        private static ProcessIdentity Settled(string path)
        {
            TimeSpan deadline = MonotonicClock.Now + TimeSpan.FromSeconds(BuildInfo.ProcessSettleSeconds + 5);
            while (true)
            {
                ProcessScan scan = ProcessScan.Find(path);
                if (!scan.Unsettled && scan.Process != null)
                {
                    return scan.Process;
                }

                if (MonotonicClock.Now >= deadline)
                {
                    throw scan.Unsettled ? (Exception)scan.Problem() : new LoginProblem(Outcome.ProcessExited);
                }

                Thread.Sleep(100);
            }
        }
    }
}
#endif
