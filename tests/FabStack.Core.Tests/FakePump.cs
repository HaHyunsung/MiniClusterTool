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

        public bool Error { get; private set; }

        public int ErrorID { get; private set; }

        public bool RespondsToCommand { get; set; } = true;   // 테스트 중간에 끌 수 있음

        public Permissive RunPermissive { get; }

        public Permissive StopPermissive { get; }

        public PumpState State { get; private set; }
        #endregion

        #region Methods
        public bool Run()
        {
            if (!RunPermissive.IsAllowed || Error) return false;
            if (State == PumpState.Running || State == PumpState.Starting) return true;

            State = PumpState.Starting;
            timer.Tick();

            return true;
        }

        public bool Stop()
        {
            if (!StopPermissive.IsAllowed || Error) return false;
            if (State == PumpState.Stopped || State == PumpState.Stopping) return true;

            State = PumpState.Stopping;
            timer.Tick();

            return true;
        }

        private void Scan()
        {
            if (Error) return;
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
                Error = true;
                ErrorID = State == PumpState.Starting ? (int)PumpErrorCode.RunTimeout : (int)PumpErrorCode.StopTimeout;
                State = PumpState.Unknown;
            }
        }

        public void Reset()
        {
            Error = false;
            ErrorID = 0;
            timer.Stop();
        }
        #endregion
    }

    public enum PumpErrorCode
    {
        NoError = 0,
        RunTimeout = 1,
        StopTimeout = 2
    }
}
