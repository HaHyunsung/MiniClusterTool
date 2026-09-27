namespace FabStack.Core
{
    public class OnDelayTimer
    {
        #region Fields
        private readonly IClock clock;
        private DateTimeOffset? startTime;
        #endregion

        #region Properties
        #endregion

        #region Constructor
        public OnDelayTimer(IClock clock)
        {
            this.clock = clock;
        }
        #endregion

        #region Methods
        public void Tick() => startTime = clock.Now;

        public void Stop() => startTime = null;
        public TimeSpan ElapsedTime()
        {
            if (startTime is null) return TimeSpan.Zero;
            return clock.Now - startTime.Value;
        }
        #endregion
    }
}
