namespace NinjaTraderAutoLogin
{
    internal enum LiveConfirmation
    {
        None, EnterLive, EnterLiveWithWatch, WatchWhileLive, UnknownVersion
    }

    // A real account stays sensitive after Live is tested, so each move into it asks again.
    internal static class LiveConfirmationPolicy
    {
        public static LiveConfirmation Required(TradingMode savedMode, LaunchMode savedLaunch, TradingMode nextMode, LaunchMode nextLaunch)
        {
            if (nextMode != TradingMode.Live)
            {
                return LiveConfirmation.None;
            }

            if (savedMode != TradingMode.Live)
            {
                return nextLaunch == LaunchMode.Watch ? LiveConfirmation.EnterLiveWithWatch : LiveConfirmation.EnterLive;
            }

            return savedLaunch != LaunchMode.Watch && nextLaunch == LaunchMode.Watch ? LiveConfirmation.WatchWhileLive : LiveConfirmation.None;
        }
    }
}
