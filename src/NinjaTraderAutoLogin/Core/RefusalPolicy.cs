namespace NinjaTraderAutoLogin
{
    // What is recorded about a password NinjaTrader refused. Only these values are stored.
    internal enum RefusalState
    {
        None = 0,
        Rejected = 1,
        Uncertain = 2
    }

    // One reading of the registry record: a known state, or a record that could not be read.
    // "Could not be read" is never stored; the next successful reading replaces it.
    internal struct RefusalReading
    {
        public bool Readable;
        public RefusalState State;

        public static readonly RefusalReading Unavailable = new RefusalReading { Readable = false };

        public static RefusalReading Known(RefusalState state)
        {
            return new RefusalReading { Readable = true, State = state };
        }
    }

    // What the app can truthfully say after it tried to clear the registry record and could not
    // confirm it: the record is clear after all, a refusal is still held, or nothing is known.
    internal enum FailedClear
    {
        Cleared,
        Kept,
        Unchecked
    }

    // The rules for holding and lifting a refusal, kept apart from storage so they can be tested.
    // They guard only the password: an environment screen that is already signed in needs no
    // password and is not held back by a refusal.
    internal static class RefusalPolicy
    {
        // A certain refusal outranks an uncertain one, and either outranks none.
        public static RefusalState Combine(RefusalState first, RefusalState second)
        {
            if (first == RefusalState.Rejected || second == RefusalState.Rejected)
            {
                return RefusalState.Rejected;
            }

            return first == RefusalState.Uncertain || second == RefusalState.Uncertain ? RefusalState.Uncertain : RefusalState.None;
        }

        public static RefusalState Of(Settings settings)
        {
            return !settings.CredentialsRejected ? RefusalState.None : settings.RejectionUncertain ? RefusalState.Uncertain : RefusalState.Rejected;
        }

        // At startup the app holds any refusal either record kept. A record that cannot be read
        // is not turned into a refusal here; the worker reads it again before any password use.
        public static RefusalState AtStartup(RefusalState file, RefusalReading registry)
        {
            return Combine(file, registry.Readable ? registry.State : RefusalState.None);
        }

        // Right before the password is read. A known refusal in any record stops it, and so
        // does a registry record that cannot be read.
        public static void RequireNoRefusal(bool parentRejected, bool fileRejected, RefusalReading registry)
        {
            if (parentRejected || fileRejected || (registry.Readable && registry.State != RefusalState.None))
            {
                throw new LoginProblem(Outcome.CredentialsRejected);
            }

            if (!registry.Readable)
            {
                throw new LoginProblem(Outcome.RefusalUnavailable);
            }
        }

        // Before the password is sent, the worker records the attempt in the registry as
        // uncertain, and sends nothing if it cannot. After the run, this says what the records
        // must hold. Null means no password left in this run, or the record of the run could not
        // be read; the registry then decides, because the attempt record is written first.
        public static RefusalState? AfterRun(Outcome outcome, bool concluded, int submitsStarted, int submissions, bool modeReached)
        {
            if (submitsStarted == 0 && submissions == 0)
            {
                return null;
            }

            if (concluded && submissions > 0 && outcome == Outcome.AuthenticationFailed)
            {
                return RefusalState.Rejected;
            }

            if (concluded && submissions > 0 && outcome == Outcome.LoginNotAccepted)
            {
                return RefusalState.Uncertain;
            }

            // The environment screen appears only after NinjaTrader accepted the password.
            if (submissions > 0 && modeReached)
            {
                return RefusalState.None;
            }

            // A NinjaTrader message after the send is not an answer to the password, and it may
            // be the refusal itself, so the next attempt asks first.
            if (submissions > 0 && outcome == Outcome.NeedsManual)
            {
                return RefusalState.Uncertain;
            }

            // The password may have left, but no answer to it was seen to the end.
            if (!concluded || submitsStarted > submissions)
            {
                return RefusalState.Uncertain;
            }

            return RefusalState.None;
        }

        // A refusal is lifted only when both records are cleared and confirmed.
        public static bool Lifted(bool fileSaved, bool registryCleared)
        {
            return fileSaved && registryCleared;
        }

        // A new password was saved, which clears the settings file, but the registry record could
        // not be confirmed clear. What remains is what the registry still holds; if it cannot be
        // read, the refusal known before the save is kept. No refusal is invented.
        public static RefusalState KeptAfterNewPassword(RefusalState before, RefusalReading registry)
        {
            return registry.Readable ? registry.State : before;
        }

        // A held refusal is reported as such. Otherwise an unreadable record is reported as
        // unchecked rather than as a plain success, without inventing a refusal.
        public static FailedClear AfterFailedClear(RefusalState kept, RefusalReading registry)
        {
            if (kept != RefusalState.None)
            {
                return FailedClear.Kept;
            }

            return registry.Readable ? FailedClear.Cleared : FailedClear.Unchecked;
        }
    }
}
