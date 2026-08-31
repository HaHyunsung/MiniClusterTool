using System.Configuration;
using System.Data;
using System.Windows;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using MiniCluster.App; // <- 이 줄 추가

namespace MiniCluster.Ui;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Serilog 파일 로거 설정 (logs/host-.log)
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("logs/host-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        // 2. DI 컨테이너 구성
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });
        // App 프로젝트의 서비스 등록
        services.ConfigureServices();
        // UI 창(MainWindow)을 DI 컨테이너에 등록
        services.AddSingleton<MainWindow>();
        _serviceProvider = services.BuildServiceProvider();
        // 3. 시작 로그 출력 및 MainWindow 실행
        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Host Controller UI 가동 시작");
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

