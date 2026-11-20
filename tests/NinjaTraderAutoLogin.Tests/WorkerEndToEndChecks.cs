using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using Microsoft.Win32;
using NinjaTraderAutoLogin;

// What the TestHooks build's --test-parent command writes after one sign-in. The serializer
// fills the fields; the initial values only keep the compiler from calling them unused.
[DataContract]
internal sealed class EndToEndResult
{
    [DataMember] public string Outcome = null, Hold = null;
    [DataMember] public int LoginSubmissions = 0, SimulationSelections = 0;
    [DataMember] public bool Concluded = false, Save = false, Latched = false, Saved = false;
}

// Runs a sign-in through the TestHooks build as the parent, its real worker process and
// Windows Credential Manager, against the fake target on a private desktop. The TestHooks
// build uses its own data folder, registry key and Credential Manager name, so a real
// password, settings file or refusal record is never touched.
internal static partial class TestRunner
{
    // Must match TestHooks in the application. Never the release name.
    private const string EndToEndCredentialTarget = "NinjaTraderAutoLogin.Tests/worker";
    private const string EndToEndLatchKey = TestRegistryRoot + @"\Worker";
    private const string EndToEndUser = "vault-user-1", EndToEndPassword = "vault-pass-123";
    private static readonly string EndToEndLogin = "login " + EndToEndUser.Length + " " + EndToEndPassword.Length;
    private static string endToEndData, endToEndDesktop, endToEndHooks, endToEndTarget;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TestCredential
    {
        public uint Flags, Type;
        public string TargetName, Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint BlobSize;
        public IntPtr Blob;
        public uint Persist, AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias, UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref TestCredential credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    private sealed class EndToEndRun
    {
        public EndToEndResult Result;
        public string[] Actions = new string[0];
        public bool ParentStopped;
    }

    private static int RunWorkerEndToEnd(string hooks, string target, string reportFolder)
    {
        endToEndHooks = Path.GetFullPath(hooks);
        endToEndTarget = Path.GetFullPath(target);
        endToEndData = Path.Combine(Path.GetTempPath(), "AutoLogin-e2e-" + Guid.NewGuid().ToString("N"));
        endToEndDesktop = "AutoLoginE2E" + Guid.NewGuid().ToString("N").Substring(0, 8);
        IntPtr desktop = FakeTargetChecks.CreatePrivateDesktop(endToEndDesktop);
        if (desktop == IntPtr.Zero)
        {
            Console.WriteLine("Private desktop unavailable: " + Marshal.GetLastWin32Error());
            return 2;
        }

        Directory.CreateDirectory(endToEndData);
        // The worker's registry record deletes its key when cleared, so the parent key must exist.
        Registry.CurrentUser.CreateSubKey(TestRegistryRoot).Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(EndToEndLatchKey, false);
        try
        {
            Test("The release build has no test-parent command", delegate
            {
                string result = Path.Combine(endToEndData, "release.json");
                using (Process release = Process.Start(new ProcessStartInfo(Paths.Executable, "--test-parent \"" + result + "\" Simulation") { UseShellExecute = false }))
                {
                    Require(release.WaitForExit(30000) && release.ExitCode == 2 && !File.Exists(result));
                }
            });
            Test("The test build does not start without its test folder and target", delegate
            {
                string result = Path.Combine(endToEndData, "unset.json");
                var start = new ProcessStartInfo(endToEndHooks, "--test-parent \"" + result + "\" Simulation") { UseShellExecute = false };
                start.EnvironmentVariables.Remove("NTAL_TEST_DATA");
                start.EnvironmentVariables.Remove("NTAL_TEST_TARGET");
                using (Process unset = Process.Start(start))
                {
                    Require(unset.WaitForExit(30000) && unset.ExitCode == 4 && !File.Exists(result));
                }
            });

            Environment.SetEnvironmentVariable("NTAL_TEST_DATA", endToEndData);
            Environment.SetEnvironmentVariable("NTAL_TEST_TARGET", endToEndTarget);
            if (!WriteEndToEndCredential())
            {
                Console.WriteLine("Test credential unavailable: " + Marshal.GetLastWin32Error());
                return 2;
            }

            Test("A sign-in through the real worker uses the Credential Manager password and clears its attempt record", delegate
            {
                ResetEndToEnd();
                EndToEndRun run = RunThroughWorker("normal", null);
                Require(run.Result != null && run.Result.Outcome == "Success" && run.Result.Hold == "None");
                Require(run.Result.LoginSubmissions == 1 && run.Result.SimulationSelections == 1 && run.Result.Concluded);
                Require(SameActions(run.Actions, EndToEndLogin, "mode Simulation"));
                Require(EndToEndLatch() == null);
                RunRecord record = LatestEndToEndRecord();
                Require(record != null && record.Outcome == "Success" && record.Trigger == "TestParent" && record.CompletedUtc != null);
                Require(record.Events.Exists(item => item.Action == "BeforeSubmit") && record.Events.Exists(item => item.Action == "SimulationInvoked"));
            });
            Test("A refused password is kept in both records, and the next run never sends it", delegate
            {
                ResetEndToEnd();
                EndToEndRun refused = RunThroughWorker("rejected", null);
                Require(refused.Result != null && refused.Result.Outcome == "AuthenticationFailed" && refused.Result.Hold == "Rejected");
                Require(refused.Result.Save && refused.Result.Saved && refused.Result.Latched && SameActions(refused.Actions, EndToEndLogin));
                Require(EndToEndLatch() == (int)RefusalState.Rejected && SettingsFileRejected());
                EndToEndRun next = RunThroughWorker("normal", null);
                Require(next.Result != null && next.Result.Outcome == "CredentialsRejected" && next.Result.LoginSubmissions == 0);
                Require(next.Actions.Length == 0 && next.Result.Hold == "Rejected");
            });
            Test("A NinjaTrader message after the password left keeps the attempt uncertain, so the next run asks first", delegate
            {
                ResetEndToEnd();
                EndToEndRun dialog = RunThroughWorker("dialog-after-login", null);
                Require(dialog.Result != null && dialog.Result.Outcome == "NeedsManual" && dialog.Result.Hold == "Uncertain");
                Require(dialog.Result.Concluded && dialog.Result.Save && dialog.Result.Saved && SameActions(dialog.Actions, EndToEndLogin));
                Require(EndToEndLatch() == (int)RefusalState.Uncertain && SettingsFileRejected());
                EndToEndRun next = RunThroughWorker("normal", null);
                Require(next.Result != null && next.Result.Outcome == "CredentialsRejected" && next.Result.Hold == "Uncertain");
                Require(next.Actions.Length == 0 && next.Result.LoginSubmissions == 0);
            });
            Test("A parent that dies after the password left holds the next attempt back after a restart", delegate
            {
                ResetEndToEnd();
                // No screen follows the login here, so the worker is still waiting when the parent is stopped.
                EndToEndRun cut = RunThroughWorker("login-returns", actions => Array.IndexOf(actions, EndToEndLogin) >= 0);
                Require(cut.ParentStopped && cut.Result == null && SameActions(cut.Actions, EndToEndLogin));
                Require(WaitUntilNoWorker());
                Require(EndToEndLatch() == (int)RefusalState.Uncertain && !SettingsFileRejected());
                // A new parent is the restarted app: only the attempt record knows about the send.
                EndToEndRun next = RunThroughWorker("normal", null);
                Require(next.Result != null && next.Result.Outcome == "CredentialsRejected" && next.Result.Hold == "Uncertain");
                Require(next.Actions.Length == 0 && next.Result.LoginSubmissions == 0);
            });
        }
        finally
        {
            DeleteEndToEndCredential();
            Environment.SetEnvironmentVariable("NTAL_TEST_DATA", null);
            Environment.SetEnvironmentVariable("NTAL_TEST_TARGET", null);
            RemoveTestRegistryKey(EndToEndLatchKey);
            FakeTargetChecks.ClosePrivateDesktop(desktop);
            try
            {
                Directory.Delete(endToEndData, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        Directory.CreateDirectory(reportFolder);
        JsonFiles.Write(Path.Combine(reportFolder, "worker-end-to-end.json"), report);
        int failures = report.Checks.FindAll(check => !check.Passed).Count;
        Console.WriteLine(report.Checks.Count + " end-to-end worker checks; " + failures + " failures. They ran the TestHooks build, not the release executable.");
        return failures == 0 ? 0 : 1;
    }

    private static EndToEndRun RunThroughWorker(string scenario, Func<string[], bool> stopParentWhen)
    {
        string id = Guid.NewGuid().ToString("N");
        string record = Path.Combine(endToEndData, "target-" + id + ".log");
        string resultPath = Path.Combine(endToEndData, "result-" + id + ".json");
        var run = new EndToEndRun();
        Process fake = FakeTargetChecks.StartOn(endToEndDesktop, endToEndTarget, scenario + " \"" + record + "\"");
        try
        {
            using (Process parent = FakeTargetChecks.StartOn(endToEndDesktop, endToEndHooks, "--test-parent \"" + resultPath + "\" Simulation"))
            {
                var elapsed = Stopwatch.StartNew();
                while (!parent.WaitForExit(100))
                {
                    if (stopParentWhen != null && stopParentWhen(TargetActions(record)))
                    {
                        parent.Kill();
                        parent.WaitForExit(10000);
                        run.ParentStopped = true;
                        break;
                    }

                    if (elapsed.Elapsed > TimeSpan.FromMinutes(4))
                    {
                        parent.Kill();
                        throw new TimeoutException("TestParentDidNotFinish");
                    }
                }
            }

            run.Result = File.Exists(resultPath) ? JsonFiles.Read<EndToEndResult>(resultPath) : null;
            run.Actions = TargetActions(record);
            return run;
        }
        finally
        {
            try
            {
                if (!fake.HasExited)
                {
                    fake.Kill();
                    fake.WaitForExit(5000);
                }
            }
            catch (InvalidOperationException) { }
            fake.Dispose();
        }
    }

    // The fake target appends while it runs, so the log is read with write sharing.
    private static string[] TargetActions(string record)
    {
        try
        {
            using (var stream = new FileStream(record, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                var lines = new List<string>();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length > 0)
                    {
                        lines.Add(line);
                    }
                }

                return lines.ToArray();
            }
        }
        catch (FileNotFoundException) { return new string[0]; }
        catch (IOException) { return new string[0]; }
    }

    private static bool SameActions(string[] actions, params string[] expected)
    {
        if (actions.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (actions[index] != expected[index])
            {
                return false;
            }
        }

        return true;
    }

    // The job object ends the worker together with its parent.
    private static bool WaitUntilNoWorker()
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(15))
        {
            bool running = false;
            foreach (Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(endToEndHooks)))
            {
                try
                {
                    running |= String.Equals(process.MainModule.FileName, endToEndHooks, StringComparison.OrdinalIgnoreCase);
                }
                catch (System.ComponentModel.Win32Exception) { }
                catch (InvalidOperationException) { }
                finally { process.Dispose(); }
            }

            if (!running)
            {
                return true;
            }

            System.Threading.Thread.Sleep(200);
        }

        return false;
    }

    private static void ResetEndToEnd()
    {
        Registry.CurrentUser.DeleteSubKeyTree(EndToEndLatchKey, false);
        string settings = Path.Combine(endToEndData, "settings.json");
        if (File.Exists(settings))
        {
            File.Delete(settings);
        }
    }

    private static int? EndToEndLatch()
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(EndToEndLatchKey, false))
        {
            object value = key == null ? null : key.GetValue("CredentialsRejected", null);
            return value is int ? (int?)(int)value : null;
        }
    }

    private static bool SettingsFileRejected()
    {
        string settings = Path.Combine(endToEndData, "settings.json");
        return File.Exists(settings) && JsonFiles.Read<Settings>(settings).CredentialsRejected;
    }

    private static RunRecord LatestEndToEndRecord()
    {
        string folder = Path.Combine(endToEndData, "evidence");
        if (!Directory.Exists(folder))
        {
            return null;
        }

        string[] files = Directory.GetFiles(folder, "run-*.json");
        Array.Sort(files, StringComparer.Ordinal);
        return files.Length == 0 ? null : JsonFiles.Read<RunRecord>(files[files.Length - 1]);
    }

    private static bool WriteEndToEndCredential()
    {
        IntPtr blob = Marshal.StringToCoTaskMemUni(EndToEndPassword);
        try
        {
            var value = new TestCredential
            {
                Type = 1, // CRED_TYPE_GENERIC
                TargetName = EndToEndCredentialTarget,
                UserName = EndToEndUser,
                Blob = blob,
                BlobSize = (uint)Encoding.Unicode.GetByteCount(EndToEndPassword),
                Persist = 1 // This logon session only.
            };
            return CredWrite(ref value, 0);
        }
        finally { Marshal.ZeroFreeCoTaskMemUnicode(blob); }
    }

    private static void DeleteEndToEndCredential()
    {
        CredDelete(EndToEndCredentialTarget, 1, 0);
    }
}
