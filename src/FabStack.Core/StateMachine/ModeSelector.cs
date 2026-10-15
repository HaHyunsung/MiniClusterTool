using System;
using System.Collections.Generic;
using System.Text;

namespace FabStack.Core
{
    public class ModeSelector
    {
        #region Fields
        private readonly List<OperationStateMachine> members = new();   // 상태 규칙 대상 (상태머신 생성자가 등록)
        private readonly List<IStandStill> standStillMembers = new();   // 정지 규칙 대상 (ModuleBase 생성자가 등록)
        #endregion

        #region Properties
        public OperationMode Mode { get; private set; }

        // 장비별 추가 조건. 바꾸려는 모드마다 따로. 고정 규칙에 AND로만 붙는다
        public Permissive ManualPermissive { get; }
        public Permissive MaintPermissive { get; }
        public Permissive AutoPermissive { get; }
        #endregion

        #region Constructor
        public ModeSelector(OperationMode initMode)
        {
            Mode = initMode;

            ManualPermissive = new Permissive();
            MaintPermissive = new Permissive();
            AutoPermissive = new Permissive();
        }
        #endregion

        #region Methods
        internal void Attach(OperationStateMachine member)   // 상태머신 생성자가 자기 자신을 등록. 이미 있으면 무시
        {
            if (!members.Contains(member))
                members.Add(member);
        }

        public bool Change(OperationMode target)               // 고정 규칙(상태, Maint/Auto는 정지) AND 모드별 Permissive 통과 시 변경
        {
            if (target == Mode) return true;

            Permissive? permissive = target switch
            {
                OperationMode.Manual => ManualPermissive,
                OperationMode.Maint => MaintPermissive,
                OperationMode.Auto => AutoPermissive,
                _ => null
            };
            if (permissive is null || !permissive.IsAllowed) return false;

            bool statesOk = target == OperationMode.Manual
                ? members.All(m => m.State is OperationState.Aborted or OperationState.Aborting)
                : members.All(m => m.State is OperationState.Aborted or OperationState.Homed);
            if (!statesOk) return false;

            if (target != OperationMode.Manual && !standStillMembers.All(m => m.StandStill)) return false;

            Mode = target;
            return true;
        }

        internal void AttachStandStill(IStandStill member)
        {
            if (!standStillMembers.Contains(member))
                standStillMembers.Add(member);
        }
        #endregion
    }
}
