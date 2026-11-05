using System;
using System.ComponentModel;
using System.Diagnostics;

namespace NinjaTraderAutoLogin
{
    // Asks Windows whether one known process has ended. Unlike a scan, this does not list
    // processes or read anything from NinjaTrader, and only one instance of it can run.
    internal sealed class ProcessWatch : IDisposable
    {
        private readonly Process process;

        private ProcessWatch(Process process)
        {
            this.process = process;
        }
        // Null when the process cannot be held, so the caller simply keeps scanning.
        public static ProcessWatch Open(ProcessIdentity copy)
        {
            Process process = null;
            try
            {
                process = Process.GetProcessById(copy.Id);
                // Holding the handle ties this object to this process, even if its id is reused later.
                IntPtr handle = process.Handle;
                if (handle != IntPtr.Zero && process.StartTime.ToUniversalTime().Ticks == copy.StartedUtcTicks)
                {
                    return new ProcessWatch(process);
                }
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { }
            if (process != null)
            {
                process.Dispose();
            }

            return null;
        }
        public bool HasEnded
        {
            get
            {
                try
                {
                    return process.HasExited;
                }
                catch (InvalidOperationException) { return true; }
                catch (Win32Exception) { return true; }
            }
        }
        public void Dispose()
        {
            process.Dispose();
        }
    }
}
