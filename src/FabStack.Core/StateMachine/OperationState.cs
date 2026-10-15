using System;
using System.Collections.Generic;
using System.Text;

namespace FabStack.Core
{
    public enum OperationState
    {
        None = 0,      // 전원 투입 직후. Abort만 받는다
        Aborting,
        Aborted,
        Homing,
        Homed,
        Idle,
        Running,
        Pausing,
        Paused,
        Stopping,
        Stopped,
        SemiAuto
    }
    public enum OperationFault
    {
        AbortingTimeout,
        HomingTimeout,
        PausingTimeout,
        StoppingTimeout
    }

}
