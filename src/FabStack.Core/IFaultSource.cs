using System;
using System.Collections.Generic;
using System.Text;

namespace FabStack.Core
{
    public interface IFaultSource
    {
        Fault? Fault { get; }        // null = 정상
        void ClearFault();           // 래치 해제
    }

}
