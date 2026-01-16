using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ScalextricRace.Services;
using ScalextricRace.ViewModels;
using ScalextricRace.Views;

namespace ScalextricRace;

/// <summary>
/// Avalonia application class. Handles application startup and DI container configuration.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Gets the application's service provider for dependency injection.
    /// </summary>
    public static IServiceProvider? Services { get; private set; }

    /// <summary>
    /// Loads the application XAML resources.
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Called when the Avalonia framework has completed initialization.
    /// Configures the DI container and creates the main window.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        // Build the dependency injection container
        Services = ConfigureServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Resolve the main view model from DI
            var mainViewModel = Services.GetRequiredService<MainViewModel>();

            // Create and configure the main window
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Configures the dependency injection container with all required services.
    /// </summary>
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Register settings (loaded from disk)
        services.AddSingleton(_ => AppSettings.Load());

        // Register services
#if WINDOWS
        services.AddSingleton<IBleService, BleService>();
#endif

        // Register ViewModels
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
