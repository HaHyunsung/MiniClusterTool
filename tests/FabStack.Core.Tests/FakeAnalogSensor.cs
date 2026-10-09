namespace FabStack.Core.Tests
{
    // 테스트가 값을 직접 넣는 대역 센서. 벤치에서 강제 입력을 넣는 것과 같다.
    // 값이 유효 범위를 벗어나면 스캔에서 에러를 래칭한다 (단선, 오버레인지 대응).
    // 시간에 따른 값 변화(압력 곡선)는 4주차 시뮬레이터 몫이라 여기서는 다루지 않는다.
    public class FakeAnalogSensor : IAnalogSensor
    {
        #region Fields
        private readonly double minValid;
        private readonly double maxValid;
        #endregion

        #region Constructor
        public FakeAnalogSensor(string name, double initValue, double minValid, double maxValid, IScanCycle scanCycle)
        {
            Name = name;
            Value = initValue;
            this.minValid = minValid;
            this.maxValid = maxValid;

            scanCycle.Register(Scan);
        }
        #endregion

        #region Properties
        public string Name { get; }

        public Fault? Fault { get; private set; }

        // 스케일 변환이 없는 가짜 센서라 RawValue와 Value가 같다
        public double RawValue => Value;

        // IAnalogSensor는 get만 요구한다. set은 테스트 전용이며 인터페이스로 보는 상위 코드에서는 보이지 않는다
        public double Value { get; set; }
        #endregion

        #region Methods
        private void Scan()
        {
            if (Fault is not null) return;

            if (Value < minValid)
                Fault = new Fault(Name, AnalogSensorFault.UnderRange, $"하한 이탈 ({Value} < {minValid})");
            else if (Value > maxValid)
                Fault = new Fault(Name, AnalogSensorFault.OverRange, $"상한 이탈 ({Value} > {maxValid})");
        }

        public void ClearFault()
        {
            Fault = null;
        }
        #endregion
    }

    public enum AnalogSensorFault
    {
        UnderRange,
        OverRange
    }
}
