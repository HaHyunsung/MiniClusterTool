using System;
using System.Collections.Generic;
using System.Text;
using Hhs.Automation.Core;

namespace MiniCluster.Infrastructure
{
    internal class SystemClock : IClock
    {
        public DateTimeOffset Now => DateTimeOffset.UtcNow;

        public Task DelayAsync(TimeSpan duration, CancellationToken ct)
            => Task.Delay(duration, ct);
    }
}
