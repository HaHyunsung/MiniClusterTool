namespace FabStack.Core
{
    public enum PumpState
    {
        Unknown = 0,    // 에러 후 등 실제 상태를 알 수 없는 경우
        Starting = 1,   // 기동 중 (정격 회전 도달 전)
        Running = 2,
        Stopping = 3,   // 정지 중
        Stopped = 4
    }

    public interface IPump
    {
        string Name { get; }                // 명칭
        bool Error { get; }                 // 에러 발생 여부
        int ErrorID { get; }                // 발생한 에러 ID - 구현 시 Enum으로 선언
        Permissive RunPermissive { get; }   // Run 동작 전 확인하는 허용 여부
        Permissive StopPermissive { get; }  // Stop 동작 전 확인하는 허용 여부
        PumpState State { get; }            // 동작 상태

        bool Run();      // Run 명령 - 반환값으로 명령 수용 여부 반환
        bool Stop();     // Stop 명령 - 반환값으로 명령 수용 여부 반환

        void Reset();
    }
}
