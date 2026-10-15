namespace FabStack.Core
{
    public enum ValveState
    {
        Unknown = 0,    // 출력이 다 꺼져있거나, 출력과 입력이 반대로 들어오는 경우
        Opening = 1,    // 열리는 중
        Opened  = 2,
        Closing = 3,
        Closed  = 4
    }

    public interface IValve : IFaultSource, IStandStill
    {
        string Name { get; }                // 명칭
        Permissive OpenPermissive { get; }  // Open 동작 전 확인하는 허용 여부
        Permissive ClosePermissive { get; } // Close 동작 전 확인하는 허용 여부
        ValveState State { get; }           // 동작 상태

        bool Open();     // Open 명령 - 반환값으로 수행 가능 여부 반환
        bool Close();    // Close 명령 - 반환값으로 수행 가능 여부 반환
    }
}
