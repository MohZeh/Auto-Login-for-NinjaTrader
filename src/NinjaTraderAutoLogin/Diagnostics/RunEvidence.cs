using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
        [DataMember(EmitDefaultValue = false)] public List<WindowEvidence> Windows;
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
        // Where the error came from, as method names only, for example
        // "Process.GetProcessById < NativeLoginUi.Windows".
        [DataMember(EmitDefaultValue = false)] public string CauseSite;
        // How many NinjaTrader processes were running when NinjaTrader seemed to close.
        [DataMember(EmitDefaultValue = false)] public int? NinjaTraderInstancesAtStop;
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
                foreach (WindowEvidence item in snapshot.Windows)
                {
                    signature.Append('|').Append(item.ClassName).Append(item.AutomationId).Append(item.Modal).Append(item.Offscreen).Append(item.Nested);
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
                Controls = snapshot == null ? null : snapshot.Controls,
                Windows = snapshot == null || snapshot.Windows.Count == 0 ? null : snapshot.Windows
            });
            return Persist();
        }
        public void Cause(Exception error)
        {
            ApplyCause(record, error);
        }

        // Type, code and method names only: exception messages can contain paths or other private text.
        internal static void ApplyCause(RunRecord value, Exception error)
        {
            if (error != null)
            {
                value.CauseType = error.GetType().FullName;
                value.CauseHResult = error.HResult;
                value.CauseSite = SiteOf(error);
            }
        }

        // The method that threw, and the first method of this app on the way there. No file
        // names or line numbers are read.
        internal static string SiteOf(Exception error)
        {
            StackFrame[] frames = new StackTrace(error, false).GetFrames();
            if (frames == null || frames.Length == 0)
            {
                return null;
            }

            string thrown = MethodName(frames[0].GetMethod());
            Assembly app = typeof(RunEvidence).Assembly;
            foreach (StackFrame frame in frames)
            {
                MethodBase method = frame.GetMethod();
                if (method != null && method.DeclaringType != null && method.DeclaringType.Assembly == app)
                {
                    string ours = MethodName(method);
                    string site = ours == thrown ? thrown : thrown + " < " + ours;
                    return site.Length > 200 ? site.Substring(0, 200) : site;
                }
            }

            return thrown.Length > 200 ? thrown.Substring(0, 200) : thrown;
        }

        private static string MethodName(MethodBase method)
        {
            if (method == null)
            {
                return "?";
            }

            return method.DeclaringType == null ? method.Name : method.DeclaringType.Name + "." + method.Name;
        }

        // Records how many NinjaTrader processes were running when the target seemed to close,
        // to tell an exit from a restart or a second copy. Only for a record this object owns.
        public bool Instances(int count)
        {
            record.NinjaTraderInstancesAtStop = count;
            return Persist();
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

            return Result(value, outcome, mode, true);
        }

        public LoginResult Interrupt(Outcome outcome, TradingMode mode, Exception cause = null)
        {
            try
            {
                RunRecord value = ReadRecord(FilePath, RunId, mode);
                UpdateInterrupted(value, outcome);
                ApplyCause(value, cause);
                JsonFiles.Write(FilePath, value);
                return Result(value, outcome, mode, false);
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

        internal static LoginResult Result(RunRecord value, Outcome outcome, TradingMode mode, bool concluded)
        {
            return new LoginResult
            {
                Outcome = outcome,
                RequestedMode = mode,
                LoginSubmissions = value.LoginSubmissions,
                SimulationSelections = value.SimulationSelections,
                LiveSelections = value.LiveSelections,
                ActionCompletionUncertain = value.ActionCompletionUncertain,
                SubmitsStarted = Count(value, "BeforeSubmit"),
                ModeReached = Count(value, "BeforeSimulation") + Count(value, "BeforeLive") > 0,
                Concluded = concluded
            };
        }
        private bool Persist()
        {
            try
            {
                JsonFiles.Write(FilePath, record);
                return true;
            }
            catch (Exception error)
            {
                // A failed write stops the sign-in as evidence unavailable; keep its type for later.
                AppFaults.Record("PersistRunEvidence", error);
                return false;
            }
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
