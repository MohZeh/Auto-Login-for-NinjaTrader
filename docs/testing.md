# Validation boundaries

## Portable final build

Version 0.1.0 adds embedded icons, relocation of an owned startup entry and a
standalone shortcut script. Headless checks exercise command ownership and exact
rollback without touching the registry; only the refusal-record check writes, to a
temporary key under `HKCU\Software\NinjaTraderAutoLogin.Tests` that it deletes.
Shortcut tests use workspace-only folders
with spaces, Unicode, apostrophes and commas; they never launch the application,
read credentials or modify the Desktop. Packaging rejects private path content
and unexpected ZIP entries before promoting an archive to the package directory.

Human acceptance is recorded per executable hash. A login claim needs fresh records from
that executable, each with one submission and one environment selection. Automatic
migration of a startup entry across a physical folder move has headless policy coverage;
that transition is not claimed as a separate Native observation. Live remains
unavailable for real-account testing.

## Fake NinjaTrader

`scripts/test.ps1` also runs `tests/NinjaTraderAutoLogin.FakeTarget`, a WPF window with
NinjaTrader's control ids, through the production UI Automation code on a private desktop.
No window reaches the screen, the target is matched by its own process id and path, and
the credentials are test values held in memory.

Covered: Simulation, Live, a late window, a slow network, a rejected password, a manual
challenge, an owned modal dialog, changed control ids, the process exiting, no screen after
login, an account without Live, an unsigned target, an already signed-in environment
screen with and without a trusted target, and pressing Live directly on an Open Account
button. CI runs the same scenarios.

An owned modal dialog is a child of its owner in the UI Automation tree, so it is not
recognized as a modal window. The sign-in stops safely at the stage timeout instead of
showing the manual-attention message.

These scenarios do not cover the login worker process, Windows Credential Manager or real
NinjaTrader behavior, and they do not replace Human acceptance.

## Existing validation scopes

Headless checks use fake login controls and credentials; configuration failures are
injected through adapters. Supervision tests start only fake console workers and check
that timeout/cancellation leaves no worker alive. They do not execute UIA or open a UI.
The font probe loads both weights from resources in a directory without font files.
Password-entry checks exercise saved-presence and draft-preview transitions without
reading the real vault or constructing a form. The settings credential interface exposes
metadata and deletion only; plaintext retrieval belongs to the login worker.

The layout audit calculates 10 resolutions, 8 DPI scales and 2 languages, plus 64 small
client-size cases. It uses production geometry and real font measurements. These are
calculations, not observations on physical monitors.

Native validation is a separate Human activity on the identified executable:

1. Save Simulation and Dedicated shortcut; start with NinjaTrader closed and launch it
   through the app shortcut. Observe automatic sign-in and the Simulation main window.
2. Save Simulation and Watch; on another fresh launch, start NinjaTrader normally.
   Observe the same successful environment selection and no incorrect warning.

The maintainer reads local run records and matches their candidate/hash and action counts.
Do not paste credentials or transcribe numeric fields. `AlreadyOpen` is useful evidence
but never a fresh login PASS. Live requires its own account, fresh records and Human
acceptance; its current unavailable-account status is preserved rather than promoted.

The Live save confirmations and the account-creation stop are checked by observation. On
an account without Live, a Live request must end with `LiveAccountUnavailable`, zero mode
selections and no account page opened. This does not establish a successful Live login.

Later executable/storage identity changes mean that historical login acceptance does
not establish a fresh successful login on the current executable. A current-build login
claim requires the fresh records described above. Detailed local acceptance journals and
captured account/machine metadata are not part of the public repository.

The current password UI is a separate focused observation: an existing password shows
masked presence with the eye disabled; a newly typed draft starts masked and can be
previewed; clearing it or reopening settings disables the eye again. A disposable draft
can be tested without saving it or changing the real credential. With no saved password,
the empty field must show no saved-password decoration.
Hovering over the disabled eye must explain that the saved password cannot be displayed,
or ask for a new password when none is present. Check the selected language's wording;
the eye must remain disabled while its hint is visible. No account login is needed for
this tooltip observation.

The integrated About page must display the running build version and the configured public contact links in
the current language/theme, with wrapping and scrolling when needed. Check updates on demand;
before a public release exists, the expected result is a clear no-public-release message (or
a connection error when the endpoint cannot be reached). A newer-version notification is tested against
fake release responses; it is not represented as a real published-version observation.

Headless update checks cover SemVer precedence, equal/older/newer versions, missing releases,
rate limits, network failure, cancellation, invalid/oversized responses and rejected external
release URLs. They use injected HTTP handlers and never contact GitHub or read the vault.

Old acceptance stays valid only for its original executable and observed behavior.

The Options menu is a child panel of the settings window. Its placement model covers
both language directions, window edges and small viewports. Human observation must
still confirm click-again/outside/Escape behavior and containment in the displayed window.
About content uses symmetric margins, including equal scrollbar reserves when scrolling
is required. Surface-layout records contain control roles and rectangles only; they do
not capture control text, credential values, screenshots or external windows. They are
written only when `NINJATRADER_AUTOLOGIN_LAYOUT_EVIDENCE=1` is set for the application.
