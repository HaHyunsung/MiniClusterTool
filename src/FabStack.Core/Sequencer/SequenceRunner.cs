using System;
using System.Collections.Generic;
using System.Text;

namespace FabStack.Core
{
    public sealed class SequenceRunner
    {
        #region Fields
        private readonly string owner;              // Fault.Source에 넣을 이름 (모듈 이름)
        private readonly OnDelayTimer timer;        // 지금 스텝의 경과 시간
        private IEnumerator<Step>? steps;
        #endregion

        #region Properties
        public Step? Current { get; private set; }            // 진행 중인 스텝. null = 스텝과 스텝 사이
        public bool IsActive => steps is not null;            // 시작했고 아직 안 끝남
        public bool IsCompleted { get; private set; }         // 끝까지 갔음
        public bool AtBoundary => Current is null;
        #endregion

        #region Constructor
        public SequenceRunner(string owner, IClock clock)
        {
            this.owner = owner;
            timer = new OnDelayTimer(clock);
        }
        #endregion

        #region Methods
        public void Start(IEnumerable<Step> sequence)
        {
            Cancel();
            steps = sequence.GetEnumerator();
            IsCompleted = false;
        }

        public void Cancel()
        {
            steps?.Dispose();
            steps = null;
            Current = null;
            IsCompleted = false;
            timer.Stop();
        }

        public Fault? Advance(bool hold)                      // 스캔마다 한 번. 실패하면 Fault 반환
        {
            if (steps is null) return null;

            // 1) 스텝 사이면 다음 스텝을 꺼낸다 (hold면 꺼내지 않는다)
            if (Current is null)
            {
                if (hold) return null;

                if (!steps.MoveNext())
                {
                    IsCompleted = true;
                    steps.Dispose();
                    steps = null;
                    return null;
                }

                Current = steps.Current;
                timer.Tick();

                if (Current.Action is not null && !Current.Action())
                    return Fail(SequenceFault.ActionRejected, "명령 거부");
            }

            // 2) 완료 확인 — 꺼낸 스캔이든 진행 중이든 매 스캔. 다음 스텝은 다음 스캔에 꺼낸다
            if (Current.Until is null || Current.Until())
            {
                Current = null;
                timer.Stop();
                return null;
            }

            // 3) 공정 타임아웃
            if (Current.Timeout is not null && timer.ElapsedTime() >= Current.Timeout)
                return Fail(SequenceFault.StepTimeout, "시간 초과");

            return null;
        }

        // 실패한 스텝은 끝난 것으로 친다. 다음 행동(Abort, 다음 스텝 진행)은 모듈이 정한다
        private Fault Fail(SequenceFault code, string reason)
        {
            Fault fault = new Fault(owner, code, $"{Current!.Name}: {reason}");
            Current = null;
            timer.Stop();
            return fault;
        }
        #endregion
    }
}
