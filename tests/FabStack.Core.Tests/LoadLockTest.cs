using System;
using FabStack.Core;
using FabStack.Cluster;

namespace FabStack.Core.Tests
{
    // 스캔 순서: 장치 6개 → 물리 모델 → 상태머신 → 모듈
    public class LoadLockTest
    {
        #region Fields
        private readonly FakeClock clock;
        private readonly FakeScanCycle scanCycle;
        private readonly FakeValve atmGate;
        private readonly FakeValve vacGate;
        private readonly FakePump pump;
        private readonly FakeValve pumpValve;
        private readonly FakeValve ventValve;
        private readonly FakeAnalogSensor pressure;
        private readonly FakeVacuumModel physics;
        private readonly VacuumChamber chamber;
        private readonly ModeSelector mode;
        private readonly LoadLock loadLock;
        #endregion

        #region Constructor
        public LoadLockTest()
        {
            TimeSpan scan = TimeSpan.FromMilliseconds(10);
            TimeSpan working = TimeSpan.FromMilliseconds(100);
            TimeSpan deviceTimeout = TimeSpan.FromSeconds(1);

            clock = new FakeClock();
            scanCycle = new FakeScanCycle(clock, scan);

            atmGate = new FakeValve("LL.AtmGate", ValveState.Closed, working, deviceTimeout, clock, scanCycle);
            vacGate = new FakeValve("LL.VacGate", ValveState.Closed, working, deviceTimeout, clock, scanCycle);
            pump = new FakePump("LL.Pump", PumpState.Stopped, working, deviceTimeout, clock, scanCycle);
            pumpValve = new FakeValve("LL.PumpValve", ValveState.Closed, working, deviceTimeout, clock, scanCycle);
            ventValve = new FakeValve("LL.Vent", ValveState.Closed, working, deviceTimeout, clock, scanCycle);
            pressure = new FakeAnalogSensor("LL.Pressure", 760, -1, 1000, scanCycle);
            physics = new FakeVacuumModel(pressure, pump, pumpValve, ventValve,
                                          pumpRate: 400, ventRate: 400, basePressure: 0.01, atmPressure: 760,
                                          scan, scanCycle);

            chamber = new VacuumChamber("LL", pump, pumpValve, ventValve, pressure,
                                        new VacuumSetpoints(VacuumLimit: 0.1, AtmosphereLimit: 750,
                                                            PumpDownTimeout: TimeSpan.FromSeconds(5),
                                                            VentTimeout: TimeSpan.FromSeconds(5)));

            mode = new ModeSelector(OperationMode.Auto);
            loadLock = new LoadLock("LL", atmGate, vacGate, chamber, mode,
                                    new TransitionTimeouts(Aborting: TimeSpan.FromSeconds(1)), clock, scanCycle);
        }
        #endregion

        #region Methods
        private void RunUntil(Func<bool> condition, TimeSpan max)
        {
            TimeSpan scan = TimeSpan.FromMilliseconds(10);
            TimeSpan elapsed = TimeSpan.Zero;
            while (!condition() && elapsed < max)
            {
                scanCycle.RunScans(scan);
                elapsed += scan;
            }
            Assert.True(condition(),
                $"조건 미충족. State={loadLock.State}, Step={loadLock.CurrentStep?.Name}, P={chamber.Pressure:F2}, " +
                $"PumpValve={pumpValve.State}, Vent={ventValve.State}, Pump={pump.State}, Fault={loadLock.Fault}");
        }

        private void ToHomed()
        {
            Assert.True(loadLock.Abort());
            RunUntil(() => loadLock.State == OperationState.Aborted, TimeSpan.FromSeconds(1));
            Assert.True(loadLock.Home());
            RunUntil(() => loadLock.State == OperationState.Homed, TimeSpan.FromSeconds(2));
        }

        private void ToIdle()
        {
            ToHomed();
            Assert.True(loadLock.Run());
            Assert.Equal(OperationState.Idle, loadLock.State);
        }

        [Fact]
        public void Home_ClosesAllValves_StartsPump_ThenHomed()
        {
            ToHomed();

            Assert.Equal(ValveState.Closed, atmGate.State);
            Assert.Equal(ValveState.Closed, vacGate.State);
            Assert.Equal(ValveState.Closed, pumpValve.State);
            Assert.Equal(ValveState.Closed, ventValve.State);
            Assert.Equal(PumpState.Running, pump.State);
            Assert.True(loadLock.StandStill);
            Assert.True(chamber.IsAtAtmosphere);
        }

        [Fact]
        public void RequestPumpDown_OpensPumpValve_ReachesVacuum_ThenIdle()
        {
            ToIdle();

            Assert.True(loadLock.RequestPumpDown());
            RunUntil(() => loadLock.State == OperationState.Running, TimeSpan.FromMilliseconds(100));
            RunUntil(() => pumpValve.State == ValveState.Opened, TimeSpan.FromSeconds(1));
            RunUntil(() => chamber.IsAtVacuum, TimeSpan.FromSeconds(5));
            RunUntil(() => loadLock.State == OperationState.Idle, TimeSpan.FromMilliseconds(100));

            Assert.False(loadLock.PumpDownRequested);
            Assert.Equal(ValveState.Opened, pumpValve.State);          // 진공 유지
            Assert.Equal(ValveState.Closed, ventValve.State);
            Assert.Null(loadLock.Fault);
        }

        [Fact]
        public void RequestVent_ClosesPumpValve_OpensVent_ReachesAtmosphere_ClosesVent()
        {
            ToIdle();
            Assert.True(loadLock.RequestPumpDown());
            RunUntil(() => chamber.IsAtVacuum && loadLock.State == OperationState.Idle, TimeSpan.FromSeconds(6));

            Assert.True(loadLock.RequestVent());
            RunUntil(() => pumpValve.State == ValveState.Closed, TimeSpan.FromSeconds(1));
            RunUntil(() => ventValve.State == ValveState.Opened, TimeSpan.FromSeconds(1));
            RunUntil(() => chamber.IsAtAtmosphere, TimeSpan.FromSeconds(5));
            RunUntil(() => loadLock.State == OperationState.Idle, TimeSpan.FromMilliseconds(300));

            Assert.False(loadLock.VentRequested);
            Assert.Equal(ValveState.Closed, ventValve.State);
            Assert.True(chamber.IsIsolated);
            Assert.Null(loadLock.Fault);
        }

        [Fact]
        public void VentValveNoResponse_ValveFault_ModuleAborts()
        {
            ToIdle();
            ventValve.RespondsToCommand = false;

            Assert.True(loadLock.RequestVent());
            RunUntil(() => loadLock.State == OperationState.Aborted, TimeSpan.FromSeconds(3));

            Assert.NotNull(loadLock.Fault);
            Assert.Equal("LL.Vent", loadLock.Fault.Source);
            Assert.Equal(ValveFault.OpenTimeout, loadLock.Fault.Code);
            Assert.False(loadLock.VentRequested);                       // Aborting이 요청 해제
            Assert.False(loadLock.Home());                              // Fault 중 Home 거부
        }

        [Fact]
        public void PressureNotReached_StepTimeout_ModuleAborts_AndIsolates()
        {
            ToIdle();
            physics.PumpEffective = false;

            Assert.True(loadLock.RequestPumpDown());
            RunUntil(() => loadLock.State == OperationState.Aborted, TimeSpan.FromSeconds(8));

            Assert.NotNull(loadLock.Fault);
            Assert.Equal("LL", loadLock.Fault.Source);                  // 공정 타임아웃은 모듈 이름으로
            Assert.Equal(SequenceFault.StepTimeout, loadLock.Fault.Code);
            Assert.Contains("진공 도달 대기", loadLock.Fault.Message);
            Assert.Equal(ValveState.Closed, pumpValve.State);           // Aborting이 격리
            Assert.Equal(PumpState.Running, pump.State);                // 펌프는 계속
        }

        [Fact]
        public void Interlock_VentOpenRejected_WhilePumpValveOpened()
        {
            ToIdle();
            Assert.True(loadLock.RequestPumpDown());
            RunUntil(() => pumpValve.State == ValveState.Opened, TimeSpan.FromSeconds(1));

            Assert.False(ventValve.Open());
            Assert.Contains("LL.PumpValve 열림", ventValve.OpenPermissive.BlockedReasons);
            Assert.Equal(ValveState.Closed, ventValve.State);

            Assert.False(atmGate.Open());                               // 격리 안 됨 + 대기압 아님
            Assert.False(loadLock.RequestVent());                       // 펌핑 요청 처리 중엔 벤트 요청 거부
        }
        #endregion
    }
}
