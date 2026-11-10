# Everyday use

[Back to README](../../README.md) · [فارسی](../fa/usage.md)

How to change the app's settings and look after it once it is set up. If this is your first time, start with [Getting started](getting-started.md).

## Open the settings

- **Dedicated shortcut:** double-click **Auto Login for NinjaTrader Settings** on your desktop, or open `NinjaTraderAutoLogin.exe` from the app folder.
- **Watch NinjaTrader:** double-click the app icon next to the clock, or right-click it and select **Settings**.

Closing the settings window with **Close** does not stop watching.

## Start methods

| | Dedicated shortcut | Watch NinjaTrader |
|---|---|---|
| How you sign in | Double-click **Auto Login for NinjaTrader** | Open NinjaTrader as usual |
| Opens NinjaTrader | Yes, if it is not open | No; it waits for you to open it |
| Runs in the background | No; it closes after signing in | Yes, next to the clock, and starts with Windows |
| Progress window with Cancel | Yes | No; use **Cancel current login** in the icon menu |

To change the method, select the other one next to **Start with** and select **Save settings**.

- **Switching to Watch NinjaTrader** adds the app to Windows startup. It does not open NinjaTrader. If NinjaTrader is already open, the app asks *Start watching now?* first.
- **Switching to Dedicated shortcut** removes the app from Windows startup.

If NinjaTrader is already open and signed in, the app reports *NinjaTrader is already open. No new login was performed.* It does not change the environment of an open NinjaTrader.

## The icon next to the clock

In Watch NinjaTrader mode, right-click the app icon next to the clock:

| Menu item | What it does |
|---|---|
| **Settings** | Opens the settings window |
| **Launch NinjaTrader — Simulation** (or Live) | Opens NinjaTrader and signs in now |
| **Pause watching** / **Resume watching** | Stops or restarts watching until you change it again |
| **Cancel current login** | Stops a sign-in in progress |
| **Exit** | Closes the app and stops watching. NinjaTrader stays open. |

The same pause, resume and exit items are in **Options** in the settings window. If you do not see the icon, select the **^** arrow next to the clock.

## Switch between Simulation and Live

Select **Simulation** or **Live trading** next to **Environment** and select **Save settings**. The next sign-in uses it; an open NinjaTrader is not changed.

Switching to Live always asks you to confirm, because Live sign-in has not yet been tested with a real account. Turning on watching with Live asks separately. When Live is saved, the window title starts with **LIVE ·**.

## Change or remove your password

- **Change it:** type the new password and select **Save settings**. The old password is replaced.
- **Keep it:** leave the password box empty. A saved password is never shown again; the box only shows a fixed mask.
- **Change the username:** type the new username and its password together, then save.
- **Remove it:** select **Options**, then **Remove saved login**, and confirm.

If NinjaTrader rejects the password, see [Sign-in was rejected](troubleshooting.md#sign-in-was-rejected).

## Shortcuts

**Create shortcut** in the **Login behavior** section adds two icons to your desktop:

- **Auto Login for NinjaTrader** signs in.
- **Auto Login for NinjaTrader Settings** opens the settings.

If the shortcuts are missing or broken, for example after moving the folder, select **Update shortcut** in the same place.

You can also manage them from PowerShell. In the app folder, run:

```powershell
./setup-shortcuts.ps1
```

This creates the same two shortcuts on your desktop and in the app folder. It replaces shortcuts with the app's previous name, leaves other programs' shortcuts alone and does not open the app.

## Check for a new version

Open **About** and select **Check for updates**. The app contacts GitHub only when you select this.

- *You have the latest version*: nothing to do.
- A sentence such as *Version 0.2.0 is available: open its page*: select it to open the release page, then download the new ZIP yourself and follow [Move or upgrade the app](#move-or-upgrade-the-app).

The app never downloads, installs or runs an update on its own, because it handles your password and is not code-signed. Pre-releases, such as release candidates, are not offered.

## Move or upgrade the app

1. Exit the app: **Options**, then **Exit**, or **Exit** in the icon menu. Closing the settings window alone keeps watching active.
2. Move the folder, or extract the new version into its final place.
3. Open `NinjaTraderAutoLogin.exe` once from the new place. If the old file no longer exists, the Windows startup entry moves to the new place. Otherwise, select **Watch NinjaTrader** again and save.
4. Select **Update shortcut** to repair the two desktop shortcuts.

Settings stay in `%LOCALAPPDATA%\NinjaTraderAutoLogin`, and the password stays in the current user's Windows Credential Manager. Copying the folder to another computer or Windows user does not copy them.

## Remove the app

1. Select **Dedicated shortcut** and save. This removes the app from Windows startup.
2. If you want, select **Options**, then **Remove saved login**.
3. Exit the app.
4. In the app folder, run `./setup-shortcuts.ps1 -Remove` in PowerShell to remove its two shortcuts.
5. Delete the app folder.

Local diagnostic records and settings remain in `%LOCALAPPDATA%\NinjaTraderAutoLogin`. Delete that folder too if you want to remove everything.
