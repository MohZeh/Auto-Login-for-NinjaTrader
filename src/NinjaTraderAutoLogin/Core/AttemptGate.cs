using System;
using System.Collections.Generic;

namespace NinjaTraderAutoLogin
{
    // Shared by watcher and launcher. A failed process is never retried by polling.
    internal sealed class AttemptGate
    {
        private readonly HashSet<string> attempted = new HashSet<string>(StringComparer.Ordinal);
        private bool busy;
        public bool CanWatch(string processKey)
        {
            lock (attempted)
            {
                return !busy && !attempted.Contains(processKey);
            }
        }
        public bool Begin(string processKey, bool explicitRequest)
        {
            lock (attempted)
            {
                if (busy || (!explicitRequest && attempted.Contains(processKey)))
                {
                    return false;
                }

                busy = true;
                attempted.Add(processKey);
                return true;
            }
        }
        public void End()
        {
            lock (attempted)
            {
                busy = false;
            }
        }
    }
}
