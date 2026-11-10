# Architecture

The application stays in one small .NET Framework WinForms executable, with separate
source responsibilities rather than multiple deployment services.

| Area | Responsibility |
| --- | --- |
| Core | Login state machine, control contracts, attempt gating and data contracts |
| Application | Settings/monitor coordination and configuration transaction |
| Platform | Win32 credentials/processes, UIA adapter, worker supervision and file storage |
| Presentation | Settings form, custom controls, localization, themes and pure layout policy |
| Diagnostics | Value-free layout metadata, progress and result persistence |

## Login lifecycle

One per-user coordinator owns both launch triggers and captures the requested environment
before starting work. The automation worker is the same executable in an internal command
mode. Its UIA thread is MTA and owns no settings window. Requests identify the parent,
target process, environment and run; passwords are absent from the protocol. A dedicated
inherited anonymous pipe carries the request, so its bytes do not depend on the console or
system encoding; replies use the worker's redirected standard output.

Before starting work, the coordinator classifies the target's file version as tested,
unknown or invalid. An unknown version shows a notice; Live also needs one agreement per
version, and an invalid version stops. The coordinator starts NinjaTrader only after its file
has a valid `NinjaTrader, LLC` signature. In the worker, wrappers require the same signature
before the password is read and before any text entry or button press in the target, so an
already signed-in environment screen is covered too. A rejected password is recorded in
the settings file and in a registry value (`RejectionLatch`); the parent also passes its
in-memory record with each request. The rules live in `RefusalPolicy`: right before the
password is read, the worker stops if any record holds a refusal (`CredentialsRejected`) or
if the registry record cannot be read (`RefusalUnavailable`). A read counts as "not there"
only when the parent key lists no such key; any other failure is "could not be read", which
is never stored. The parent lifts a refusal only after both records are cleared and
confirmed by a successful read.

The worker verifies the request against the existing run record and process identity.
The engine inspects login, submits once, selects only the requested mode once, and waits
for the main surface. Existing authenticated mode dialogs can be continued; an already-open
main surface does not count as a fresh mode selection. A Live button that offers account
creation stops the operation before any mode action; its text is read only for this
classification and is not recorded.

NinjaTrader can briefly be unreadable, doubled or show overlapping windows while it starts
or exits. These states delay only observation and become failures if they last; submission
and mode selection are never repeated. A sign-in window that stays ready after submission
is reported as not accepted. Records keep an unexpected failure's type and code, not its text.

UI Automation queries run on NinjaTrader's own window thread, so each inspection walks every
window once for all known controls and then pauses for as long as it took, up to 1.5 seconds.
The walk that records unknown controls runs at most every 5 seconds. If the shortcut finds a
copy that is already exiting and nothing has been sent to it, the coordinator waits for it to
close and opens a fresh copy.

Launch watching lists processes every 3.5 seconds, and only while NinjaTrader is closed. NinjaTrader
runs a single instance, so once a copy is found the coordinator holds that process and only
asks Windows whether it has ended.

Each engine wait has a deadline. An independent parent watchdog bounds a blocked provider
call, and cancellation terminates the worker. The parent records known completed actions
and any uncertain last action from persisted progress. It never kills NinjaTrader.

## Configuration lifecycle

Configuration storage, startup registration and credentials have explicit adapters.
Failure recovery attempts both earlier configuration writes. Incomplete recovery is
reported and watching remains disabled until the user reviews and saves settings.

## Layout and fonts

One pure layout plan drives both the form and numerical audit. Narrow controls stack;
commands join the scroll page when vertical space is insufficient. DPI rounding may
consume a small existing gap without reducing text rows. The scrollbar reserve accounts
for both native per-window and legacy framework metrics. Embedded Vazir resources are
registered process-privately for both GDI and GDI+ controls.
