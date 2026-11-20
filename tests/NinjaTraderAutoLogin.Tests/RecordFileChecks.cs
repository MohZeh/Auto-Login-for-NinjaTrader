using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NinjaTraderAutoLogin;

internal static partial class TestRunner
{
    private static void RunRecordFileChecks()
    {
        Test("A record that is being read can still be replaced", delegate
        {
            WithRecordFolder(path =>
            {
                JsonFiles.Write(path, RecordWith("First"));
                using (Stream reader = JsonFiles.OpenRead(path))
                {
                    JsonFiles.Write(path, RecordWith("Second"));
                }

                Require(JsonFiles.Read<RunRecord>(path).Events[0].Action == "Second");
            });
        });
        Test("The progress read never stops the worker's record writes", delegate
        {
            WithRecordFolder(path =>
            {
                JsonFiles.Write(path, RecordWith("Started"));
                bool done = false;
                int reads = 0;
                var reader = new Thread(delegate ()
                {
                    while (!Volatile.Read(ref done))
                    {
                        RunEvidence.LastStage(path);
                        reads++;
                    }
                });
                reader.Start();
                try
                {
                    // The same pair as a sign-in: the app reads the stage while the worker writes.
                    for (int write = 0; write < 200; write++)
                    {
                        JsonFiles.Write(path, RecordWith("Observed" + write));
                    }
                }
                finally
                {
                    Volatile.Write(ref done, true);
                    reader.Join();
                }

                Require(reads > 0 && JsonFiles.Read<RunRecord>(path).Events[0].Action == "Observed199");
            });
        });
        Test("A short hold by another program is waited out", delegate
        {
            WithRecordFolder(path =>
            {
                JsonFiles.Write(path, RecordWith("First"));
                var holder = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                TimerCallback dispose = delegate
                {
                    holder.Dispose();
                };
                var release = new Timer(dispose, null, 150, Timeout.Infinite);
                try
                {
                    JsonFiles.Write(path, RecordWith("Second"));
                }
                finally
                {
                    release.Dispose();
                    holder.Dispose();
                }

                Require(JsonFiles.Read<RunRecord>(path).Events[0].Action == "Second");
            });
        });
        Test("A hold that does not end fails within a bounded time and leaves no temporary file", delegate
        {
            WithRecordFolder(path =>
            {
                JsonFiles.Write(path, RecordWith("First"));
                var elapsed = Stopwatch.StartNew();
                bool failed = false;
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    try
                    {
                        JsonFiles.Write(path, RecordWith("Second"));
                    }
                    catch (IOException) { failed = true; }
                    catch (UnauthorizedAccessException) { failed = true; }
                }

                Require(failed && elapsed.Elapsed < TimeSpan.FromSeconds(5));
                Require(Directory.GetFiles(Path.GetDirectoryName(path), "*.tmp").Length == 0);
                Require(JsonFiles.Read<RunRecord>(path).Events[0].Action == "First");
            });
        });
    }

    private static RunRecord RecordWith(string action)
    {
        var record = new RunRecord();
        record.Events.Add(new EvidenceEvent { Stage = action, Action = action });
        return record;
    }

    private static void WithRecordFolder(Action<string> body)
    {
        string folder = Path.Combine(Path.GetTempPath(), "AutoLogin-records-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            body(Path.Combine(folder, "run-test.json"));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
