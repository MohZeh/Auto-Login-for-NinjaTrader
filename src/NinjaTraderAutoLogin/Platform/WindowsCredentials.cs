using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace NinjaTraderAutoLogin
{
    internal sealed class WindowsCredentials : ICredentials, ICredentialWriter, ICredentialSettings
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Credential
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
        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredWrite(ref Credential credential, uint flags);
        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredDelete(string target, uint type, uint flags);
        [DllImport("advapi32.dll")] private static extern void CredFree(IntPtr pointer);

        public CredentialMetadata ReadMetadata()
        {
            IntPtr pointer;
            if (!CredRead(BuildInfo.CredentialTarget, 1, 0, out pointer))
            {
                if (Marshal.GetLastWin32Error() == 1168)
                {
                    return null;
                }

                throw new LoginProblem(Outcome.AccessDenied);
            }
            try
            {
                Credential value = (Credential)Marshal.PtrToStructure(pointer, typeof(Credential));
                bool hasPassword = !String.IsNullOrWhiteSpace(value.UserName) && value.Blob != IntPtr.Zero &&
                    value.BlobSize > 0 && value.BlobSize <= 2560 && value.BlobSize % 2 == 0;
                return new CredentialMetadata(value.UserName, hasPassword);
            }
            finally { ClearReturnedBlob(pointer); CredFree(pointer); }
        }

        public Secret Read()
        {
            IntPtr pointer;
            if (!CredRead(BuildInfo.CredentialTarget, 1, 0, out pointer))
            {
                if (Marshal.GetLastWin32Error() == 1168)
                {
                    return null;
                }

                throw new LoginProblem(Outcome.AccessDenied);
            }
            try
            {
                Credential value = (Credential)Marshal.PtrToStructure(pointer, typeof(Credential));
                if (value.BlobSize == 0 || value.BlobSize > 2560 || value.BlobSize % 2 != 0)
                {
                    throw new LoginProblem(Outcome.CredentialsMissing);
                }

                return new Secret(value.UserName, Marshal.PtrToStringUni(value.Blob, (int)value.BlobSize / 2));
            }
            finally { ClearReturnedBlob(pointer); CredFree(pointer); }
        }

        private static void ClearReturnedBlob(IntPtr pointer)
        {
            Credential value = (Credential)Marshal.PtrToStructure(pointer, typeof(Credential));
            if (value.Blob != IntPtr.Zero && value.BlobSize <= 2560)
            {
                for (int i = 0; i < value.BlobSize; i++)
                {
                    Marshal.WriteByte(value.Blob, i, 0);
                }
            }
        }

        public void Save(string username, string password)
        {
            if (String.IsNullOrWhiteSpace(username) || String.IsNullOrEmpty(password) || Encoding.Unicode.GetByteCount(password) > 2560)
            {
                throw new InvalidDataException("InvalidCredentials");
            }

            IntPtr blob = Marshal.StringToCoTaskMemUni(password);
            try
            {
                var value = new Credential();
                value.Type = 1; // CRED_TYPE_GENERIC
                value.TargetName = BuildInfo.CredentialTarget;
                value.UserName = username.Trim();
                value.Blob = blob;
                value.BlobSize = (uint)Encoding.Unicode.GetByteCount(password);
                value.Persist = 2; // Local computer, same Windows user across logons.
                if (!CredWrite(ref value, 0))
                {
                    throw new LoginProblem(Outcome.AccessDenied);
                }
            }
            finally { Marshal.ZeroFreeCoTaskMemUnicode(blob); }
        }

        public void Delete()
        {
            if (!CredDelete(BuildInfo.CredentialTarget, 1, 0) && Marshal.GetLastWin32Error() != 1168)
            {
                throw new LoginProblem(Outcome.AccessDenied);
            }
        }
    }
}
