using System.IO;

namespace NinjaTraderAutoLogin
{
    // What the app holds after a run, whether the settings file must record it, and whether
    // the registry already holds it.
    internal struct RefusalUpdate
    {
        public RefusalState Hold;
        public bool Save;
        public bool Latched;
    }

    // Applies a finished run to the registry record, which the worker wrote as uncertain
    // before any password left. A refusal outranks the attempt record, a known answer clears
    // it, and anything unknown keeps it, so the next attempt asks first.
    internal static class RefusalRecords
    {
        public static RefusalUpdate AfterRun(RefusalState held, LoginResult result, RejectionLatch latch)
        {
            RefusalState? next = RefusalPolicy.AfterRun(result.Outcome, result.Concluded, result.SubmitsStarted, result.LoginSubmissions, result.ModeReached);
            RefusalState hold;
            if (next == null)
            {
                // Catch up with what the worker may have left, without inventing anything.
                RefusalReading left = latch.Read();
                hold = RefusalPolicy.Combine(held, left.Readable ? left.State : RefusalState.None);
                return new RefusalUpdate { Hold = hold, Save = hold != held, Latched = hold != held };
            }

            if (next.Value == RefusalState.None)
            {
                if (latch.Write(RefusalState.None))
                {
                    return new RefusalUpdate { Hold = held };
                }

                // The worker will read the record that stayed, so the app holds it too.
                AppFaults.Record("ClearAttemptRecord", new IOException("AttemptRecordKept"));
                hold = RefusalPolicy.Combine(held, RefusalState.Uncertain);
                return new RefusalUpdate { Hold = hold, Save = hold != held, Latched = true };
            }

            // If this write fails, the attempt record still holds the password back as uncertain.
            return new RefusalUpdate { Hold = next.Value, Save = true, Latched = latch.Write(next.Value) };
        }
    }
}
