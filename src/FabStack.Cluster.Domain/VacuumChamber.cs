using System;
using System.Collections.Generic;
using FabStack.Core;

namespace FabStack.Cluster
{
    // 펌프·펌핑 밸브·벤트 밸브·압력 센서를 묶어 펌핑/벤팅 절차를 아는 부품.
    // 모듈이 아니다: 상태머신도 스캔도 없고, 모듈 시퀀스가 여기서 받은 스텝 목록을 끼워 넣는다.
    // 펌프는 상시 운전. 압력 제어는 펌핑 밸브 개폐로 한다.
    public sealed class VacuumChamber
    {
        #region Properties
        public string Name { get; }
        public IPump Pump { get; }
        public IValve PumpValve { get; }
        public IValve VentValve { get; }
        public IAnalogSensor PressureSensor { get; }
        public VacuumSetpoints Setpoints { get; }

        public double Pressure => PressureSensor.Value;
        public bool IsAtVacuum => Pressure <= Setpoints.VacuumLimit;
        public bool IsAtAtmosphere => Pressure >= Setpoints.AtmosphereLimit;
        public bool IsIsolated => PumpValve.State == ValveState.Closed && VentValve.State == ValveState.Closed;

        // 모듈이 AddComponent로 등록할 하위 장치
        public IEnumerable<IFaultSource> Components
        {
            get
            {
                yield return Pump;
                yield return PumpValve;
                yield return VentValve;
                yield return PressureSensor;
            }
        }
        #endregion

        #region Constructor
        public VacuumChamber(string name, IPump pump, IValve pumpValve, IValve ventValve, IAnalogSensor pressureSensor, VacuumSetpoints setpoints)
        {
            Name = name;
            Pump = pump;
            PumpValve = pumpValve;
            VentValve = ventValve;
            PressureSensor = pressureSensor;
            Setpoints = setpoints;

            // 챔버 자체의 인터락. 게이트 관련 인터락은 챔버를 쓰는 모듈이 등록한다
            PumpValve.OpenPermissive.AddCondition(() => VentValve.State == ValveState.Closed, $"{VentValve.Name} 열림");
            PumpValve.OpenPermissive.AddCondition(() => Pump.State == PumpState.Running, $"{Pump.Name} 미운전");
            VentValve.OpenPermissive.AddCondition(() => PumpValve.State == ValveState.Closed, $"{PumpValve.Name} 열림");
        }
        #endregion

        #region Methods
        // 벤트 닫기 → 펌핑 밸브 열기 → 진공 도달 대기. 도달 후에도 펌핑 밸브는 열어 둔다(진공 유지)
        public IEnumerable<Step> PumpDown()
        {
            yield return new Step($"{Name} 벤트 닫기", Action: VentValve.Close, Until: () => VentValve.State == ValveState.Closed);
            yield return new Step($"{Name} 펌핑 밸브 열기", Action: PumpValve.Open, Until: () => PumpValve.State == ValveState.Opened);
            yield return new Step($"{Name} 진공 도달 대기", Until: () => IsAtVacuum, Timeout: Setpoints.PumpDownTimeout);
        }

        // 펌핑 밸브 닫기 → 벤트 열기 → 대기압 도달 대기 → 벤트 닫기
        public IEnumerable<Step> Vent()
        {
            yield return new Step($"{Name} 펌핑 밸브 닫기", Action: PumpValve.Close, Until: () => PumpValve.State == ValveState.Closed);
            yield return new Step($"{Name} 벤트 열기", Action: VentValve.Open, Until: () => VentValve.State == ValveState.Opened);
            yield return new Step($"{Name} 대기압 도달 대기", Until: () => IsAtAtmosphere, Timeout: Setpoints.VentTimeout);
            yield return new Step($"{Name} 벤트 닫기", Action: VentValve.Close, Until: () => VentValve.State == ValveState.Closed);
        }

        // 챔버 격리: 펌핑 밸브·벤트 닫기. 펌프는 그대로. Homing/Aborting에서 사용
        public IEnumerable<Step> Isolate()
        {
            yield return new Step($"{Name} 펌핑 밸브 닫기", Action: PumpValve.Close, Until: () => PumpValve.State == ValveState.Closed);
            yield return new Step($"{Name} 벤트 닫기", Action: VentValve.Close, Until: () => VentValve.State == ValveState.Closed);
        }

        // 펌프 기동. 이미 Running이면 즉시 완료
        public IEnumerable<Step> StartPump()
        {
            yield return new Step($"{Name} 펌프 기동", Action: Pump.Run, Until: () => Pump.State == PumpState.Running);
        }
        #endregion
    }
}
