using System;
using System.Diagnostics;

namespace NinjaTraderAutoLogin
{
    // Time that only moves forward, for deadlines, grace periods and pauses. A change to the
    // Windows clock cannot shorten or stretch a wait. It has no meaning as a date.
    internal static class MonotonicClock
    {
        private static readonly Stopwatch Watch = Stopwatch.StartNew();

        public static TimeSpan Now
        {
            get
            {
                return Watch.Elapsed;
            }
        }
    }
}
