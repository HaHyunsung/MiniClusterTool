using FabStack.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace FabStack.Core.Tests
{
    public class FakeValve : IValve
    {
        #region Fields
        private readonly TimeSpan workingTime;
        private readonly TimeSpan timeoutTime;
        private readonly OnDelayTimer timer;
        #endregion

        #region Constructor
        public FakeValve(string name, ValveState initState, TimeSpan workingTime, TimeSpan timeoutTime, IClock clock, IScanCycle scanCycle)
        {
            OpenPermissive = new Permissive();
            ClosePermissive = new Permissive();
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

        public Permissive OpenPermissive { get; }

        public Permissive ClosePermissive { get; }

        public ValveState State { get; private set; }

        public Fault? Fault { get; private set; }

        public bool StandStill
        {
            get => State is not (ValveState.Opening or ValveState.Closing);
        }
        #endregion

        #region Methods
        public bool Open()
        {
            if (!OpenPermissive.IsAllowed || Fault is not null) return false;
            if (State == ValveState.Opened || State == ValveState.Opening) return true;

            State = ValveState.Opening;
            timer.Tick();          // 명령 시각 기록 (TON의 IN이 켜진 순간)

            return true;
        }

        public bool Close()
        {
            if (!ClosePermissive.IsAllowed || Fault is not null) return false;
            if (State == ValveState.Closed || State == ValveState.Closing) return true;

            State = ValveState.Closing;
            timer.Tick();          // 명령 시각 기록 (TON의 IN이 켜진 순간)

            return true;
        }

        private void Scan()
        {
            if (Fault is not null) return;
            if (State != ValveState.Opening && State != ValveState.Closing) return;

            TimeSpan elapsed = timer.ElapsedTime();

            // 1) 정상 응답: 동작 시간이 지나면 센서가 들어온다
            if (RespondsToCommand && elapsed >= workingTime)
            {
                State = State == ValveState.Opening ? ValveState.Opened : ValveState.Closed;
                timer.Stop();
                return;
            }

            // 2) 타임아웃: 동작 중 상태가 timeout 이상 유지되면 에러 (래칭)
            if (elapsed >= timeoutTime)
            {
                Fault = new Fault(Name, State == ValveState.Opening ? ValveFault.OpenTimeout : ValveFault.CloseTimeout, "응답 없음");
                State = ValveState.Unknown;
            }
        }

        public void ClearFault()
        {
            Fault = null;
            timer.Stop();
        }
        #endregion
    }

    public enum ValveFault
    {
        OpenTimeout,
        CloseTimeout
    }
}
