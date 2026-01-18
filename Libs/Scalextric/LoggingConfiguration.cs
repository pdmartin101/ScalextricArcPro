using System;
using System.IO;
using Serilog;
using Serilog.Events;

namespace Scalextric;

/// <summary>
/// Configures Serilog logging for Scalextric applications.
/// </summary>
public static class LoggingConfiguration
{
    private static bool _isInitialized;
    private static string _logDirectory = string.Empty;

    /// <summary>
    /// Gets the path to the log file directory.
    /// </summary>
    public static string LogDirectory => _logDirectory;

    /// <summary>
    /// Initializes the Serilog logging infrastructure.
    /// Call this once at application startup.
    /// </summary>
    /// <param name="appName">The application name (e.g., "ScalextricRace", "ScalextricBleMonitor").</param>
    /// <param name="logFilePrefix">The log file name prefix (e.g., "scalextric-race-", "scalextric-").</param>
    public static void Initialize(string appName, string logFilePrefix = "scalextric-")
    {
        if (_isInitialized) return;

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _logDirectory = Path.Combine(appDataPath, "ScalextricPdm", appName, "logs");

        var logPath = Path.Combine(_logDirectory, $"{logFilePrefix}.log");

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
        Log.Information("Logging initialized. Log directory: {LogDirectory}", _logDirectory);
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
