using System;
using System.Collections.Generic;
using FabStack.Core;

namespace FabStack.Core.Tests
{
    public class SequenceRunnerTest
    {
        #region Fields
        private readonly FakeClock clock = new();
        private readonly SequenceRunner runner;
        private bool done;                                    // Until이 매 Advance마다 읽는 스위치
        #endregion

        #region Constructor
        public SequenceRunnerTest()
        {
            runner = new SequenceRunner("R", clock);
        }
        #endregion

        #region Methods
        private IEnumerable<Step> TwoSteps()
        {
            yield return new Step("A", Until: () => done);
            yield return new Step("B");                       // Until 없음 = 꺼낸 Advance에서 바로 끝
        }

        private IEnumerable<Step> TimeoutStep()
        {
            yield return new Step("A", Until: () => false, Timeout: TimeSpan.FromSeconds(1));
        }

        private IEnumerable<Step> RejectedStep()
        {
            yield return new Step("A", Action: () => false);
        }

        [Fact]
        public void Advance_ImmediateSteps_OneStepPerAdvance()
        {
            done = true;
            runner.Start(TwoSteps());
            Assert.True(runner.IsActive);

            Assert.Null(runner.Advance(hold: false));         // A 꺼내고 Until 참 → 같은 Advance에서 끝
            Assert.Null(runner.Current);
            Assert.False(runner.IsCompleted);

            Assert.Null(runner.Advance(hold: false));         // B
            Assert.Null(runner.Current);
            Assert.False(runner.IsCompleted);

            Assert.Null(runner.Advance(hold: false));         // MoveNext false → 완료
            Assert.True(runner.IsCompleted);
            Assert.False(runner.IsActive);
        }

        [Fact]
        public void Advance_Hold_FinishesCurrentButDoesNotPullNext()
        {
            done = false;
            runner.Start(TwoSteps());

            runner.Advance(hold: false);
            Assert.Equal("A", runner.Current?.Name);

            done = true;
            runner.Advance(hold: true);                       // 진행 중인 스텝은 끝낸다
            Assert.True(runner.AtBoundary);
            Assert.False(runner.IsCompleted);

            runner.Advance(hold: true);                       // 다음 스텝은 꺼내지 않는다
            Assert.True(runner.AtBoundary);
            Assert.False(runner.IsCompleted);

            runner.Advance(hold: false);                      // hold 해제 → B 꺼내고 끝
            Assert.True(runner.AtBoundary);
            runner.Advance(hold: false);
            Assert.True(runner.IsCompleted);
        }

        [Fact]
        public void Advance_StepTimeout_ReturnsFaultAndEndsStep()
        {
            runner.Start(TimeoutStep());

            Assert.Null(runner.Advance(hold: false));
            Assert.Equal("A", runner.Current?.Name);

            clock.Advance(TimeSpan.FromSeconds(1));
            Fault? fault = runner.Advance(hold: false);

            Assert.NotNull(fault);
            Assert.Equal(SequenceFault.StepTimeout, fault.Code);
            Assert.Equal("R", fault.Source);
            Assert.Contains("A", fault.Message);
            Assert.Null(runner.Current);                      // 실패한 스텝은 끝난 것으로 친다
            Assert.True(runner.IsActive);                     // 다음 행동은 실행기가 정하지 않는다
        }

        [Fact]
        public void Advance_ActionRejected_ReturnsFault()
        {
            runner.Start(RejectedStep());

            Fault? fault = runner.Advance(hold: false);

            Assert.NotNull(fault);
            Assert.Equal(SequenceFault.ActionRejected, fault.Code);
            Assert.Contains("A", fault.Message);
            Assert.Null(runner.Current);
        }
        #endregion
    }
}
