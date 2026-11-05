using NinjaTraderAutoLogin;

internal static partial class TestRunner
{
    private static void RunPasswordEntryChecks()
    {
        Test("No saved credential means no saved-password mask and no preview", delegate
        {
            var state = new PasswordEntryState();
            state.ToggleReveal();
            Require(!state.HasSavedPassword && !state.ShowSavedMask(true) && !state.CanReveal && !state.IsRevealed);
        });
        Test("A saved password shows presence but cannot be revealed", delegate
        {
            var state = new PasswordEntryState();
            state.LoadSaved(true);
            state.ToggleReveal();
            Require(state.ShowSavedMask(true) && !state.CanReveal && !state.IsRevealed);
        });
        Test("A saved password belongs only to its matching username", delegate
        {
            var state = new PasswordEntryState();
            state.LoadSaved(true);
            Require(!state.ShowSavedMask(false) && state.ShowSavedMask(true));
        });
        Test("A newly entered password starts masked and enables its own preview", delegate
        {
            var state = new PasswordEntryState();
            state.DraftChanged(true);
            Require(state.CanReveal && !state.IsRevealed && !state.ShowSavedMask(true));
            state.ToggleReveal();
            Require(state.IsRevealed);
            state.ToggleReveal();
            Require(!state.IsRevealed && state.CanReveal);
        });
        Test("A replacement draft hides the saved-password decoration", delegate
        {
            var state = new PasswordEntryState();
            state.LoadSaved(true);
            state.DraftChanged(true);
            Require(state.HasSavedPassword && state.CanReveal && !state.IsRevealed && !state.ShowSavedMask(true));
        });
        Test("Clearing a replacement restores only the saved indicator and disables preview", delegate
        {
            var state = new PasswordEntryState();
            state.LoadSaved(true);
            state.DraftChanged(true);
            state.ToggleReveal();
            state.DraftChanged(false);
            state.ToggleReveal();
            Require(state.ShowSavedMask(true) && !state.CanReveal && !state.IsRevealed);
        });
        Test("Clearing an unsaved password leaves an empty field with preview disabled", delegate
        {
            var state = new PasswordEntryState();
            state.DraftChanged(true);
            state.ToggleReveal();
            state.DraftChanged(false);
            Require(!state.ShowSavedMask(true) && !state.CanReveal && !state.IsRevealed);
        });
        Test("A new draft never inherits the previous draft's reveal state", delegate
        {
            var state = new PasswordEntryState();
            state.DraftChanged(true);
            state.ToggleReveal();
            state.DraftChanged(false);
            state.DraftChanged(true);
            Require(state.CanReveal && !state.IsRevealed);
        });
        Test("Saving a revealed draft locks preview and shows only saved presence", delegate
        {
            var state = new PasswordEntryState();
            state.DraftChanged(true);
            state.ToggleReveal();
            state.Saved();
            state.ToggleReveal();
            Require(state.ShowSavedMask(true) && !state.CanReveal && !state.IsRevealed);
        });
        Test("Reloading metadata does not restore a previous preview session", delegate
        {
            var state = new PasswordEntryState();
            state.DraftChanged(true);
            state.ToggleReveal();
            state.LoadSaved(true);
            Require(state.ShowSavedMask(true) && !state.CanReveal && !state.IsRevealed);
            state.LoadSaved(false);
            Require(!state.HasSavedPassword && !state.ShowSavedMask(true));
        });
        Test("Concealing a draft keeps the input available without revealing it", delegate
        {
            var state = new PasswordEntryState();
            state.DraftChanged(true);
            state.ToggleReveal();
            state.Conceal();
            state.DraftChanged(true);
            Require(state.CanReveal && !state.IsRevealed);
        });
        Test("The settings credential capability cannot read a plaintext secret", delegate
        {
            Require(!typeof(ICredentials).IsAssignableFrom(typeof(ICredentialSettings)));
            Require(typeof(ICredentialSettings).GetMethod("Read") == null);
            Require(typeof(ICredentialSettings).GetMethod("ReadMetadata").ReturnType == typeof(CredentialMetadata));
            foreach (var method in typeof(ICredentialSettings).GetMethods())
            {
                Require(method.ReturnType == typeof(CredentialMetadata) || method.ReturnType == typeof(void));
            }
            Require(typeof(SettingsForm).GetConstructors()[0].GetParameters()[2].ParameterType == typeof(ICredentialSettings));
        });
    }
}
