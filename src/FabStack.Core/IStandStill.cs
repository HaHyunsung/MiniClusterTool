using System;
using System.Collections.Generic;
using System.Text;

namespace FabStack.Core
{
    public interface IStandStill
    {
        bool StandStill { get; }     // 진행 중인 명령 동작이 없다
    }
}
