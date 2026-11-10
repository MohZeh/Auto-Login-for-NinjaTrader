using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace NinjaTraderAutoLogin
{
    // The password only ever reaches a file that Windows confirms was signed by NinjaTrader.
    // This guards against a wrong path or a replaced file; it is not malware protection.
    internal static class NinjaTraderSignature
    {
        private const string Publisher = "NinjaTrader, LLC";
        private static readonly Guid GenericVerify = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        [StructLayout(LayoutKind.Sequential)]
        private struct FileInfoData
        {
            public uint Size;
            [MarshalAs(UnmanagedType.LPWStr)] public string Path;
            public IntPtr Handle, KnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TrustData
        {
            public uint Size;
            public IntPtr PolicyCallback, SipClient;
            public uint UiChoice, RevocationChecks, UnionChoice;
            public IntPtr File;
            public uint StateAction;
            public IntPtr StateData;
            public IntPtr UrlReference;
            public uint ProviderFlags, UiContext;
            public IntPtr SignatureSettings;
        }

        [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
        private static extern int WinVerifyTrust(IntPtr window, [In] ref Guid action, [In] ref TrustData data);

        public static bool IsTrustedPublisher(string name)
        {
            return String.Equals(name, Publisher, StringComparison.Ordinal);
        }
        public static bool IsTrusted(string path)
        {
            try
            {
                if (!File.Exists(path) || !HasValidSignature(path))
                {
                    return false;
                }

                using (X509Certificate signer = X509Certificate.CreateFromSignedFile(path))
                using (var certificate = new X509Certificate2(signer))
                {
                    return IsTrustedPublisher(certificate.GetNameInfo(X509NameType.SimpleName, false));
                }
            }
            catch (CryptographicException) { return false; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (ArgumentException) { return false; }
        }
        // The certificate chain and timestamp are checked offline, so a sign-in never waits on the network.
        private static bool HasValidSignature(string path)
        {
            var file = new FileInfoData { Size = (uint)Marshal.SizeOf(typeof(FileInfoData)), Path = path };
            IntPtr fileBlock = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(FileInfoData)));
            try
            {
                Marshal.StructureToPtr(file, fileBlock, false);
                var data = new TrustData
                {
                    Size = (uint)Marshal.SizeOf(typeof(TrustData)),
                    UiChoice = 2, // WTD_UI_NONE
                    RevocationChecks = 0, // WTD_REVOKE_NONE
                    UnionChoice = 1, // WTD_CHOICE_FILE
                    File = fileBlock,
                    ProviderFlags = 0x00001000 // WTD_CACHE_ONLY_URL_RETRIEVAL
                };
                Guid action = GenericVerify;
                return WinVerifyTrust(new IntPtr(-1), ref action, ref data) == 0;
            }
            finally
            {
                Marshal.DestroyStructure(fileBlock, typeof(FileInfoData));
                Marshal.FreeHGlobal(fileBlock);
            }
        }
    }

    internal sealed class TrustedTargetCredentials : ICredentials
    {
        private readonly string path;
        private readonly Func<string, bool> trusted;
        private readonly ICredentials credentials;

        public TrustedTargetCredentials(string path, Func<string, bool> trusted, ICredentials credentials)
        {
            this.path = path;
            this.trusted = trusted;
            this.credentials = credentials;
        }
        public Secret Read()
        {
            if (!trusted(path))
            {
                throw new LoginProblem(Outcome.UntrustedTarget);
            }

            return credentials.Read();
        }
    }

    // Nothing is typed or clicked in a file that is not signed by NinjaTrader, including the
    // environment choice on an already signed-in window, where no password is read.
    internal sealed class TrustedTargetUi : ILoginUi
    {
        private readonly ILoginUi ui;
        private readonly string path;
        private readonly Func<string, bool> trusted;

        public TrustedTargetUi(ILoginUi ui, string path, Func<string, bool> trusted)
        {
            this.ui = ui;
            this.path = path;
            this.trusted = trusted;
        }
        public UiSnapshot Inspect()
        {
            return ui.Inspect();
        }
        public void Submit(Secret credentials, CancellationToken cancellation)
        {
            Require();
            ui.Submit(credentials, cancellation);
        }
        public void SelectMode(TradingMode mode, CancellationToken cancellation)
        {
            Require();
            ui.SelectMode(mode, cancellation);
        }
        private void Require()
        {
            if (!trusted(path))
            {
                throw new LoginProblem(Outcome.UntrustedTarget);
            }
        }
    }

    // Repeating a refused password can lock the account, so it stays unused until the owner decides.
    internal sealed class UnrejectedCredentials : ICredentials
    {
        private readonly Func<bool> rejected;
        private readonly ICredentials credentials;

        public UnrejectedCredentials(Func<bool> rejected, ICredentials credentials)
        {
            this.rejected = rejected;
            this.credentials = credentials;
        }
        public Secret Read()
        {
            if (rejected())
            {
                throw new LoginProblem(Outcome.CredentialsRejected);
            }

            return credentials.Read();
        }
    }

    internal sealed class TargetInspection
    {
        public bool Exists, Trusted;
        public string Version;
        public NinjaTraderVersionStatus Status;

        public static TargetInspection Of(string path)
        {
            var result = new TargetInspection { Exists = !String.IsNullOrWhiteSpace(path) && File.Exists(path) };
            if (result.Exists)
            {
                result.Trusted = NinjaTraderSignature.IsTrusted(path);
                result.Version = NinjaTraderCompatibility.ReadVersion(path);
            }

            result.Status = NinjaTraderCompatibility.Classify(result.Version);
            return result;
        }
    }
}
