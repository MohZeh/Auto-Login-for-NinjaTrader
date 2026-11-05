namespace NinjaTraderAutoLogin
{
    // Saving clears a pause caused by a fault, but never one the user chose.
    internal sealed class MonitoringPause
    {
        private bool chosenByUser;
        public bool Paused
        {
            get; private set;
        }
        public void Toggle()
        {
            Paused = !Paused;
            chosenByUser = Paused;
        }
        public void Fault()
        {
            Paused = true;
        }
        public void Saved(LaunchMode mode)
        {
            if (mode != LaunchMode.Watch || !chosenByUser)
            {
                Paused = false;
                chosenByUser = false;
            }
        }
    }
}
