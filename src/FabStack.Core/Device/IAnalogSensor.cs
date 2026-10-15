namespace FabStack.Core
{
    public interface IAnalogSensor : IFaultSource
    {
        string Name { get; }                // 명칭
        double RawValue { get; }            // Raw 값
        double Value { get; }               // 가공된 결과 값
    }
}
