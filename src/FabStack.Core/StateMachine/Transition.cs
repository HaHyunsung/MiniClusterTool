using System;
using System.Collections.Generic;
using System.Text;

namespace FabStack.Core
{
    public enum TransitionTrigger
    {
        Abort,
        Home,
        Run,
        Pause,
        Stop,
        SemiAuto,
        Complete,
        Activity
    }

    public sealed record StateTransition(DateTimeOffset Time, OperationState From, OperationState To, TransitionTrigger Trigger);

    public sealed record TransitionTimeouts(
    TimeSpan Aborting,              // 필수. 안전 정지는 짧게 끝나야 한다
    TimeSpan? Homing = null,        // 선택. null이면 상태머신은 감시하지 않는다
    TimeSpan? Pausing = null,
    TimeSpan? Stopping = null);
}
