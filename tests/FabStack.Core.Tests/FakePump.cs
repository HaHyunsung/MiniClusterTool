namespace FabStack.Core.Tests
{
    // 테스트에서 모듈에 꽂는 대역 펌프. FakeValve와 같은 구조다.
    // Run/Stop 명령 후 workingTime이 지나면 Running/Stopped로 바뀐다.
    public class FakePump : IPump
    {
        #region Fields
        private readonly TimeSpan workingTime;
        private readonly TimeSpan timeoutTime;
        private readonly OnDelayTimer timer;
        #endregion

        #region Constructor
        public FakePump(string name, PumpState initState, TimeSpan workingTime, TimeSpan timeoutTime, IClock clock, IScanCycle scanCycle)
        {
            RunPermissive = new Permissive();
            StopPermissive = new Permissive();
            State = initState;

            Name = name;
            this.workingTime = workingTime;
            this.timeoutTime = timeoutTime;

            timer = new OnDelayTimer(clock);
            scanCycle.Register(Scan);
        }
        #endregion

        #region Properties
        public string Name { get; }

        public bool RespondsToCommand { get; set; } = true;   // 테스트 중간에 끌 수 있음

        public Permissive RunPermissive { get; }

        public Permissive StopPermissive { get; }

        public PumpState State { get; private set; }

        public Fault? Fault { get; private set; }

        public bool StandStill
        {
            get => State is not (PumpState.Starting or PumpState.Stopping);
        }
        #endregion

        #region Methods
        public bool Run()
        {
            if (!RunPermissive.IsAllowed || Fault is not null) return false;
            if (State == PumpState.Running || State == PumpState.Starting) return true;

            State = PumpState.Starting;
            timer.Tick();

            return true;
        }

        public bool Stop()
        {
            if (!StopPermissive.IsAllowed || Fault is not null) return false;
            if (State == PumpState.Stopped || State == PumpState.Stopping) return true;

            State = PumpState.Stopping;
            timer.Tick();

            return true;
        }

        private void Scan()
        {
            if (Fault is not null) return;
            if (State != PumpState.Starting && State != PumpState.Stopping) return;

            TimeSpan elapsed = timer.ElapsedTime();

            // 1) 정상 응답: 동작 시간이 지나면 정격 회전 도달 / 정지 완료
            if (RespondsToCommand && elapsed >= workingTime)
            {
                State = State == PumpState.Starting ? PumpState.Running : PumpState.Stopped;
                timer.Stop();
                return;
            }

            // 2) 타임아웃: 동작 중 상태가 timeout 이상 유지되면 에러 (래칭)
            if (elapsed >= timeoutTime)
            {
                Fault = new Fault(Name, State == PumpState.Starting ? PumpFault.RunTimeout : PumpFault.StopTimeout, "응답 없음");
                State = PumpState.Unknown;
            }
        }

        public void ClearFault()
        {
            Fault = null;
            timer.Stop();
        }
        #endregion
    }

    public enum PumpFault
    {
        RunTimeout,
        StopTimeout
    }
}
