using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace NinjaTraderAutoLogin
{
    internal static class ProcessSecurity
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr token, int kind, IntPtr information, int length, out int returned);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        public static void Verify(Process target)
        {
            IntPtr token;
            if (!OpenProcessToken(target.Handle, 8, out token))
            {
                throw new LoginProblem(Outcome.AccessDenied);
            }

            try
            {
                IntPtr user = ReadToken(token, 1);
                try
                {
                    string sid = new SecurityIdentifier(Marshal.ReadIntPtr(user)).Value;
                    using (WindowsIdentity current = WindowsIdentity.GetCurrent())
                    {
                        if (sid != current.User.Value)
                        {
                            throw new LoginProblem(Outcome.AccessDenied);
                        }
                    }
                }
                finally { Marshal.FreeHGlobal(user); }
                int targetIntegrity = Integrity(token);
                IntPtr ownToken;
                using (Process current = Process.GetCurrentProcess())
                {
                    if (!OpenProcessToken(current.Handle, 8, out ownToken))
                    {
                        throw new LoginProblem(Outcome.AccessDenied);
                    }
                }

                try
                {
                    if (targetIntegrity != Integrity(ownToken))
                    {
                        throw new LoginProblem(Outcome.AccessDenied);
                    }
                }
                finally { CloseHandle(ownToken); }
            }
            finally { CloseHandle(token); }
        }
        private static IntPtr ReadToken(IntPtr token, int kind)
        {
            int length;
            GetTokenInformation(token, kind, IntPtr.Zero, 0, out length);
            if (length <= 0)
            {
                throw new LoginProblem(Outcome.AccessDenied);
            }

            IntPtr result = Marshal.AllocHGlobal(length);
            if (!GetTokenInformation(token, kind, result, length, out length))
            {
                Marshal.FreeHGlobal(result);
                throw new LoginProblem(Outcome.AccessDenied);
            }
            return result;
        }
        private static int Integrity(IntPtr token)
        {
            IntPtr info = ReadToken(token, 25);
            try
            {
                IntPtr sid = Marshal.ReadIntPtr(info);
                int count = Marshal.ReadByte(sid, 1);
                return Marshal.ReadInt32(sid, 8 + ((count - 1) * 4));
            }
            finally { Marshal.FreeHGlobal(info); }
        }
    }
}
