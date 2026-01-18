using Microsoft.Extensions.DependencyInjection;
using Scalextric;
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
        services.AddSingleton<IBleService, BleService>();
        // Register Scalextric.IBleService pointing to the same instance for services that only need base interface
        services.AddSingleton<Scalextric.IBleService>(sp => sp.GetRequiredService<IBleService>());
        services.AddSingleton<IGhostRecordingService, GhostRecordingService>();
        services.AddSingleton<IGhostPlaybackService, GhostPlaybackService>();
        services.AddSingleton<IPowerHeartbeatService>(sp =>
            new PowerHeartbeatService(sp.GetRequiredService<Scalextric.IBleService>()));
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
