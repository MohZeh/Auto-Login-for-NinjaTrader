using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;

namespace NinjaTraderAutoLogin
{
    [DataContract]
    internal sealed class AppFaultRecord
    {
        [DataMember] public string Candidate, Utc, Phase, ExceptionType;
        [DataMember] public int HResult;
        [DataMember] public List<string> Frames = new List<string>();
        [DataMember] public string CauseExceptionType;
        [DataMember] public List<string> RecoveryFailures = new List<string>();
    }

    internal static class AppFaults
    {
        public static AppFaultRecord Create(string phase, Exception error)
        {
            var record = new AppFaultRecord
            {
                Candidate = BuildInfo.Version,
                Utc = DateTime.UtcNow.ToString("o"),
                Phase = phase,
                ExceptionType = error.GetType().FullName,
                HResult = error.HResult
            };
            var configuration = error as ConfigurationSaveException;
            if (configuration != null)
            {
                record.CauseExceptionType = configuration.InnerException == null ? null : configuration.InnerException.GetType().FullName;
                record.RecoveryFailures.AddRange(configuration.RecoveryFailures);
            }
            foreach (StackFrame frame in new StackTrace(error, false).GetFrames() ?? new StackFrame[0])
            {
                var method = frame.GetMethod();
                if (method == null || method.DeclaringType == null)
                {
                    continue;
                }

                string name = method.DeclaringType.FullName + "." + method.Name;
                if ((name.StartsWith("NinjaTraderAutoLogin.", StringComparison.Ordinal) || name.StartsWith("System.Windows.Forms.", StringComparison.Ordinal)) && record.Frames.Count < 16)
                {
                    record.Frames.Add(name);
                }
            }
            return record;
        }
        public static void Record(string phase, Exception error)
        {
            try
            {
                AppFaultRecord record = Create(phase, error);
                JsonFiles.Write(Path.Combine(Paths.Evidence, "app-fault-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json"), record);
            }
            catch { }
        }
    }
}
