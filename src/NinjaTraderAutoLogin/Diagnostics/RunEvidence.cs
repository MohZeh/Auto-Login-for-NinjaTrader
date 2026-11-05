using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace NinjaTraderAutoLogin
{
    [DataContract]
    internal sealed class EvidenceEvent
    {
        [DataMember] public string Utc, Stage, Action;
        [DataMember] public List<ControlEvidence> Controls;
    }

    [DataContract]
    internal sealed class RunRecord
    {
        [DataMember] public int SchemaVersion = 3;
        [DataMember] public string RunId, Candidate, HelperSha256, StartedUtc, CompletedUtc;
        [DataMember] public string Trigger, NinjaTraderPath, NinjaTraderVersion, ProcessStartedUtc;
        [DataMember] public int ProcessId;
        [DataMember] public string RequestedMode = "Simulation";
        [DataMember] public string Outcome = "Running";
        [DataMember] public int LoginSubmissions, SimulationSelections, LiveSelections;
        [DataMember] public bool HumanAccepted = false;
        [DataMember] public bool ActionCompletionUncertain;
        [DataMember] public string CauseType;
        [DataMember] public int CauseHResult;
        [DataMember] public List<EvidenceEvent> Events = new List<EvidenceEvent>();
    }

    internal sealed class RunEvidence : IRunEvidence
    {
        private readonly RunRecord record;
        public readonly string FilePath;
        public string RunId
        {
            get
            {
                return record.RunId;
            }
        }
        private string lastMetadata;
        public RunEvidence(ProcessIdentity process, string trigger, TradingMode requestedMode = TradingMode.Simulation)
        {
            string id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            record = new RunRecord
            {
                SchemaVersion = 3,
                RunId = id,
                Candidate = BuildInfo.Version,
                RequestedMode = requestedMode.ToString(),
                HelperSha256 = HashFile(Paths.Executable),
                StartedUtc = DateTime.UtcNow.ToString("o"),
                Trigger = trigger,
                NinjaTraderPath = process.ImagePath,
                NinjaTraderVersion = process.Version,
                ProcessId = process.Id,
                ProcessStartedUtc = process.StartedUtcTicks == 0 ? null : new DateTime(process.StartedUtcTicks, DateTimeKind.Utc).ToString("o")
            };
            FilePath = Path.Combine(Paths.Evidence, "run-" + id + ".json");
        }
        public bool Event(Stage stage, string action, UiSnapshot snapshot)
        {
            if (snapshot != null)
            {
                var signature = new StringBuilder();
                foreach (ControlEvidence item in snapshot.Controls)
                {
                    signature.Append(item.AutomationId).Append(item.ControlType).Append(item.Enabled).Append(item.Offscreen).Append(item.ValuePattern).Append(item.InvokePattern);
                }

                string next = stage.ToString() + signature.ToString();
                if (next == lastMetadata)
                {
                    return true;
                }

                lastMetadata = next;
            }
            record.Events.Add(new EvidenceEvent
            {
                Utc = DateTime.UtcNow.ToString("o"),
                Stage = stage.ToString(),
                Action = action,
                Controls = snapshot == null ? null : snapshot.Controls
            });
            return Persist();
        }
        public void Cause(Exception error)
        {
            ApplyCause(record, error);
        }

        // Type and code only: exception messages can contain paths or other private text.
        internal static void ApplyCause(RunRecord value, Exception error)
        {
            if (error != null)
            {
                value.CauseType = error.GetType().FullName;
                value.CauseHResult = error.HResult;
            }
        }
        public bool Complete(Outcome outcome, int loginSubmissions, int simulationSelections, int liveSelections = 0)
        {
            record.Outcome = outcome.ToString();
            record.CompletedUtc = DateTime.UtcNow.ToString("o");
            record.LoginSubmissions = loginSubmissions;
            record.SimulationSelections = simulationSelections;
            record.LiveSelections = liveSelections;
            return Persist();
        }

        private RunEvidence(RunRecord record, string path)
        {
            this.record = record;
            FilePath = path;
        }

        public static RunEvidence Continue(string runId, ProcessIdentity target, TradingMode mode)
        {
            if (runId == null || !System.Text.RegularExpressions.Regex.IsMatch(runId, @"^\d{8}-\d{6}-\d{3}-[a-f0-9]{8}$"))
            {
                throw new InvalidDataException("InvalidRunId");
            }

            string path = Path.Combine(Paths.Evidence, "run-" + runId + ".json");
            RunRecord value = ReadRecord(path, runId, mode);
            string started = new DateTime(target.StartedUtcTicks, DateTimeKind.Utc).ToString("o");
            if (value.ProcessId != target.Id || value.ProcessStartedUtc != started ||
                !String.Equals(value.NinjaTraderPath, target.ImagePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("WorkerTargetChanged");
            }

            return new RunEvidence(value, path);
        }

        private static RunRecord ReadRecord(string path, string runId, TradingMode mode)
        {
            RunRecord value = JsonFiles.Read<RunRecord>(path);
            if (value == null || value.SchemaVersion != 3 || value.Candidate != BuildInfo.Version ||
                value.HelperSha256 != HashFile(Paths.Executable) || value.RunId != runId || value.RequestedMode != mode.ToString())
            {
                throw new InvalidDataException("WorkerEvidenceChanged");
            }

            return value;
        }

        public LoginResult ReadCompleted(WorkerResponse response, TradingMode mode)
        {
            RunRecord value = ReadRecord(FilePath, RunId, mode);
            Outcome outcome;
            if (response == null || response.Candidate != BuildInfo.Version || response.RunId != RunId ||
                !Enum.TryParse(response.Outcome, false, out outcome) || !Enum.IsDefined(typeof(Outcome), outcome) ||
                value.CompletedUtc == null || value.Outcome != response.Outcome ||
                value.LoginSubmissions != response.LoginSubmissions || value.SimulationSelections != response.SimulationSelections ||
                value.LiveSelections != response.LiveSelections || !ValidCounts(value, mode) ||
                (outcome == Outcome.Success && (mode == TradingMode.Simulation ? value.SimulationSelections != 1 : value.LiveSelections != 1)))
            {
                throw new InvalidDataException("InvalidWorkerResult");
            }

            return Result(value, outcome, mode);
        }

        public LoginResult Interrupt(Outcome outcome, TradingMode mode, Exception cause = null)
        {
            try
            {
                RunRecord value = ReadRecord(FilePath, RunId, mode);
                UpdateInterrupted(value, outcome);
                ApplyCause(value, cause);
                JsonFiles.Write(FilePath, value);
                return Result(value, outcome, mode);
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is SerializationException)
            {
                AppFaults.Record("WorkerInterruptionEvidence", error);
                return new LoginResult { RequestedMode = mode, Outcome = Outcome.EvidenceUnavailable, ActionCompletionUncertain = true };
            }
        }

        private static int Count(RunRecord value, string action)
        {
            return value.Events.FindAll(item => item.Action == action).Count;
        }

        internal static void UpdateInterrupted(RunRecord value, Outcome outcome)
        {
            value.LoginSubmissions = Count(value, "LoginSubmitted");
            value.SimulationSelections = Count(value, "SimulationInvoked");
            value.LiveSelections = Count(value, "LiveInvoked");
            value.ActionCompletionUncertain = Count(value, "BeforeSubmit") > value.LoginSubmissions ||
                Count(value, "BeforeSimulation") > value.SimulationSelections || Count(value, "BeforeLive") > value.LiveSelections;
            value.Outcome = outcome.ToString();
            value.CompletedUtc = DateTime.UtcNow.ToString("o");
            value.Events.Add(new EvidenceEvent { Utc = value.CompletedUtc, Stage = Stage.Finished.ToString(), Action = "WorkerStopped" });
        }

        private static bool ValidCounts(RunRecord value, TradingMode mode)
        {
            return value.LoginSubmissions >= 0 && value.LoginSubmissions <= 1 &&
                value.SimulationSelections >= 0 && value.SimulationSelections <= 1 &&
                value.LiveSelections >= 0 && value.LiveSelections <= 1 &&
                (mode == TradingMode.Simulation ? value.LiveSelections == 0 : value.SimulationSelections == 0);
        }

        private static LoginResult Result(RunRecord value, Outcome outcome, TradingMode mode)
        {
            return new LoginResult
            {
                Outcome = outcome,
                RequestedMode = mode,
                LoginSubmissions = value.LoginSubmissions,
                SimulationSelections = value.SimulationSelections,
                LiveSelections = value.LiveSelections,
                ActionCompletionUncertain = value.ActionCompletionUncertain
            };
        }
        private bool Persist()
        {
            try
            {
                JsonFiles.Write(FilePath, record);
                return true;
            }
            catch { return false; }
        }
        public static string LastStage(string path)
        {
            try
            {
                List<EvidenceEvent> events = JsonFiles.Read<RunRecord>(path).Events;
                return events == null || events.Count == 0 ? null : events[events.Count - 1].Stage;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (SerializationException) { return null; }
            catch (System.Xml.XmlException) { return null; }
        }
        public static string HashFile(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            }
        }
    }
}
