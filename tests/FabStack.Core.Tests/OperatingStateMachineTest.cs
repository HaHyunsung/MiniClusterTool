using System;
using System.Collections.Generic;
using System.Text;
using FabStack.Core;

namespace FabStack.Core.Tests
{
    public class OperatingStateMachineTest
    {
        #region Fields
        private readonly OperationStateMachine stateMachine;
        private readonly ModeSelector modeSelector;
        private readonly FakeClock clock;
        private readonly FakeScanCycle scanCycle;

        // 테스트에서 조건을 바꾸는 스위치 (람다가 이 값을 매 스캔 읽는다)
        private bool homeDone;
        private bool abortDone;
        private bool hasWork;

        #endregion

        #region Constructor
        public OperatingStateMachineTest()
        {
            clock = new FakeClock();
            scanCycle = new FakeScanCycle(clock, TimeSpan.FromMilliseconds(10));

            modeSelector = new ModeSelector(OperationMode.Manual);

            TransitionTimeouts timeouts = new TransitionTimeouts(
                                                TimeSpan.FromMilliseconds(1000),
                                                TimeSpan.FromSeconds(180),
                                                TimeSpan.FromSeconds(10),
                                                TimeSpan.FromSeconds(60));
            stateMachine = new OperationStateMachine("Test StateMachine", modeSelector, timeouts, clock, scanCycle);
        }
        #endregion

        #region Properties
        #endregion

        #region Methods
        [Fact]
        public void ChangeMode()
        {
            stateMachine.Abort();
            scanCycle.RunScans(TimeSpan.FromMilliseconds(100));

            Assert.True(modeSelector.Change(OperationMode.Auto));
            Assert.Equal(OperationMode.Auto, modeSelector.Mode);

            Assert.True(modeSelector.Change(OperationMode.Maint));
            Assert.Equal(OperationMode.Maint, modeSelector.Mode);

            stateMachine.Home();
            Assert.Equal(OperationState.Homing, stateMachine.State);
            scanCycle.RunScans(TimeSpan.FromMilliseconds(100));
            Assert.Equal(OperationState.Homed, stateMachine.State);
            Assert.True(modeSelector.Change(OperationMode.Auto));
            Assert.Equal(OperationMode.Auto, modeSelector.Mode);

            Assert.False(modeSelector.Change(OperationMode.Manual));
            Assert.Equal(OperationMode.Auto, modeSelector.Mode);

            stateMachine.Run();
            scanCycle.RunScans(TimeSpan.FromMilliseconds(100));
            Assert.Equal(OperationState.Running, stateMachine.State);
            Assert.False(modeSelector.Change(OperationMode.Manual));
            Assert.Equal(OperationMode.Auto, modeSelector.Mode);

            stateMachine.Abort();
            Assert.Equal(OperationState.Aborting, stateMachine.State);
            scanCycle.RunScans(TimeSpan.FromMilliseconds(100));
            Assert.Equal(OperationState.Aborted, stateMachine.State);
            Assert.True(modeSelector.Change(OperationMode.Manual));
            Assert.Equal(OperationMode.Manual, modeSelector.Mode);
        }
        #endregion

        #region Helpers
        // [Fact]가 없으므로 테스트로 실행되지 않는다. 각 테스트의 준비 단계로 쓴다.
        private void GoToAborted()
        {
            stateMachine.Abort();
            scanCycle.RunScans(TimeSpan.FromMilliseconds(20));
        }

        private void GoToHomed(OperationMode mode)
        {
            GoToAborted();
            modeSelector.Change(OperationMode.Maint);
            stateMachine.Home();
            scanCycle.RunScans(TimeSpan.FromMilliseconds(20));
            modeSelector.Change(mode);
        }
        #endregion

        #region Tests - 명령 수락
        [Fact]
        public void InitialState_IsNone_AndHomeIsRejected()
        {
            Assert.Equal(OperationState.None, stateMachine.State);
            Assert.False(stateMachine.Home());
        }

        [Fact]
        public void ModeChange_InNoneState_IsRejected()
        {
            Assert.False(modeSelector.Change(OperationMode.Auto));
            Assert.Equal(OperationMode.Manual, modeSelector.Mode);
        }

        [Fact]
        public void Abort_FromNone_GoesAbortingThenAborted()
        {
            Assert.True(stateMachine.Abort());
            Assert.Equal(OperationState.Aborting, stateMachine.State);

            scanCycle.RunScans(TimeSpan.FromMilliseconds(20));
            Assert.Equal(OperationState.Aborted, stateMachine.State);
        }

        [Fact]
        public void Home_InManualMode_IsRejected()
        {
            GoToAborted();
            Assert.False(stateMachine.Home());
            Assert.Equal(OperationState.Aborted, stateMachine.State);
        }

        [Fact]
        public void Home_InMaint_StaysHomingUntilConditionMet()
        {
            GoToAborted();
            modeSelector.Change(OperationMode.Maint);
            stateMachine.HomedCondition.AddCondition(() => homeDone, "Home 시퀀스 미완료");

            Assert.True(stateMachine.Home());
            scanCycle.RunScans(TimeSpan.FromMilliseconds(500));
            Assert.Equal(OperationState.Homing, stateMachine.State);

            homeDone = true;
            scanCycle.RunScans(TimeSpan.FromMilliseconds(20));
            Assert.Equal(OperationState.Homed, stateMachine.State);
        }

        [Fact]
        public void Run_InMaint_IsRejected()
        {
            GoToHomed(OperationMode.Maint);
            Assert.False(stateMachine.Run());
            Assert.Equal(OperationState.Homed, stateMachine.State);
        }

        [Fact]
        public void HomePermissive_NotMet_RejectsHome()
        {
            GoToAborted();
            modeSelector.Change(OperationMode.Maint);
            stateMachine.HomePermissive.AddCondition(() => false, "테스트용 금지 조건");

            Assert.False(stateMachine.Home());
            Assert.Equal(OperationState.Aborted, stateMachine.State);
        }

        [Fact]
        public void SemiAuto_OnlyFromHomedInMaint_AndExitsOnlyByAbort()
        {
            GoToHomed(OperationMode.Auto);
            Assert.False(stateMachine.SemiAuto());          // Auto에서는 거부

            modeSelector.Change(OperationMode.Maint);
            Assert.True(stateMachine.SemiAuto());
            Assert.Equal(OperationState.SemiAuto, stateMachine.State);

            Assert.False(stateMachine.Home());              // SemiAuto에서 다른 명령은 거부
            Assert.True(stateMachine.Abort());
            Assert.Equal(OperationState.Aborting, stateMachine.State);
        }
        #endregion

        #region Tests - 자동 전이
        [Fact]
        public void IdleAndRunning_FollowRunningCondition()
        {
            GoToHomed(OperationMode.Auto);
            stateMachine.RunningCondition.AddCondition(() => hasWork, "일감 없음");

            Assert.True(stateMachine.Run());
            scanCycle.RunScans(TimeSpan.FromMilliseconds(100));
            Assert.Equal(OperationState.Idle, stateMachine.State);

            hasWork = true;
            scanCycle.RunScans(TimeSpan.FromMilliseconds(20));
            Assert.Equal(OperationState.Running, stateMachine.State);

            hasWork = false;
            scanCycle.RunScans(TimeSpan.FromMilliseconds(20));
            Assert.Equal(OperationState.Idle, stateMachine.State);
            Assert.Equal(TransitionTrigger.Activity, stateMachine.History[^1].Trigger);
        }

        [Fact]
        public void Run_FromPaused_SetsResumed_FromStopped_DoesNot()
        {
            GoToHomed(OperationMode.Auto);

            stateMachine.Run();
            Assert.False(stateMachine.Resumed);
            scanCycle.RunScans(TimeSpan.FromMilliseconds(20));

            Assert.True(stateMachine.Pause());
            scanCycle.RunScans(TimeSpan.FromMilliseconds(20));
            Assert.Equal(OperationState.Paused, stateMachine.State);

            Assert.True(stateMachine.Run());
            Assert.True(stateMachine.Resumed);
            scanCycle.RunScans(TimeSpan.FromMilliseconds(20));

            Assert.True(stateMachine.Stop());
            scanCycle.RunScans(TimeSpan.FromMilliseconds(20));
            Assert.Equal(OperationState.Stopped, stateMachine.State);

            Assert.True(stateMachine.Run());
            Assert.False(stateMachine.Resumed);
        }
        #endregion

        #region Tests - 타임아웃과 에러
        [Fact]
        public void AbortingTimeout_LatchesError_AndStateStays()
        {
            stateMachine.AbortedCondition.AddCondition(() => abortDone, "정지 미완료");
            stateMachine.Abort();

            scanCycle.RunScans(TimeSpan.FromMilliseconds(990));
            Assert.False(stateMachine.Error);

            scanCycle.RunScans(TimeSpan.FromMilliseconds(20));
            Assert.True(stateMachine.Error);
            Assert.Equal((int)OperationFault.AbortingTimeout, stateMachine.ErrorID);
            Assert.Equal(OperationState.Aborting, stateMachine.State);
        }

        [Fact]
        public void AbortAgain_WhileAborting_DoesNotRestartTimer()
        {
            stateMachine.AbortedCondition.AddCondition(() => abortDone, "정지 미완료");
            stateMachine.Abort();
            scanCycle.RunScans(TimeSpan.FromMilliseconds(600));

            Assert.True(stateMachine.Abort());              // 재명령은 수용만
            scanCycle.RunScans(TimeSpan.FromMilliseconds(500));
            Assert.True(stateMachine.Error);                // 처음 Abort 기준 1.1초 → 타임아웃
        }

        [Fact]
        public void Error_RejectsNewCommands_ButAbortIsAccepted()
        {
            stateMachine.AbortedCondition.AddCondition(() => abortDone, "정지 미완료");
            stateMachine.Abort();
            scanCycle.RunScans(TimeSpan.FromMilliseconds(1100));
            Assert.True(stateMachine.Error);

            abortDone = true;
            scanCycle.RunScans(TimeSpan.FromMilliseconds(20));
            Assert.Equal(OperationState.Aborted, stateMachine.State);   // 에러 중에도 완료 전이는 진행

            modeSelector.Change(OperationMode.Maint);
            Assert.False(stateMachine.Home());              // 에러 중 새 명령 거부
            Assert.True(stateMachine.Abort());              // Abort는 항상 수락
        }

        [Fact]
        public void Reset_ClearsError_AndRestartsTransitionTimer()
        {
            stateMachine.AbortedCondition.AddCondition(() => abortDone, "정지 미완료");
            stateMachine.Abort();
            scanCycle.RunScans(TimeSpan.FromMilliseconds(1100));
            Assert.True(stateMachine.Error);

            stateMachine.Reset();
            Assert.False(stateMachine.Error);
            Assert.Equal(0, stateMachine.ErrorID);

            scanCycle.RunScans(TimeSpan.FromMilliseconds(500));
            Assert.False(stateMachine.Error);               // 바로 다시 서지 않는다

            scanCycle.RunScans(TimeSpan.FromMilliseconds(600));
            Assert.True(stateMachine.Error);                // Reset 기준 1.1초 → 다시 타임아웃
        }

        [Fact]
        public void HomingTimeout_ThenConditionMet_GoesHomed_ErrorStays_RunRejected()
        {
            GoToAborted();
            modeSelector.Change(OperationMode.Maint);
            stateMachine.HomedCondition.AddCondition(() => homeDone, "Home 시퀀스 미완료");
            stateMachine.Home();

            scanCycle.RunScans(TimeSpan.FromSeconds(181));
            Assert.True(stateMachine.Error);
            Assert.Equal((int)OperationFault.HomingTimeout, stateMachine.ErrorID);

            homeDone = true;
            scanCycle.RunScans(TimeSpan.FromMilliseconds(20));
            Assert.Equal(OperationState.Homed, stateMachine.State);
            Assert.True(stateMachine.Error);

            Assert.True(modeSelector.Change(OperationMode.Auto));
            Assert.False(stateMachine.Run());
        }

        [Fact]
        public void NullTimeout_NeverRaisesError()
        {
            ModeSelector otherMode = new ModeSelector(OperationMode.Manual);
            OperationStateMachine other = new OperationStateMachine("Other", otherMode,
                new TransitionTimeouts(TimeSpan.FromSeconds(1)), clock, scanCycle);   // Stopping 생략 → null

            other.Abort();
            scanCycle.RunScans(TimeSpan.FromMilliseconds(20));
            otherMode.Change(OperationMode.Maint);
            other.Home();
            scanCycle.RunScans(TimeSpan.FromMilliseconds(20));
            otherMode.Change(OperationMode.Auto);
            other.Run();
            other.StoppedCondition.AddCondition(() => false, "공정 진행 중");
            other.Stop();

            scanCycle.RunScans(TimeSpan.FromMinutes(10));
            Assert.Equal(OperationState.Stopping, other.State);
            Assert.False(other.Error);
        }
        #endregion

        #region Tests - 이력
        [Fact]
        public void History_RecordsEachTransition()
        {
            GoToAborted();
            modeSelector.Change(OperationMode.Maint);
            stateMachine.Home();

            Assert.Equal(3, stateMachine.History.Count);
            Assert.Equal(TransitionTrigger.Abort, stateMachine.History[0].Trigger);
            Assert.Equal(TransitionTrigger.Complete, stateMachine.History[1].Trigger);
            Assert.Equal(
                new StateTransition(clock.Now, OperationState.Aborted, OperationState.Homing, TransitionTrigger.Home),
                stateMachine.History[2]);                   // record는 값 비교
        }

        [Fact]
        public void RejectedCommand_AddsNoHistory()
        {
            GoToAborted();
            int before = stateMachine.History.Count;

            Assert.False(stateMachine.Home());              // Manual이라 거부
            Assert.False(stateMachine.Run());
            Assert.Equal(before, stateMachine.History.Count);
        }
        #endregion

        #region Tests - 모드
        [Fact]
        public void ModeChange_RejectedWhileAnyMemberIsRunning()
        {
            OperationStateMachine second = new OperationStateMachine("Second", modeSelector,
                new TransitionTimeouts(TimeSpan.FromSeconds(1)), clock, scanCycle);

            stateMachine.Abort();
            second.Abort();
            scanCycle.RunScans(TimeSpan.FromMilliseconds(20));
            Assert.True(modeSelector.Change(OperationMode.Maint));

            stateMachine.Home();
            second.Home();
            scanCycle.RunScans(TimeSpan.FromMilliseconds(20));
            Assert.True(modeSelector.Change(OperationMode.Auto));

            stateMachine.Run();
            scanCycle.RunScans(TimeSpan.FromMilliseconds(20));
            Assert.Equal(OperationState.Running, stateMachine.State);
            Assert.False(modeSelector.Change(OperationMode.Maint));   // 하나라도 Running이면 거부
        }

        [Fact]
        public void ModeChange_IsSharedByAllMembers()
        {
            OperationStateMachine second = new OperationStateMachine("Second", modeSelector,
                new TransitionTimeouts(TimeSpan.FromSeconds(1)), clock, scanCycle);

            stateMachine.Abort();
            second.Abort();
            scanCycle.RunScans(TimeSpan.FromMilliseconds(20));

            Assert.True(modeSelector.Change(OperationMode.Maint));
            Assert.True(stateMachine.Home());               // 둘 다 같은 Maint 모드를 본다
            Assert.True(second.Home());
        }

        [Fact]
        public void ModePermissive_AppliesOnlyToItsTargetMode()
        {
            GoToAborted();
            modeSelector.AutoPermissive.AddCondition(() => false, "도어 열림");

            Assert.False(modeSelector.Change(OperationMode.Auto));
            Assert.Equal(OperationMode.Manual, modeSelector.Mode);
            Assert.True(modeSelector.Change(OperationMode.Maint));
        }

        [Fact]
        public void ModeChange_ToSameMode_ReturnsTrue_EvenWhileRunning()
        {
            GoToHomed(OperationMode.Auto);
            stateMachine.Run();
            scanCycle.RunScans(TimeSpan.FromMilliseconds(20));

            Assert.True(modeSelector.Change(OperationMode.Auto));
        }
        #endregion
    }
}
