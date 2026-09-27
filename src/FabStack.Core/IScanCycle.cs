using System;
using System.Collections.Generic;
using System.Text;

namespace FabStack.Core
{
    public interface IScanCycle
    {
        void Register(Action scan);
    }
}
