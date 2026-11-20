using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;

namespace NinjaTraderAutoLogin
{
    internal enum LaunchMode
    {
        Shortcut, Watch
    }

    internal enum TradingMode
    {
        Simulation, Live
    }

    internal enum Outcome
    {
        Success, AlreadyOpen, AuthenticationFailed, NeedsManual, UnsupportedUi,
        TimedOut, Cancelled, ProcessExited, CredentialsMissing, ModeNotConfirmed,
        EvidenceUnavailable, AccessDenied, UnexpectedFailure, LiveAccountUnavailable, MultipleInstances, LoginNotAccepted, UntrustedTarget, LiveVersionNotConfirmed, CredentialsRejected, RefusalNotCleared, RefusalUnavailable
    }

    internal enum Stage
    {
        WaitingForLogin, WaitingForTradingMode, WaitingForControlCenter, Finished
    }

    internal sealed class LoginProblem : Exception
    {
        public readonly Outcome Outcome;
        public LoginProblem(Outcome outcome) : base(outcome.ToString()) { Outcome = outcome; }
        public LoginProblem(Outcome outcome, Exception cause) : base(outcome.ToString(), cause) { Outcome = outcome; }
    }

    internal sealed class Secret : IDisposable
    {
        public string Username;
        public string Password;
        public Secret(string username, string password)
        {
            Username = username;
            Password = password;
        }
        public bool Valid
        {
            get
            {
                return !String.IsNullOrWhiteSpace(Username) && !String.IsNullOrEmpty(Password);
            }
        }
        public void Dispose()
        {
            Username = null;
            Password = null;
        }
    }

    internal interface ICredentials
    {
        Secret Read();
    }

    // Now only moves forward and has no meaning as a date, so a change to the Windows clock
    // cannot shorten or stretch a stage deadline.
    internal interface IClock
    {
        TimeSpan Now
        {
            get;
        }
        void Wait(CancellationToken cancellation);
    }

    [DataContract]
    internal sealed class ControlEvidence
    {
        [DataMember] public string AutomationId;
        [DataMember] public string ControlType;
        [DataMember] public string ClassName;
        [DataMember] public bool Enabled;
        [DataMember] public bool Offscreen;
        [DataMember] public bool IsPassword;
        [DataMember] public bool ValuePattern;
        [DataMember] public bool InvokePattern;
    }

    // A window of the target, by type only. Titles and text are never read.
    [DataContract]
    internal sealed class WindowEvidence
    {
        [DataMember] public string ClassName;
        [DataMember] public string AutomationId;
        [DataMember] public bool Modal;
        [DataMember] public bool Offscreen;
        [DataMember] public bool Nested;
    }

    internal sealed class UiSnapshot
    {
        public bool LoginReady;
        public bool SimulationReady;
        public bool LiveReady;
        public bool LiveAccountUnavailable;
        public bool ControlCenter;
        public bool AuthenticationError;
        public bool ManualRequired;
        // A modal dialog owned by a NinjaTrader window, while no known screen is ready.
        public bool OwnedDialog;
        public bool Ambiguous;
        public List<ControlEvidence> Controls = new List<ControlEvidence>();
        public List<WindowEvidence> Windows = new List<WindowEvidence>();
    }

    internal interface ILoginUi
    {
        UiSnapshot Inspect();
        void Submit(Secret credentials, CancellationToken cancellation);
        void SelectMode(TradingMode mode, CancellationToken cancellation);
    }

    internal interface IRunEvidence
    {
        bool Event(Stage stage, string action, UiSnapshot snapshot);
        void Cause(Exception error);
        bool Complete(Outcome outcome, int loginSubmissions, int simulationSelections, int liveSelections = 0);
    }

    internal sealed class LoginResult
    {
        public Outcome Outcome;
        public int LoginSubmissions;
        public int SimulationSelections;
        public int LiveSelections;
        public TradingMode RequestedMode;
        public bool ActionCompletionUncertain;
        // What the run record shows about the password, for the attempt record.
        public int SubmitsStarted;
        public bool ModeReached;
        // True only when the worker itself saw the run to its end and reported it.
        public bool Concluded;
    }

    internal sealed class DisposeOnce
    {
        private int disposed;
        public bool TryBegin()
        {
            return Interlocked.Exchange(ref disposed, 1) == 0;
        }
    }
}
