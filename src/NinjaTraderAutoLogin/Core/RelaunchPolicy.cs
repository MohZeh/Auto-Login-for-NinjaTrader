using System;

namespace NinjaTraderAutoLogin
{
    // Pressing the shortcut just after closing NinjaTrader can find the copy that is still exiting.
    // A fresh copy is opened only if that one vanished at once and nothing had been sent to it.
    internal static class RelaunchPolicy
    {
        public static readonly TimeSpan ExitingWindow = TimeSpan.FromSeconds(15);

        public static bool ShouldRelaunch(bool explicitRequest, bool attachedToOpenCopy, LoginResult result, TimeSpan attemptDuration)
        {
            return explicitRequest && attachedToOpenCopy && result.Outcome == Outcome.ProcessExited && !result.ActionCompletionUncertain &&
                result.LoginSubmissions == 0 && result.SimulationSelections == 0 && result.LiveSelections == 0 && attemptDuration <= ExitingWindow;
        }
    }
}
