using Microsoft.Extensions.DependencyInjection;
using Scalextric;
using ScalextricRace.ViewModels;

namespace ScalextricRace.Services;

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
        // Register settings (loaded from disk)
        services.AddSingleton(_ => AppSettings.Load());

        // Register services
        services.AddSingleton<IDispatcherService, AvaloniaDispatcherService>();
#if WINDOWS
        services.AddSingleton<Scalextric.IBleService, ScalextricBle.BleService>();
        services.AddSingleton<IPowerHeartbeatService>(sp =>
            new PowerHeartbeatService(sp.GetRequiredService<Scalextric.IBleService>()));
#endif
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<ICarStorage, CarStorage>();
        services.AddSingleton<IDriverStorage, DriverStorage>();
        services.AddSingleton<IRaceStorage, RaceStorage>();

        // Register ViewModels
        services.AddSingleton<BleConnectionViewModel>();
        services.AddSingleton<CarManagementViewModel>();
        services.AddSingleton<DriverManagementViewModel>();
        services.AddSingleton<RaceManagementViewModel>();
        services.AddSingleton<PowerControlViewModel>();
        services.AddSingleton<RaceConfigurationViewModel>();
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
