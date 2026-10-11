using System;
using System.Collections.Generic;
using System.Text;

namespace FabStack.Core
{
    public abstract class ModuleBase : IFaultSource, IStandStill, IAbortable
    {
        #region Fields
        private readonly List<IFaultSource> components = new();
        #endregion

        #region Properties
        public string Name { get; }
        public OperationState State => StateMachine.State;
        public IReadOnlyList<StateTransition> History => StateMachine.History;
        public Step? CurrentStep => StateMachine.CurrentStep;
        public Fault? Fault { get; private set; }             // 이 모듈에서 처음 생긴 Fault (출처 포함)
        public bool StandStill =>
            (State is OperationState.Aborted or OperationState.Homed or OperationState.Paused or OperationState.Stopped)
            && ComponentsStandStill;

        protected OperationStateMachine StateMachine { get; }
        protected bool StopRequested => State == OperationState.Stopping;
        private bool ComponentsStandStill => components.OfType<IStandStill>().All(c => c.StandStill);
        #endregion

        #region Constructor
        protected ModuleBase(string name, ModeSelector mode, TransitionTimeouts timeouts, IClock clock, IScanCycle scanCycle)
        {
            Name = name;

            StateMachine = new OperationStateMachine(name, mode, timeouts, new OperationSequences(Aborting, Homing, Running), clock, scanCycle);

            StateMachine.AbortedCondition.AddCondition(() => ComponentsStandStill, "Components StandStill 미충족");
            StateMachine.HomedCondition.AddCondition(() => ComponentsStandStill, "Components StandStill 미충족");
            StateMachine.PausedCondition.AddCondition(() => ComponentsStandStill, "Components StandStill 미충족");
            StateMachine.StoppedCondition.AddCondition(() => ComponentsStandStill, "Components StandStill 미충족");

            StateMachine.HomePermissive.AddCondition(() => Fault is null, "모듈 Fault 상태");
            StateMachine.RunPermissive.AddCondition(() => Fault is null, "모듈 Fault 상태");
            StateMachine.SemiAutoPermissive.AddCondition(() => Fault is null, "모듈 Fault 상태");

            mode.AttachStandStill(this);
            scanCycle.Register(Scan);
        }
        #endregion

        #region Methods
        public bool Abort()
        {
            if (State == OperationState.Aborting) return true;   // 재진입: 전파하지 않는다

            StateMachine.Abort();                                 // 상태 변경 + Aborting 시퀀스 시작
            foreach (IAbortable abortable in components.OfType<IAbortable>())
                abortable.Abort();
            return true;
        }
        public bool Home() => StateMachine.Home();
        public bool Run() => StateMachine.Run();
        public bool Pause() => StateMachine.Pause();
        public bool Stop() => StateMachine.Stop();
        public bool SemiAuto() => StateMachine.SemiAuto();

        protected virtual bool AbortOnFault(Fault fault) => true;

        public void ClearFault()
        {
            Fault = null;
            StateMachine.ClearFault();

            foreach (IFaultSource component in components)
                component.ClearFault();
        }

        protected void RaiseFault(Enum code, string message) => Fault ??= new Fault(Name, code, message);

        protected void AddComponent(IFaultSource component)
        {
            if (!components.Contains(component))
                components.Add(component);
        }

        protected abstract IEnumerable<Step> Aborting();
        protected abstract IEnumerable<Step> Homing();
        protected abstract IEnumerable<Step> Running();

        private void Scan()
        {
            Fault ??= components.Select(c => c.Fault).FirstOrDefault(f => f is not null) ?? StateMachine.Fault;

            if (Fault is not null
                && (State is OperationState.Homing or OperationState.Idle or OperationState.Running
                          or OperationState.Pausing or OperationState.Stopping or OperationState.SemiAuto)
                && AbortOnFault(Fault))
                Abort();
        }
        #endregion
    }
}
