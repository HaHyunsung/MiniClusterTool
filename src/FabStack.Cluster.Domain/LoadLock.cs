using System;
using System.Collections.Generic;
using FabStack.Core;

namespace FabStack.Cluster
{
    // 로드락 모듈. 게이트 2개 + 진공 챔버.
    // Running은 외부 요청(RequestPumpDown / RequestVent)을 기다렸다가 챔버 스텝을 끼워 넣는다.
    // TM 핸드셰이크와 카세트는 시퀀스 엔진 단계에서 이 자리에 들어온다.
    public sealed class LoadLock : ModuleBase
    {
        #region Fields
        private readonly IValve atmGate;
        private readonly IValve vacGate;
        private readonly VacuumChamber chamber;
        private bool pumpDownRequested;
        private bool ventRequested;
        #endregion

        #region Properties
        public IValve AtmGate => atmGate;
        public IValve VacGate => vacGate;
        public VacuumChamber Chamber => chamber;
        public bool PumpDownRequested => pumpDownRequested;
        public bool VentRequested => ventRequested;
        #endregion

        #region Constructor
        public LoadLock(string name, IValve atmGate, IValve vacGate, VacuumChamber chamber,
                        ModeSelector mode, TransitionTimeouts timeouts, IClock clock, IScanCycle scanCycle)
            : base(name, mode, timeouts, clock, scanCycle)
        {
            this.atmGate = atmGate;
            this.vacGate = vacGate;
            this.chamber = chamber;

            AddComponent(atmGate);
            AddComponent(vacGate);
            foreach (IFaultSource component in chamber.Components)
                AddComponent(component);

            // 게이트 인터락 (챔버 내부 인터락은 챔버 생성자가 등록)
            atmGate.OpenPermissive.AddCondition(() => chamber.IsAtAtmosphere, "대기압 아님");
            atmGate.OpenPermissive.AddCondition(() => chamber.IsIsolated, "챔버 미격리");
            atmGate.OpenPermissive.AddCondition(() => vacGate.State == ValveState.Closed, $"{vacGate.Name} 열림");
            vacGate.OpenPermissive.AddCondition(() => chamber.IsAtVacuum, "진공 아님");
            vacGate.OpenPermissive.AddCondition(() => chamber.VentValve.State == ValveState.Closed, $"{chamber.VentValve.Name} 열림");
            vacGate.OpenPermissive.AddCondition(() => atmGate.State == ValveState.Closed, $"{atmGate.Name} 열림");
            chamber.VentValve.OpenPermissive.AddCondition(() => GatesClosed, "게이트 열림");
            chamber.PumpValve.OpenPermissive.AddCondition(() => GatesClosed, "게이트 열림");

            // 요청이 있을 때만 Running, 없으면 Idle
            StateMachine.RunningCondition.AddCondition(() => pumpDownRequested || ventRequested, "요청 없음");
        }
        #endregion

        #region Methods
        private bool GatesClosed => atmGate.State == ValveState.Closed && vacGate.State == ValveState.Closed;

        // 즉시 bool. 완료는 Chamber.IsAtVacuum으로 확인
        public bool RequestPumpDown()
        {
            if (State is not (OperationState.Idle or OperationState.Running)) return false;
            if (ventRequested) return false;
            pumpDownRequested = true;
            return true;
        }

        public bool RequestVent()
        {
            if (State is not (OperationState.Idle or OperationState.Running)) return false;
            if (pumpDownRequested) return false;
            ventRequested = true;
            return true;
        }

        private bool ClearRequests()
        {
            pumpDownRequested = false;
            ventRequested = false;
            return true;
        }

        protected override IEnumerable<Step> Aborting()
        {
            yield return new Step("요청 해제", Action: ClearRequests);
            foreach (Step step in chamber.Isolate()) yield return step;
        }

        protected override IEnumerable<Step> Homing()
        {
            yield return new Step($"{atmGate.Name} 닫기", Action: atmGate.Close, Until: () => atmGate.State == ValveState.Closed);
            yield return new Step($"{vacGate.Name} 닫기", Action: vacGate.Close, Until: () => vacGate.State == ValveState.Closed);
            foreach (Step step in chamber.Isolate()) yield return step;
            foreach (Step step in chamber.StartPump()) yield return step;
        }

        protected override IEnumerable<Step> Running()
        {
            while (!StopRequested)
            {
                yield return new Step("요청 대기", Until: () => pumpDownRequested || ventRequested || StopRequested);
                if (StopRequested) yield break;

                if (pumpDownRequested)
                {
                    foreach (Step step in chamber.PumpDown()) yield return step;
                    pumpDownRequested = false;
                }
                else if (ventRequested)
                {
                    foreach (Step step in chamber.Vent()) yield return step;
                    ventRequested = false;
                }
            }
        }
        #endregion
    }
}
