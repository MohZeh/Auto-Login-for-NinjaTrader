using System;

namespace NinjaTraderAutoLogin
{
    // A state only becomes a failure after it has lasted this long without clearing.
    internal sealed class ProblemGrace
    {
        private readonly TimeSpan span;
        private DateTime? first;

        public ProblemGrace(TimeSpan span)
        {
            this.span = span;
        }
        public bool Expired(DateTime now)
        {
            if (!first.HasValue)
            {
                first = now;
            }

            return now - first.Value >= span;
        }
        public void Clear()
        {
            first = null;
        }
    }
}
