using System;
using FabStack.Core;

namespace FabStack.Core.Tests
{
    // 가짜 물리: 스캔마다 펌프·밸브 상태를 보고 압력 센서 값을 선형으로 바꾼다.
    // 펌프 Running AND 펌핑 밸브 Opened AND 벤트 Closed → 감압. 벤트 Opened → 승압.
    // 시뮬레이터(4주차)가 이 모델을 가져간다.
    public sealed class FakeVacuumModel
    {
        #region Fields
        private readonly FakeAnalogSensor sensor;
        private readonly IPump pump;
        private readonly IValve pumpValve;
        private readonly IValve ventValve;
        private readonly double pumpRate;        // 초당 감압량
        private readonly double ventRate;        // 초당 승압량
        private readonly double basePressure;    // 최저 도달 압력
        private readonly double atmPressure;     // 대기압
        private readonly TimeSpan scanPeriod;
        #endregion

        #region Properties
        public bool PumpEffective { get; set; } = true;    // false면 펌핑해도 압력이 안 내려감 (압력 도달 실패)
        #endregion

        #region Constructor
        public FakeVacuumModel(FakeAnalogSensor sensor, IPump pump, IValve pumpValve, IValve ventValve,
                               double pumpRate, double ventRate, double basePressure, double atmPressure,
                               TimeSpan scanPeriod, IScanCycle scanCycle)
        {
            this.sensor = sensor;
            this.pump = pump;
            this.pumpValve = pumpValve;
            this.ventValve = ventValve;
            this.pumpRate = pumpRate;
            this.ventRate = ventRate;
            this.basePressure = basePressure;
            this.atmPressure = atmPressure;
            this.scanPeriod = scanPeriod;

            scanCycle.Register(Scan);
        }
        #endregion

        #region Methods
        private void Scan()
        {
            double dt = scanPeriod.TotalSeconds;

            if (ventValve.State == ValveState.Opened)
                sensor.Value = Math.Min(atmPressure, sensor.Value + ventRate * dt);
            else if (PumpEffective && pump.State == PumpState.Running && pumpValve.State == ValveState.Opened)
                sensor.Value = Math.Max(basePressure, sensor.Value - pumpRate * dt);
        }
        #endregion
    }
}
