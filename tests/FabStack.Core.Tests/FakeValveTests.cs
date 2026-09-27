using System;
using System.Collections.Generic;
using System.Text;

namespace FabStack.Core.Tests
{
    public class FakeValveTests
    {
        #region Fields
        private readonly FakeValve valve;
        private readonly FakeClock clock;
        private readonly FakeScanCycle scanCycle;

        #endregion

        #region Constructor
        public FakeValveTests()
        {
            clock = new FakeClock();
            scanCycle = new FakeScanCycle(clock, TimeSpan.FromMilliseconds(10));
            valve = new FakeValve("Test Valve", ValveState.Closed, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(1000), clock, scanCycle);
        }
        #endregion

        #region Properties
        #endregion

        #region Methods
        [Fact]
        public async Task ValveOpen()
        {
            await valve.OpenAsync(CancellationToken.None);
            scanCycle.RunScans(TimeSpan.FromMilliseconds(100));
            Assert.Equal(ValveState.Opened, valve.State);
        }

        [Fact]
        public async Task ValveTimeout()
        {
            valve.RespondsToCommand = false;
            await valve.OpenAsync(CancellationToken.None);
            scanCycle.RunScans(TimeSpan.FromMilliseconds(1100));
            Assert.Equal(ValveState.Unknown, valve.State);
            Assert.True(valve.Error);
            Assert.Equal((int)ValveErrorCode.OpenTimeout, valve.ErrorID);
        }
        #endregion
    }
}
