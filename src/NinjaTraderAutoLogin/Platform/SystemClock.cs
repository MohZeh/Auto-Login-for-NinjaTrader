using System;
using System.Threading;

namespace NinjaTraderAutoLogin
{
    internal sealed class SystemClock : IClock
    {
        public DateTime UtcNow
        {
            get
            {
                return DateTime.UtcNow;
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
