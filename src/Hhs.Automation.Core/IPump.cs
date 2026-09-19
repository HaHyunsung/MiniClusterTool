namespace Hhs.Automation.Core
{
    public enum PumpState
    {
        Unknown = 0,
        Running = 1,
        Stopped = 2
    }

    public interface IPump
    {
        string Name { get; }                // 명칭
        bool Error { get; }                 // 에러 발생 여부
        int ErrorID { get; }                // 발생한 에러 ID - 구현 시 Enum으로 선언
        bool RunInterlock { get; }          // Run 동작 전 확인하는 Interlock 신호
        bool StopInterlock { get; }         // Stop 동작 전 확인하는 Interlock 신호
        PumpState State { get; }            // 동작 상태

        Task<bool> RunAsync(CancellationToken ct);      // Run 명령 - 반환값으로 수행 가능 여부 반환
        Task<bool> StopAsync(CancellationToken ct);     // Stop 명령 - 반환값으로 수행 가능 여부 반환
    }
}
