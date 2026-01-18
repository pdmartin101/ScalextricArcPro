using Avalonia;
using Scalextric;

namespace ScalextricRace;

/// <summary>
/// Application entry point.
/// </summary>
internal sealed class Program
{
    /// <summary>
    /// Main entry point for the application.
    /// Initialization code should go in App.axaml.cs OnFrameworkInitializationCompleted.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        // Initialize logging first
        LoggingConfiguration.Initialize("ScalextricRace", "scalextric-race-");

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            // Ensure logs are flushed on exit
            LoggingConfiguration.Shutdown();
        }
    }

    /// <summary>
    /// Builds the Avalonia application configuration.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
