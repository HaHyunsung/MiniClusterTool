using System;
using System.Collections.Generic;
using System.Text;

namespace FabStack.Core.Tests
{
    public class FakeScanCycle : IScanCycle
    {
        #region Fields
        private readonly List<Action> scans = new();
        private readonly FakeClock clock;
        private readonly TimeSpan scanPeriod;
        #endregion

        #region Constructor
        public FakeScanCycle(FakeClock clock, TimeSpan scanPeriod)
        {
            this.clock = clock;
            this.scanPeriod = scanPeriod;
        }
        #endregion

        #region Properties
        #endregion

        #region Methods
        public void Register(Action scan) => scans.Add(scan);

        public void RunScans(TimeSpan duration)
        {
            int count = (int)(duration.Ticks / scanPeriod.Ticks);
            for (int i = 0; i < count; i++)
            {
                clock.Advance(scanPeriod);
                foreach (Action scan in scans)
                    scan();
            }
        }
        #endregion
    }
}
