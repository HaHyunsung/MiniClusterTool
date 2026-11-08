using System;
using FabStack.Core;

namespace FabStack.Core.Tests
{
    // SampleModule로 상태머신 + 실행기 + 모듈을 한 번에 검증한다.
    // 스캔 등록 순서: 밸브 → 상태머신 → 모듈 (생성 순서가 곧 스캔 순서)
    public class ModuleBaseTest
    {
        #region Fields
        private readonly FakeClock clock;
        private readonly FakeScanCycle scanCycle;
        private readonly FakeValve valve;
        private readonly ModeSelector mode;
        private readonly SampleModule module;
        #endregion

        #region Constructor
        public ModuleBaseTest()
        {
            clock = new FakeClock();
            scanCycle = new FakeScanCycle(clock, TimeSpan.FromMilliseconds(10));
            valve = new FakeValve("V1", ValveState.Opened, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1), clock, scanCycle);
            mode = new ModeSelector(OperationMode.Auto);
            module = new SampleModule("M1", valve, mode, new TransitionTimeouts(Aborting: TimeSpan.FromSeconds(1)), clock, scanCycle);
        }
        #endregion

        #region Methods
        // 조건이 참이 될 때까지 스캔. 실패 시 어디서 멈췄는지 메시지에 남긴다
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
                $"조건 미충족. State={module.State}, Step={module.CurrentStep?.Name}, Valve={valve.State}, Fault={module.Fault}");
        }

        private void ToAborted()
        {
            Assert.True(module.Abort());
            scanCycle.RunScans(TimeSpan.FromMilliseconds(50));
            Assert.Equal(OperationState.Aborted, module.State);
        }

        private void ToHomed()
        {
            ToAborted();
            Assert.True(module.Home());
            RunUntil(() => module.State == OperationState.Homed, TimeSpan.FromMilliseconds(500));
        }

        private void ToRunningOpenStep()
        {
            ToHomed();
            Assert.True(module.Run());
            Assert.Equal(OperationState.Idle, module.State);           // HasWork false → Idle

            module.HasWork = true;
            RunUntil(() => module.CurrentStep?.Name == "밸브 열기", TimeSpan.FromMilliseconds(100));
            Assert.Equal(OperationState.Running, module.State);
            Assert.Equal(ValveState.Opening, valve.State);
        }

        [Fact]
        public void Abort_FromNone_ReachesAborted()
        {
            Assert.True(module.Abort());
            Assert.Equal(OperationState.Aborting, module.State);

            scanCycle.RunScans(TimeSpan.FromMilliseconds(50));          // 빈 Aborting 시퀀스 + 밸브 정지 → 첫 스캔에 완료
            Assert.Equal(OperationState.Aborted, module.State);
            Assert.True(module.StandStill);
        }

        [Fact]
        public void Abort_PropagatesOnce_ReentryDoesNotPropagate()
        {
            FakeAbortable fake = new FakeAbortable("F");
            module.AddForTest(fake);

            Assert.True(module.Abort());
            Assert.Equal(1, fake.AbortCount);

            Assert.True(module.Abort());                                // Aborting 중 재진입
            Assert.Equal(1, fake.AbortCount);
        }

        [Fact]
        public void Home_ClosesValve_ThenHomedAndStandStill()
        {
            ToAborted();

            Assert.True(module.Home());
            Assert.Equal(OperationState.Homing, module.State);

            scanCycle.RunScans(TimeSpan.FromMilliseconds(10));
            Assert.Equal("밸브 닫기", module.CurrentStep?.Name);
            Assert.Equal(ValveState.Closing, valve.State);
            Assert.False(module.StandStill);

            RunUntil(() => module.State == OperationState.Homed, TimeSpan.FromMilliseconds(500));
            Assert.Equal(ValveState.Closed, valve.State);
            Assert.Null(module.CurrentStep);
            Assert.True(module.StandStill);
        }

        [Fact]
        public void Home_ValveTimeout_CollectsFaultAndSelfAborts()
        {
            ToAborted();
            valve.RespondsToCommand = false;

            Assert.True(module.Home());
            RunUntil(() => module.State == OperationState.Aborted, TimeSpan.FromSeconds(2));

            Assert.NotNull(module.Fault);
            Assert.Equal("V1", module.Fault.Source);                     // 출처는 밸브
            Assert.Equal(ValveFault.CloseTimeout, module.Fault.Code);
            Assert.Equal(ValveState.Unknown, valve.State);
            Assert.False(module.Home());                                // Fault 중 Home 거부
        }

        [Fact]
        public void ClearFault_AfterValveTimeout_HomeAccepted()
        {
            ToAborted();
            valve.RespondsToCommand = false;
            Assert.True(module.Home());
            RunUntil(() => module.State == OperationState.Aborted, TimeSpan.FromSeconds(2));
            Assert.NotNull(module.Fault);

            valve.RespondsToCommand = true;
            module.ClearFault();
            Assert.Null(module.Fault);
            Assert.Null(valve.Fault);

            Assert.True(module.Home());
            RunUntil(() => module.State == OperationState.Homed, TimeSpan.FromMilliseconds(500));
            Assert.Equal(ValveState.Closed, valve.State);
        }

        [Fact]
        public void Pause_DuringStep_FinishesStepThenPauses_RunResumesNextStep()
        {
            ToRunningOpenStep();

            Assert.True(module.Pause());
            Assert.Equal(OperationState.Pausing, module.State);

            RunUntil(() => module.State == OperationState.Paused, TimeSpan.FromMilliseconds(500));
            Assert.Equal(ValveState.Opened, valve.State);               // 진행 중이던 열기는 끝까지
            Assert.Null(module.CurrentStep);                            // 다음 스텝은 꺼내지 않음
            Assert.True(module.StandStill);

            Assert.True(module.Run());                                  // 재개
            RunUntil(() => module.CurrentStep?.Name == "밸브 닫기", TimeSpan.FromMilliseconds(100));
            Assert.Equal(ValveState.Closing, valve.State);              // 열기를 다시 하지 않고 닫기부터
        }

        [Fact]
        public void Stop_DuringCycle_FinishesCycleThenStopped()
        {
            ToRunningOpenStep();

            Assert.True(module.Stop());
            Assert.Equal(OperationState.Stopping, module.State);

            RunUntil(() => module.State == OperationState.Stopped, TimeSpan.FromSeconds(1));
            Assert.Equal(1, module.CycleCount);                         // 사이클은 끝냄
            Assert.Equal(ValveState.Closed, valve.State);
            Assert.True(module.HasWork);                                // 일감이 남아도 새 사이클은 시작하지 않음
            Assert.Null(module.CurrentStep);
        }

        [Fact]
        public void ModeChange_ToMaintOrAuto_RejectedWhileValveMoving()
        {
            ToAborted();
            Assert.True(mode.Change(OperationMode.Manual));             // 생성자가 Auto라 먼저 Manual로

            Assert.True(valve.Close());                                 // 수동 조작 → Closing (동작 중)
            Assert.Equal(ValveState.Closing, valve.State);
            Assert.False(module.StandStill);

            Assert.False(mode.Change(OperationMode.Auto));
            Assert.False(mode.Change(OperationMode.Maint));
            Assert.Equal(OperationMode.Manual, mode.Mode);

            RunUntil(() => valve.State == ValveState.Closed, TimeSpan.FromMilliseconds(200));
            Assert.True(mode.Change(OperationMode.Auto));
        }
        #endregion
    }
}
