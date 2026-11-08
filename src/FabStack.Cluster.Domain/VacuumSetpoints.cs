using System;

namespace FabStack.Cluster
{
    // 진공 챔버 설정값. 단위는 Torr 가정. 레시피 연동은 DB 단계에서
    public sealed record VacuumSetpoints(
        double VacuumLimit,             // 이하이면 진공 도달
        double AtmosphereLimit,         // 이상이면 대기압 도달
        TimeSpan PumpDownTimeout,       // 공정 타임아웃: 진공 도달까지
        TimeSpan VentTimeout);          // 공정 타임아웃: 대기압 도달까지
}
