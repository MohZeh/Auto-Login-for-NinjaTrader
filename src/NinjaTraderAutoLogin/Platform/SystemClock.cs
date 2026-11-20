using System;
using System.Threading;

namespace NinjaTraderAutoLogin
{
    internal sealed class SystemClock : IClock
    {
        public TimeSpan Now
        {
            get
            {
                return MonotonicClock.Now;
            }
        }
        public void Wait(CancellationToken cancellation)
        {
            if (cancellation.WaitHandle.WaitOne(400))
            {
                cancellation.ThrowIfCancellationRequested();
            }
        }
    }
}
