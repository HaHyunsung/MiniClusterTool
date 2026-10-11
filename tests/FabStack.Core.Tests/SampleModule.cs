using System;
using System.Collections.Generic;
using System.Text;
using FabStack.Core;

namespace FabStack.Core.Tests
{
    public class SampleModule : ModuleBase
    {
        #region Fields
        private readonly IValve valve;
        private readonly TimeSpan? openTimeout;
        #endregion

        #region Properties
        public bool HasWork { get; set; }               // 테스트가 일감 유무를 넣는다
        public int CycleCount { get; private set; }     // 끝낸 사이클 수
        #endregion

        #region Constructor
        public SampleModule(string name, IValve valve, ModeSelector mode, TransitionTimeouts timeouts,
                            IClock clock, IScanCycle scanCycle, TimeSpan? openTimeout = null)
            : base(name, mode, timeouts, clock, scanCycle)
        {
            this.valve = valve;
            this.openTimeout = openTimeout;
            AddComponent(valve);

            StateMachine.RunningCondition.AddCondition(() => HasWork, "일감 없음");
        }
        #endregion

        #region Methods
        public void AddForTest(IFaultSource component) => AddComponent(component);
        public void RaiseForTest(Enum code) => RaiseFault(code, "테스트");

        protected override IEnumerable<Step> Aborting()
        {
            yield break;      // 밸브는 출력 유지 장치라 정지할 게 없다
        }

        protected override IEnumerable<Step> Homing()
        {
            yield return new Step("밸브 닫기", Action: valve.Close, Until: () => valve.State == ValveState.Closed);
        }

        protected override IEnumerable<Step> Running()
        {
            while (!StopRequested)
            {
                yield return new Step("일감 대기", Until: () => HasWork || StopRequested);
                if (StopRequested) yield break;

                yield return new Step("밸브 열기", Action: valve.Open, Until: () => valve.State == ValveState.Opened, Timeout: openTimeout);
                yield return new Step("밸브 닫기", Action: valve.Close,Until: () => valve.State == ValveState.Closed);
                CycleCount++;
            }
        }
        #endregion
    }
}
