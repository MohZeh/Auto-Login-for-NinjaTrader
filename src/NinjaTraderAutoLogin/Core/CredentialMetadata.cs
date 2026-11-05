namespace NinjaTraderAutoLogin
{
    internal sealed class CredentialMetadata
    {
        public readonly string Username;
        public readonly bool HasPassword;

        public CredentialMetadata(string username, bool hasPassword)
        {
            Username = username;
            HasPassword = hasPassword;
        }
    }

    // Settings can inspect credential presence and delete it, but cannot retrieve its secret.
    internal interface ICredentialSettings
    {
        CredentialMetadata ReadMetadata();
        void Delete();
    }
}
