# Security and data boundaries

- Credentials are stored through the Windows Credential Manager API for the current
  Windows user, persisted across that user's sign-ins on this computer. Passwords are
  not stored in settings, logs, command arguments, environment variables, the clipboard
  or worker messages.
- Settings load the saved username and password-presence metadata. They do not convert
  a saved credential blob into a managed password string or put it into the editor.
  A constant-length decorative mask indicates an existing password for that username;
  the editor's actual value remains empty. Saving an empty editor preserves that secret.
- The eye button is enabled only for a newly typed, unsaved password. Saving, clearing,
  reopening settings or losing window activation conceals it; saved passwords cannot
  be revealed through settings. This UI rule does not change the vault's access policy.
- The app verifies the target process path, start time, session, user and integrity
  level before UI interaction. Unknown or ambiguous controls stop the operation.
- Before starting NinjaTrader, before reading the password, and before typing into or
  clicking anything in its window, the app asks Windows to verify the target file's
  Authenticode signature and requires the publisher `NinjaTrader, LLC`. A file that fails
  is never started or touched. This catches a wrong path or a replaced file. Revocation is
  not checked, so a sign-in never waits on the network, and it does not protect a
  compromised Windows account.
- Release executables are not code-signed. Each release publishes a SHA-256 checksum and
  a GitHub build attestation that ties it to this repository's workflow.
- The automation child runs at the same privilege level as the parent and uses a private
  inherited input/output channel containing process metadata only. It reads credentials
  directly from Credential Manager when the login state requires them.
- A password NinjaTrader rejected is not read or sent again until the user enters a new
  one or confirms another attempt, because repeated attempts can lock the account. The
  refusal is held both in the settings file and in the running app, so a failed save
  cannot lift it; if that save fails, watching also pauses until the user acts.
- The Live button is checked for an account-opening offer again right before it is
  pressed, so a label that changed after inspection never opens an account page.
- The support summary copies only named fields: versions, outcome, action counts and error
  types. It has no username, password, file path or control detail. The diagnostics folder
  may hold system and process paths, so review its files before sharing them.
- Cancellation or the outer watchdog terminates only the app's automation child.
  It does not terminate NinjaTrader. If the last external action may have completed,
  the diagnostic result records uncertainty and asks the user to inspect NinjaTrader.
- A request already delivered to NinjaTrader cannot be undone by killing the client.
  The app does not automatically retry such an attempt.
- Managed strings and UIA require in-memory password strings. Clearing references or
  a textbox is not a guarantee of immediate managed-memory erasure. Unmanaged credential
  buffers are explicitly cleared before release.
- Software running as the same Windows user may have comparable access to the vault.
  This app is not a security boundary against a compromised Windows account.
- About and contact links are opened only by a user action. The update check performs
  an unauthenticated HTTPS GET for this project's latest public GitHub release; it sends
  the product name/version and API headers, not NinjaTrader credentials or account data.
  It has a 10-second deadline and a 64 KiB response limit. Redirects are not followed.
  Only HTTPS release pages under the configured GitHub repository are offered. There
  is no automatic download, installation, execution or background update polling. Contact
  links display the exact address they open.

Do not include credentials, account details, raw runtime captures or full system paths
in public issues. Use the repository's private vulnerability reporting facility when
available; otherwise request a private contact method without posting sensitive details.
