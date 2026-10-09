using System;
using System.Collections.Generic;
using System.Text;

namespace FabStack.Core
{
    public sealed record Fault(string Source, Enum Code, string Message);

}
