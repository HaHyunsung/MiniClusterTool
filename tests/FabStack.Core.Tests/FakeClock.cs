using FabStack.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace FabStack.Core.Tests
{
    public sealed class FakeClock : IClock
    {
        public DateTimeOffset Now { get; private set; }
        = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public Task DelayAsync(TimeSpan duration, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Now += duration;              // 기다리지 않고 시계만 앞으로 민다
            return Task.CompletedTask;    // 이미 끝난 작업을 돌려줌
        }

        public void Advance(TimeSpan duration) => Now += duration;
    }
}
