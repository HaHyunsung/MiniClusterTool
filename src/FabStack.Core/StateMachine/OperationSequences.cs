using System;
using System.Collections.Generic;
using System.Text;

namespace FabStack.Core
{
    public sealed record OperationSequences(
    Func<IEnumerable<Step>> Aborting,
    Func<IEnumerable<Step>> Homing,
    Func<IEnumerable<Step>> Running)
    {
        public static OperationSequences Empty { get; } = new(NoSteps, NoSteps, NoSteps);   // 테스트용: 바로 끝나는 시퀀스

        private static IEnumerable<Step> NoSteps() { yield break; }
    }
}
