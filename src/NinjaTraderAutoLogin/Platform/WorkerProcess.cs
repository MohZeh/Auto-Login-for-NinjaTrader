using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NinjaTraderAutoLogin
{
    internal enum WorkerStopReason
    {
        Completed, Cancelled, TimedOut, Failed
    }

    internal sealed class WorkerProcessResult
    {
        public WorkerStopReason Reason;
        public int ProcessId;
        public byte[] Response;
    }

    internal static class WorkerFrames
    {
        // Both directions fit inside an anonymous pipe's buffer. No credential
        // values are part of the protocol or command line.
        public const int MaximumBytes = 2048;

        public static byte[] Serialize<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                if (stream.Length > MaximumBytes)
                {
                    throw new InvalidDataException("WorkerFrameTooLarge");
                }

                return stream.ToArray();
            }
        }

        public static T Deserialize<T>(byte[] value)
        {
            if (value == null || value.Length > MaximumBytes)
            {
                throw new InvalidDataException("InvalidWorkerFrame");
            }

            using (var stream = new MemoryStream(value, false))
            {
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
            }
        }

        public static void Write(Stream stream, byte[] value)
        {
            if (value == null || value.Length > MaximumBytes)
            {
                throw new InvalidDataException("WorkerFrameTooLarge");
            }

            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write(value.Length);
                writer.Write(value);
                writer.Flush();
            }
        }

        public static byte[] Read(Stream stream)
        {
            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
            {
                int length = reader.ReadInt32();
                if (length < 1 || length > MaximumBytes)
                {
                    throw new InvalidDataException("InvalidWorkerFrameLength");
                }

                byte[] value = reader.ReadBytes(length);
                if (value.Length != length)
                {
                    throw new EndOfStreamException("IncompleteWorkerFrame");
                }

                return value;
            }
        }

        public static byte[] ReadRequest(string pipeHandle)
        {
            using (var input = new AnonymousPipeClientStream(PipeDirection.In, pipeHandle))
            {
                return Read(input);
            }
        }
    }

    internal static class WorkerProcess
    {
        public static WorkerProcessResult Run(string executable, string argument, byte[] request,
            TimeSpan timeout, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (request == null || request.Length > WorkerFrames.MaximumBytes)
            {
                throw new InvalidDataException("InvalidWorkerRequest");
            }

            var result = new WorkerProcessResult { Reason = WorkerStopReason.Failed };
            using (WorkerJob job = WorkerJob.Create())
            // Process.StandardInput writes the console encoding's preamble before
            // any request byte, so the request has its own binary channel.
            using (var requestPipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable))
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo(executable, argument + " " + requestPipe.GetClientHandleAsString())
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    WorkingDirectory = Path.GetDirectoryName(executable)
                };
                Task<byte[]> response = null;
                try
                {
                    if (!process.Start())
                    {
                        return result;
                    }

                    result.ProcessId = process.Id;
                    requestPipe.DisposeLocalCopyOfClientHandle();
                    // Attach before sending work; parent exit closes the job and
                    // cannot leave an automation child acting on its own.
                    job.Attach(process);
                    response = Task.Run(delegate
                    {
                        return WorkerFrames.Read(process.StandardOutput.BaseStream);
                    });
                    // Observe protocol-reader faults even if cancellation wins.
                    response.ContinueWith(task => { var ignored = task.Exception; },
                        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                    WorkerFrames.Write(requestPipe, request);
                    requestPipe.Dispose();
                    var elapsed = Stopwatch.StartNew();
                    while (!process.WaitForExit(50))
                    {
                        if (cancellation.IsCancellationRequested)
                        {
                            if (response.Status == TaskStatus.RanToCompletion)
                            {
                                result.Response = response.Result;
                            }

                            Stop(process);
                            result.Reason = WorkerStopReason.Cancelled;
                            return result;
                        }
                        if (elapsed.Elapsed >= timeout)
                        {
                            if (response.Status == TaskStatus.RanToCompletion)
                            {
                                result.Response = response.Result;
                            }

                            Stop(process);
                            result.Reason = WorkerStopReason.TimedOut;
                            return result;
                        }
                    }
                    if (process.ExitCode != 0 || !response.Wait(TimeSpan.FromSeconds(2)))
                    {
                        return result;
                    }

                    result.Response = response.Result;
                    result.Reason = WorkerStopReason.Completed;
                    return result;
                }
                catch (Exception error) when (error is IOException || error is InvalidOperationException ||
                    error is AggregateException || error is System.ComponentModel.Win32Exception)
                {
                    return result;
                }
                finally
                {
                    if (result.ProcessId != 0)
                    {
                        Stop(process);
                    }
                }
            }
        }

        private static void Stop(Process process)
        {
            if (process.HasExited)
            {
                return;
            }

            try
            {
                process.Kill();
            }
            catch (InvalidOperationException)
            {
                if (!process.HasExited)
                {
                    throw;
                }
            }
            if (!process.WaitForExit(2000))
            {
                throw new InvalidOperationException("WorkerDidNotExit");
            }
        }
    }
}
