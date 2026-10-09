using System;
using System.Collections.Generic;
using System.Text;

namespace FabStack.Core
{
    public interface IAbortable
    {
        bool Abort();                // 즉시 정지. 항상 수락
    }
}
