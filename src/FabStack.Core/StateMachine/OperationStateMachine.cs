using System;
using System.Text;

namespace FabStack.Core
{
    public class OperationStateMachine : IFaultSource
    {
        #region Fields
        private readonly ModeSelector mode;
        private readonly TransitionTimeouts timeouts;
        private readonly IClock clock;                 // 이력 시각용 (아래 설명)
        private readonly OnDelayTimer timer;           // 전이 상태 진입 시각 (TON)
        private readonly List<StateTransition> history = new();
        private readonly OperationSequences sequences;
        private readonly SequenceRunner abortingRunner;
        private readonly SequenceRunner homingRunner;
        private readonly SequenceRunner runningRunner;
        #endregion

        #region Properties
        public string Name { get; }
        public OperationState State { get; private set; } = OperationState.None;
        public bool Resumed { get; private set; }      // PAUSED에서 Run으로 들어왔는가 (TcHit P_Resumed)
        public Permissive HomePermissive { get; }    // 층 2가 추가 조건 등록 (하위 에러 없음, 하위 전원 ABORTED 등)
        public Permissive RunPermissive { get; }
        public Permissive SemiAutoPermissive { get; }


        // 완료 조건 (전이 상태를 끝내도 되는가) — TcHit CompConditions / Paused·Stopped·RunningConditions
        public Permissive AbortedCondition { get; }
        public Permissive HomedCondition { get; }
        public Permissive PausedCondition { get; }
        public Permissive StoppedCondition { get; }
        public Permissive RunningCondition { get; }    // 충족이면 Running, 아니면 Idle (일감 유무)

        public IReadOnlyList<StateTransition> History => history;

        public Fault? Fault { get; private set; }

        public Step? CurrentStep => abortingRunner.Current ?? homingRunner.Current ?? runningRunner.Current;
        #endregion

        #region Constructor
        public OperationStateMachine(string name, ModeSelector mode, TransitionTimeouts timeouts, OperationSequences sequences, IClock clock, IScanCycle scanCycle)
        {
            Name = name;
            
            mode.Attach(this);
            this.mode = mode;

            this.timeouts = timeouts;
            this.clock = clock;
            timer = new OnDelayTimer(clock);

            HomePermissive = new Permissive();
            RunPermissive = new Permissive();
            SemiAutoPermissive = new Permissive();

            AbortedCondition = new Permissive();
            HomedCondition = new Permissive();
            PausedCondition = new Permissive(); 
            StoppedCondition = new Permissive();
            RunningCondition = new Permissive();

            this.sequences = sequences;
            abortingRunner = new SequenceRunner(name, clock);
            homingRunner = new SequenceRunner(name, clock);
            runningRunner = new SequenceRunner(name, clock);

            scanCycle.Register(Scan);
        }
        #endregion

        #region Methods
        private void StartSequence(SequenceRunner target, IEnumerable<Step> sequence)
        {
            homingRunner.Cancel();
            runningRunner.Cancel();
            abortingRunner.Cancel();

            target.Start(sequence);
        }

        public bool Abort()
        {
            if (State == OperationState.Aborting) return true;

            StartSequence(abortingRunner, sequences.Aborting());
            ChangeState(OperationState.Aborting, TransitionTrigger.Abort);
            return true;
        }

        public bool Home()
        {
            if (State != OperationState.Aborted) return false;
            if (mode.Mode is not (OperationMode.Maint or OperationMode.Auto)) return false;
            if (Fault is not null || !HomePermissive.IsAllowed) return false;

            StartSequence(homingRunner, sequences.Homing());
            ChangeState(OperationState.Homing, TransitionTrigger.Home);
            return true;
        }

        public bool Run()
        {
            if (State is not (OperationState.Homed or OperationState.Stopped or OperationState.Paused)) return false;
            if (mode.Mode is not (OperationMode.Auto)) return false;
            if (Fault is not null || !RunPermissive.IsAllowed) return false;
        
            Resumed = State == OperationState.Paused;
            if (!Resumed || !runningRunner.IsActive)       // 재개라도 Pausing 중 시퀀스가 끝났으면 새로 시작
                StartSequence(runningRunner, sequences.Running());

            ChangeState(OperationState.Idle, TransitionTrigger.Run);
            return true;
        }

        public bool Pause()
        {
            if (State is not (OperationState.Running or OperationState.Idle)) return false;
            if (mode.Mode is not (OperationMode.Auto)) return false;

            ChangeState(OperationState.Pausing, TransitionTrigger.Pause);
            return true;
        }

        public bool Stop()
        {
            if (State is not (OperationState.Running or OperationState.Idle)) return false;
            if (mode.Mode is not (OperationMode.Auto)) return false;

            ChangeState(OperationState.Stopping, TransitionTrigger.Stop);
            return true;
        }

        public bool SemiAuto()
        {
            if (State is not (OperationState.Homed)) return false;
            if (mode.Mode is not (OperationMode.Maint)) return false;
            if (Fault is not null || !SemiAutoPermissive.IsAllowed) return false;

            StartSequence(runningRunner, sequences.Running());
            ChangeState(OperationState.SemiAuto, TransitionTrigger.SemiAuto);
            return true;
        }

        private void ChangeState(OperationState next, TransitionTrigger trigger)
        {
            history.Add(new StateTransition(clock.Now, State, next, trigger));
            State = next;
            if (next is OperationState.Aborting or OperationState.Homing or OperationState.Pausing or OperationState.Stopping)
                timer.Tick();
            else timer.Stop();
        }

        private void Scan()
        {
            Fault? failure = State switch
            {
                OperationState.Aborting => abortingRunner.Advance(hold: false),
                OperationState.Homing => homingRunner.Advance(hold: false),
                OperationState.Idle or OperationState.Running or OperationState.Stopping or OperationState.SemiAuto
                    => runningRunner.Advance(hold: false),
                OperationState.Pausing or OperationState.Paused => runningRunner.Advance(hold: true),
                _ => null
            };
            Fault ??= failure;

            if (State == OperationState.Running && !RunningCondition.IsAllowed)
            {
                ChangeState(OperationState.Idle, TransitionTrigger.Activity);
                return;
            }
            else if (State == OperationState.Idle && RunningCondition.IsAllowed)
            {
                ChangeState(OperationState.Running, TransitionTrigger.Activity);
                return;
            }

            (bool sequenceDone, Permissive? condition, OperationState next, TimeSpan? timeout, OperationFault? fault) step = State switch
            {
                OperationState.Aborting => (abortingRunner.IsCompleted, AbortedCondition, OperationState.Aborted, timeouts.Aborting, OperationFault.AbortingTimeout),
                OperationState.Homing => (homingRunner.IsCompleted, HomedCondition, OperationState.Homed, timeouts.Homing, OperationFault.HomingTimeout),
                OperationState.Pausing => (runningRunner.AtBoundary, PausedCondition, OperationState.Paused, timeouts.Pausing, OperationFault.PausingTimeout),
                OperationState.Stopping => (runningRunner.IsCompleted, StoppedCondition, OperationState.Stopped, timeouts.Stopping, OperationFault.StoppingTimeout),
                _ => (false, null, State, null, null)
            };

            if (step.condition is null) return;
            else if (step.sequenceDone && step.condition.IsAllowed)
            {
                ChangeState(step.next, TransitionTrigger.Complete);
                return;
            }

            if (step.timeout is null) return;      // 감시 안 하는 전이
            else if(timer.ElapsedTime() >= step.timeout && Fault is null)
            {
                string waiting = step.sequenceDone
                    ? step.condition.BlockedReasonsText
                    : $"시퀀스 진행 중({CurrentStep?.Name ?? "스텝 전환 중"})";
                Fault = new Fault(Name, step.fault.Value, $"상태 변경 시간 초과: {waiting}");
                return;
            }
        }

        public void ClearFault()
        {
            if (Fault is not null &&
                State is OperationState.Aborting or OperationState.Homing or OperationState.Pausing or OperationState.Stopping)
                timer.Tick();

            Fault = null;
        }

        #endregion
    }
}
