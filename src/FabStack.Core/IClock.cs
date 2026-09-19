using System;
using System.Collections.Generic;
using System.Text;

namespace FabStack.Core
{
    public interface IClock
    {
        DateTimeOffset Now { get; }
        Task DelayAsync(TimeSpan duration, CancellationToken ct);
    }
}
