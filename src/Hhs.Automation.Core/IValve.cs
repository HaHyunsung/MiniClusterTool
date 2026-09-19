namespace Hhs.Automation.Core
{
    public enum ValveState
    {
        Unknown = 0,    // 출력이 다 꺼져있거나, 출력과 입력이 반대로 들어오는 경우
        Opening = 1,    // 열리는 중
        Opened  = 2,
        Closing = 3,
        Closed  = 4
    }

    public interface IValve
    {
        string Name { get; }                // 명칭
        bool Error { get; }                 // 에러 발생 여부
        int ErrorID { get; }                // 발생한 에러 ID - 구현 시 Enum으로 선언
        Permissive OpenPermissive { get; }  // Open 동작 전 확인하는 허용 여부
        Permissive ClosePermissive { get; } // Close 동작 전 확인하는 허용 여부
        ValveState State { get; }           // 동작 상태

        Task<bool> OpenAsync(CancellationToken ct);     // Open 명령 - 반환값으로 수행 가능 여부 반환
        Task<bool> CloseAsync(CancellationToken ct);    // Close 명령 - 반환값으로 수행 가능 여부 반환
    }
}
