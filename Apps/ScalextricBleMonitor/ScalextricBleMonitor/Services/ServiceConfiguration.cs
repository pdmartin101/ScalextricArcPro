using Microsoft.Extensions.DependencyInjection;
using ScalextricBle;
using ScalextricBleMonitor.ViewModels;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Configures dependency injection services for the application.
/// </summary>
public static class ServiceConfiguration
{
    /// <summary>
    /// Configures all application services.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection ConfigureServices(this IServiceCollection services)
    {
        // Register services
        services.AddSingleton<IBleMonitorService, BleMonitorService>();
        // Register IBleService pointing to the same instance for services that only need base interface
        services.AddSingleton<ScalextricBle.IBleService>(sp => sp.GetRequiredService<IBleMonitorService>());
        services.AddSingleton<IGhostRecordingService, GhostRecordingService>();
        services.AddSingleton<IGhostPlaybackService, GhostPlaybackService>();
        services.AddSingleton<IPowerHeartbeatService>(sp =>
            new PowerHeartbeatService(sp.GetRequiredService<ScalextricBle.IBleService>()));
        services.AddSingleton<ITimingCalibrationService, TimingCalibrationService>();
        services.AddSingleton<AppSettings>(_ => AppSettings.Load());

        // Register ViewModels
        services.AddSingleton<MainViewModel>();

        return services;
    }

    /// <summary>
    /// Builds the service provider from the configured services.
    /// </summary>
    /// <returns>The built service provider.</returns>
    public static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.ConfigureServices();
        return services.BuildServiceProvider();
    }
}
