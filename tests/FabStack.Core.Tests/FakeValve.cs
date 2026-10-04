using FabStack.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;

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

        public bool Error { get; private set; }

        public int ErrorID { get; private set; }

        public bool RespondsToCommand { get; set; } = true;   // 테스트 중간에 끌 수 있음

        public Permissive OpenPermissive { get; }

        public Permissive ClosePermissive { get; }

        public ValveState State { get; private set; }
        #endregion

        #region Methods
        public bool Open()
        {
            if (!OpenPermissive.IsAllowed || Error) return false;
            if (State == ValveState.Opened || State == ValveState.Opening) return true;

            State = ValveState.Opening;
            timer.Tick();          // 명령 시각 기록 (TON의 IN이 켜진 순간)

            return true;
        }

        public bool Close()
        {
            if (!ClosePermissive.IsAllowed || Error) return false;
            if (State == ValveState.Closed || State == ValveState.Closing) return true;

            State = ValveState.Closing;
            timer.Tick();          // 명령 시각 기록 (TON의 IN이 켜진 순간)

            return true;
        }

        private void Scan()
        {
            if (Error) return;
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
                Error = true;
                ErrorID = State == ValveState.Opening ? (int)ValveErrorCode.OpenTimeout : (int)ValveErrorCode.CloseTimeout;
                State = ValveState.Unknown;
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

    public enum ValveErrorCode
    { 
        NoError = 0,
        OpenTimeout = 1,
        CloseTimeout = 2
    }
}
