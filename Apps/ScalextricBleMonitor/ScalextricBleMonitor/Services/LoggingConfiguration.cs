using System;
using System.IO;
using Serilog;
using Serilog.Events;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Configures Serilog logging for the application.
/// </summary>
public static class LoggingConfiguration
{
    private static bool _isInitialized;

    /// <summary>
    /// Gets the path to the log file directory.
    /// </summary>
    public static string LogDirectory
    {
        get
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appDataPath, "ScalextricBleMonitor", "logs");
        }
    }

    /// <summary>
    /// Initializes the Serilog logging infrastructure.
    /// Call this once at application startup.
    /// </summary>
    public static void Initialize()
    {
        if (_isInitialized) return;

        var logPath = Path.Combine(LogDirectory, "scalextric-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Debug(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        _isInitialized = true;
        Log.Information("Logging initialized. Log directory: {LogDirectory}", LogDirectory);
    }

    /// <summary>
    /// Closes and flushes the logging infrastructure.
    /// Call this at application shutdown.
    /// </summary>
    public static void Shutdown()
    {
        Log.Information("Application shutting down");
        Log.CloseAndFlush();
        _isInitialized = false;
    }
}
