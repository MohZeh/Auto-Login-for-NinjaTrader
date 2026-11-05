using System;
using System.Diagnostics;
using System.IO;

namespace NinjaTraderAutoLogin
{
    // One look at the running copies. NinjaTrader can be unreadable or doubled for
    // a few seconds while it starts or exits, so callers wait before treating this as a failure.
    internal sealed class ProcessScan
    {
        public ProcessIdentity Process;
        public bool Multiple;
        public Exception Unreadable;

        public bool Unsettled
        {
            get
            {
                return Multiple || Unreadable != null;
            }
        }
        public LoginProblem Problem()
        {
            return Multiple ? new LoginProblem(Outcome.MultipleInstances) : new LoginProblem(Outcome.AccessDenied, Unreadable);
        }
        public static ProcessScan Find(string path)
        {
            var scan = new ProcessScan();
            foreach (Process process in System.Diagnostics.Process.GetProcessesByName("NinjaTrader"))
            {
                using (process)
                {
                    try
                    {
                        ProcessIdentity identity = ProcessIdentity.From(process, path);
                        if (identity != null)
                        {
                            scan.Multiple = scan.Process != null;
                            scan.Process = identity;
                        }
                    }
                    catch (ArgumentException) { }
                    catch (InvalidOperationException) { }
                    catch (System.ComponentModel.Win32Exception error) { scan.Unreadable = error; }
                }
            }
            return scan;
        }
    }

    internal sealed class ProcessIdentity
    {
        private const int NoModules = 299; // ERROR_PARTIAL_COPY
        private static readonly int CurrentSession = ReadCurrentSession();
        private static int ReadCurrentSession()
        {
            using (Process current = Process.GetCurrentProcess())
            {
                return current.SessionId;
            }
        }
        public int Id;
        public long StartedUtcTicks;
        public string ImagePath, Version;
        public string Key
        {
            get
            {
                return Id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + StartedUtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        public static ProcessIdentity From(Process process, string expectedPath)
        {
            if (process.SessionId != CurrentSession)
            {
                return null;
            }

            string path = ImagePathOf(() => process.MainModule.FileName);
            if (!String.Equals(Path.GetFullPath(path), Path.GetFullPath(expectedPath), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new ProcessIdentity
            {
                Id = process.Id,
                StartedUtcTicks = process.StartTime.ToUniversalTime().Ticks,
                ImagePath = Path.GetFullPath(path),
                Version = FileVersionInfo.GetVersionInfo(path).FileVersion
            };
        }
        // While a process starts or exits it can have no modules. The framework then returns a
        // module object whose FileName throws NullReferenceException. Report this like the other
        // unreadable moments, so callers wait until NinjaTrader has settled instead of failing.
        internal static string ImagePathOf(Func<string> read)
        {
            try
            {
                string path = read();
                if (path == null)
                {
                    throw new System.ComponentModel.Win32Exception(NoModules);
                }

                return path;
            }
            catch (NullReferenceException error)
            {
                throw new System.ComponentModel.Win32Exception(NoModules, error.GetType().Name);
            }
        }
        public void Verify()
        {
            try
            {
                using (Process process = Process.GetProcessById(Id))
                {
                    if (process.HasExited || process.StartTime.ToUniversalTime().Ticks != StartedUtcTicks)
                    {
                        throw new LoginProblem(Outcome.ProcessExited);
                    }

                    ProcessIdentity current;
                    try
                    {
                        current = From(process, ImagePath);
                    }
                    catch (System.ComponentModel.Win32Exception error) when (error.NativeErrorCode == NoModules && !process.HasExited)
                    {
                        // While NinjaTrader loads its workspace its module list can be briefly
                        // unreadable. Its id and start time still identify it, so this is not an exit.
                        return;
                    }

                    if (current == null || current.StartedUtcTicks != StartedUtcTicks || process.HasExited)
                    {
                        throw new LoginProblem(Outcome.ProcessExited);
                    }
                }
            }
            catch (LoginProblem) { throw; }
            catch (System.ComponentModel.Win32Exception error) when (error.NativeErrorCode == NoModules) { throw new LoginProblem(Outcome.ProcessExited, error); }
            catch (System.ComponentModel.Win32Exception error) { throw new LoginProblem(Outcome.AccessDenied, error); }
            catch (ArgumentException error) { throw new LoginProblem(Outcome.ProcessExited, error); }
            catch (InvalidOperationException error) { throw new LoginProblem(Outcome.ProcessExited, error); }
        }
    }
}
