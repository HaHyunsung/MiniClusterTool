namespace FabStack.Core
{
    public interface IAnalogSensor
    {
        string Name { get; }                // 명칭
        bool Error { get; }                 // 에러 발생 여부
        uint ErrorID { get; }                // 발생한 에러 ID - 구현 시 Enum으로 선언
        double RawValue { get; }              // Raw 값
        double Value { get; }                 // 가공된 결과 값
    }
}
