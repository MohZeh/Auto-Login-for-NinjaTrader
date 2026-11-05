namespace NinjaTraderAutoLogin
{
    // Tracks presentation flags only. The draft remains in the password textbox;
    // a saved secret is never loaded into the settings form.
    internal sealed class PasswordEntryState
    {
        private bool hasDraft, revealing;

        public bool HasSavedPassword
        {
            get; private set;
        }
        public bool CanReveal
        {
            get
            {
                return hasDraft;
            }
        }
        public bool IsRevealed
        {
            get
            {
                return hasDraft && revealing;
            }
        }

        public bool ShowSavedMask(bool usernameMatches)
        {
            return HasSavedPassword && usernameMatches && !hasDraft;
        }

        public void LoadSaved(bool hasPassword)
        {
            HasSavedPassword = hasPassword;
            hasDraft = revealing = false;
        }

        public void DraftChanged(bool hasValue)
        {
            if (!hasDraft || !hasValue)
            {
                revealing = false;
            }
            hasDraft = hasValue;
        }

        public void ToggleReveal()
        {
            revealing = hasDraft && !revealing;
        }

        public void Conceal()
        {
            revealing = false;
        }

        public void Saved()
        {
            LoadSaved(true);
        }
    }
}
