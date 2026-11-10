# Troubleshooting

[Back to README](../../README.md) · [فارسی](../fa/troubleshooting.md)

When a sign-in cannot finish, the app stops and shows a message at the bottom of the settings window. Find the message below. The app never sends a rejected password again by itself.

## Messages after a sign-in

| Message | What it means | What to do |
|---|---|---|
| *Simulation selected. NinjaTrader is open.* (or Live trading) | The sign-in worked | Nothing |
| *NinjaTrader is already open. No new login was performed.* | NinjaTrader was already signed in | Nothing. To sign in again, close NinjaTrader first |
| *Automatic login cancelled.* | You selected Cancel | Start again when you are ready |
| *Sign-in was rejected. Review your credentials; there is no automatic retry.* | NinjaTrader did not accept the username or password | See [Sign-in was rejected](#sign-in-was-rejected) |
| *NinjaTrader returned to its sign-in window, so it did not accept this sign-in.* | NinjaTrader went back to its sign-in window without saying why | Read NinjaTrader's window, then see [Sign-in was rejected](#sign-in-was-rejected) |
| *NinjaTrader needs your attention. Continue manually in its window.* | NinjaTrader asked for something extra, such as a verification code or a message to accept | Finish it in NinjaTrader's window |
| *Waiting timed out. Check NinjaTrader's window.* | NinjaTrader took too long to show its sign-in window | Look at NinjaTrader's window. If it is still loading, wait, then sign in again |
| *NinjaTrader closed or could not be found at the selected path.* | NinjaTrader closed during sign-in, or the path is wrong | Check the path under **NinjaTrader application**, then try again |
| *More than one NinjaTrader is running. Close the extra copy and try again.* | Two NinjaTrader windows are open | Close the extra copy, then try again |
| *This file is not signed by NinjaTrader, so your password was not used.* | The selected file is not the original NinjaTrader program | Choose the real `NinjaTrader.exe` under **NinjaTrader application** |
| *Run the app and NinjaTrader as the same Windows user and access level.* | One of them runs as administrator and the other does not | Open both normally, or both as administrator |
| *NinjaTrader opened; the selected trading mode was not confirmed.* | Signed in, but the app could not confirm Simulation or Live | Check the environment in NinjaTrader |
| *This login has no active Live account (Live offers Open Account).* | Your login has no Live account; NinjaTrader offers to open one | Continue in NinjaTrader's window, or switch the app to Simulation |
| *Live sign-in stopped because this NinjaTrader version was not confirmed.* | You did not confirm Live for an untested NinjaTrader version | Sign in again and confirm, or use Simulation |
| *A NinjaTrader control was not recognized. A diagnostic report was saved.* | NinjaTrader's window looks different from what the app knows, often after a NinjaTrader update | Sign in manually and [report it](#report-a-problem) |
| *Save your username and password to enable automatic login.* | No saved login | Enter your username and password and select **Save settings** |
| *The last sign-in wasn't accepted, so the password was not sent again. Enter it again here.* | NinjaTrader refused this password earlier, and the app is holding it back | See [Sign-in was rejected](#sign-in-was-rejected) |
| *Your choice to try again could not be saved, so the password was not sent.* | The app could not record that you allowed another attempt | Wait a moment and try again; if it repeats, check that the disk is not full |
| *The record of an earlier refused password could not be read, so the password was not sent.* | Windows did not let the app read its small registry record | Make sure your Windows user can use its registry normally, then try again |
| *The app could not save its diagnostic report.* | The sign-in record could not be written, so the app stopped to stay safe | Check that the disk is not full, then try again |
| *The operation could not finish. A diagnostic report was saved.* | Something unexpected happened | Try again once; if it happens again, [report it](#report-a-problem) |

## Sign-in was rejected

After NinjaTrader rejects a sign-in, the **Setup** page keeps a warning, and the app does not send that password again. This protects your account: repeated wrong attempts can lock it.

- *NinjaTrader rejected this username or password. It won't be resent.*: type the correct password and select **Save settings**.
- *Sign-in wasn't accepted. It may be the password or the connection.*: the cause is not certain. If your password is correct and the connection was the problem, start the sign-in again; the app asks *Try the same password again?* Select **Try again**.

The saved login is not deleted automatically, because the cause may be a locked account or a failed connection. To delete it, select **Remove saved login** next to the warning.

## Messages on the Setup page

| Message | What to do |
|---|---|
| *✕ This file was not found.* | Select the folder icon and choose `NinjaTrader.exe` from the `bin` folder of your NinjaTrader installation |
| *⚠ Version … has not been tested with this app.* | Your NinjaTrader version differs from the tested 8.1.8.4. Simulation still works; Live asks once for this version |
| *✕ Not signed by NinjaTrader, so sign-in will stop.* | Choose the original `NinjaTrader.exe` |
| *Watching paused. More than one NinjaTrader is running; close the extra copy and resume.* | Close the extra NinjaTrader, then select **Resume watching** |
| *Watching paused. Check NinjaTrader's path and access level.* | Check the path, run both programs as the same Windows user, then resume |
| *Windows startup needs attention. Review the launch method and save again.* | Select the start method again and select **Save settings** |
| *Settings saved, but the record of the earlier refused password could not be cleared, so automatic sign-in stays on hold.* | Your new details are saved. Select **Save settings** again; if it repeats, check that your Windows user can use its registry |
| *Settings saved, but the record of refused passwords could not be checked, so the password will not be sent until it can be read.* | Your new details are saved, but Windows did not let the app read its registry record. Select **Save settings** again; if it repeats, check that your Windows user can use its registry |
| *Saved credentials removed, but the app could not confirm that its record of an earlier refused password was cleared.* | Your password is removed. The record is checked again the next time you save new details |

## Other problems

**The shortcut does nothing or points to the wrong place.** Open the settings and select **Update shortcut**.

**I don't see the app icon next to the clock.** Select the **^** arrow next to the clock to show hidden icons. If it is not there either, open `NinjaTraderAutoLogin.exe`; watching may have been turned off by **Exit**.

**Watching starts with Windows but I want to stop it.** Select **Dedicated shortcut** and save, or use **Pause watching** to stop it for now.

**Don't start a second copy** of the app to force another attempt. Pause watching before investigating a problem.

## Report a problem

1. Open **About** and select **Copy support summary**. It has no password, username or file paths.
2. Open an [issue on GitHub](https://github.com/MohZeh/Auto-Login-for-NinjaTrader/issues) and paste the summary.

**Open diagnostics folder** shows the full local reports. They may contain system paths; review them before sharing anything, and never post account details or screenshots that show them.

For a security problem, follow [SECURITY.md](../../SECURITY.md) instead of opening a public issue.
