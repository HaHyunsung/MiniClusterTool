using System;
using FabStack.Core;

namespace FabStack.Core.Tests
{
    // IFaultSource + IStandStill + IAbortable 대역. 모듈의 하위 전파를 세는 용도
    public sealed class FakeAbortable : IFaultSource, IStandStill, IAbortable
    {
        #region Properties
        public string Name { get; }
        public Fault? Fault { get; set; }
        public bool StandStill { get; set; } = true;
        public int AbortCount { get; private set; }
        public int ClearCount { get; private set; }
        #endregion

        #region Constructor
        public FakeAbortable(string name)
        {
            Name = name;
        }
        #endregion

        #region Methods
        public bool Abort()
        {
            AbortCount++;
            return true;
        }

        public void ClearFault()
        {
            ClearCount++;
            Fault = null;
        }
        #endregion
    }
}
