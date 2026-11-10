using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;

namespace NinjaTraderAutoLogin
{
    [DataContract]
    internal sealed class WorkerRequest
    {
        [DataMember] public int ParentId, ProcessId;
        [DataMember] public long ParentStartedTicks, ProcessStartedTicks;
        [DataMember] public string TargetPath, RunId, RequestedMode;
        [DataMember] public bool AlreadyRunning, CredentialsRejected;
    }

    [DataContract]
    internal sealed class WorkerResponse
    {
        [DataMember] public string Candidate, RunId, Outcome;
        [DataMember] public int LoginSubmissions, SimulationSelections, LiveSelections;
    }

    internal static class AutomationWorker
    {
        public const string Command = "--automation-worker";

        public static LoginResult Execute(ProcessIdentity target, bool alreadyRunning, bool credentialsRejected,
            TradingMode mode, RunEvidence evidence, CancellationToken cancellation)
        {
            WorkerRequest request;
            using (var parent = Process.GetCurrentProcess())
            {
                request = new WorkerRequest
                {
                    ParentId = parent.Id,
                    ParentStartedTicks = parent.StartTime.ToUniversalTime().Ticks,
                    ProcessId = target.Id,
                    ProcessStartedTicks = target.StartedUtcTicks,
                    TargetPath = target.ImagePath,
                    RunId = evidence.RunId,
                    RequestedMode = mode.ToString(),
                    AlreadyRunning = alreadyRunning,
                    CredentialsRejected = credentialsRejected
                };
            }

            if (!evidence.Event(Stage.WaitingForLogin, "WorkerStarting", null))
            {
                throw new LoginProblem(Outcome.EvidenceUnavailable);
            }

            WorkerProcessResult supervised = WorkerProcess.Run(Paths.Executable, Command,
                WorkerFrames.Serialize(request), TimeSpan.FromSeconds(BuildInfo.StageTimeoutSeconds * 3 + 10), cancellation);
            if (supervised.Reason == WorkerStopReason.Completed)
            {
                try
                {
                    WorkerResponse response = WorkerFrames.Deserialize<WorkerResponse>(supervised.Response);
                    return evidence.ReadCompleted(response, mode);
                }
                catch (Exception error) when (error is IOException || error is InvalidDataException || error is SerializationException || error is ArgumentException)
                {
                    AppFaults.Record("WorkerResponse", error);
                }
            }
            Outcome outcome = supervised.Reason == WorkerStopReason.Cancelled ? Outcome.Cancelled :
                supervised.Reason == WorkerStopReason.TimedOut ? Outcome.TimedOut : Outcome.UnexpectedFailure;
            return evidence.Interrupt(outcome, mode);
        }

        // Any record of a refused password is enough to hold it: the parent's memory covers a
        // save that failed, and the settings file and the registry cover a parent that restarted.
        internal static Func<bool> Refusal(bool parentRejected, Func<bool> savedRejected)
        {
            return () => parentRejected || savedRejected();
        }

        public static int Run(string requestPipe)
        {
            int exitCode = 1;
            var thread = new Thread(new ThreadStart(delegate
            {
                exitCode = RunCore(requestPipe);
            }));
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
            thread.Join();
            return exitCode;
        }

        private static int RunCore(string requestPipe)
        {
            try
            {
                WorkerRequest request = WorkerFrames.Deserialize<WorkerRequest>(WorkerFrames.ReadRequest(requestPipe));

                TradingMode mode;
                if (request == null || !Enum.TryParse(request.RequestedMode, false, out mode) ||
                    !Enum.IsDefined(typeof(TradingMode), mode))
                {
                    throw new InvalidDataException("InvalidWorkerMode");
                }

                var settings = new Settings { NinjaTraderPath = request.TargetPath };
                settings.Validate();
                using (Process parent = Process.GetProcessById(request.ParentId))
                {
                    if (parent.StartTime.ToUniversalTime().Ticks != request.ParentStartedTicks ||
                        !String.Equals(parent.MainModule.FileName, Paths.Executable, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("InvalidWorkerParent");
                    }

                    ProcessSecurity.Verify(parent);
                }
                var target = new ProcessIdentity
                {
                    Id = request.ProcessId,
                    StartedUtcTicks = request.ProcessStartedTicks,
                    ImagePath = settings.NinjaTraderPath
                };
                RunEvidence evidence = RunEvidence.Continue(request.RunId, target, mode);
                var engine = new LoginEngine(new TrustedTargetUi(new NativeLoginUi(target), settings.NinjaTraderPath, NinjaTraderSignature.IsTrusted),
                    new TrustedTargetCredentials(settings.NinjaTraderPath, NinjaTraderSignature.IsTrusted,
                        new UnrejectedCredentials(Refusal(request.CredentialsRejected,
                            () => SettingsStore.Load().CredentialsRejected || RejectionLatch.Current.Read() != RefusalState.None), new WindowsCredentials())),
                    new SystemClock(), evidence, TimeSpan.FromSeconds(BuildInfo.StageTimeoutSeconds), mode);
                LoginResult result = engine.Run(request.AlreadyRunning, CancellationToken.None);
                var response = new WorkerResponse
                {
                    Candidate = BuildInfo.Version,
                    RunId = request.RunId,
                    Outcome = result.Outcome.ToString(),
                    LoginSubmissions = result.LoginSubmissions,
                    SimulationSelections = result.SimulationSelections,
                    LiveSelections = result.LiveSelections
                };
                using (Stream output = Console.OpenStandardOutput())
                {
                    WorkerFrames.Write(output, WorkerFrames.Serialize(response));
                }

                return 0;
            }
            catch (Exception error)
            {
                AppFaults.Record("AutomationWorker", error);
                return 1;
            }
        }
    }
}
