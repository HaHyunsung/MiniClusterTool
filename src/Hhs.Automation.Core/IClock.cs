using System;
using System.Collections.Generic;
using System.Text;

namespace Hhs.Automation.Core
{
    public interface IClock
    {
        DateTimeOffset Now { get; }
        Task DelayAsync(TimeSpan duration, CancellationToken ct);
    }
}
