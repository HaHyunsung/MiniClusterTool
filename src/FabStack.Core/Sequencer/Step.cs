using System;
using System.Collections.Generic;
using System.Text;

namespace FabStack.Core
{
    public sealed record Step(
        string Name,
        Func<bool>? Action = null,      // 스텝 시작 시 한 번. false = 명령 거부
        Func<bool>? Until = null,       // 완료 조건. 매 스캔 확인. null이면 바로 완료
        TimeSpan? Timeout = null);      // 공정 타임아웃. null이면 감시 안 함

    public enum SequenceFault
    {
        ActionRejected,
        StepTimeout
    }
}
