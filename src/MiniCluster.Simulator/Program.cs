using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace MiniCluster.Simulator;

internal class Program
{
    static void Main(string[] args)
    {
        // 1. Serilog 로거 설정 (콘솔 출력 및 logs 폴더에 파일 저장)
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("logs/simulator-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        // 2. DI 컨테이너에 서비스 등록
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });

        // 3. DI 컨테이너 빌드 (조립 완료)
        var serviceProvider = services.BuildServiceProvider();

        // 4. 로거를 주입받아 시작 로그 출력
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("MiniCluster Simulator 가동 시작");

        Console.WriteLine("시뮬레이터가 실행되었습니다. 종료하려면 아무 키나 누르세요.");
        Console.ReadKey();

        // 5. 종료 시 로거 자원 해제
        Log.CloseAndFlush();
    }
}