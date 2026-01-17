namespace ScalextricRace.Services;

/// <summary>
/// Interface for application settings, enabling unit test mocking.
/// </summary>
public interface IAppSettings
{
    /// <summary>
    /// Whether track power should be enabled automatically on startup after connection.
    /// </summary>
    bool StartWithPowerEnabled { get; set; }

    /// <summary>
    /// Startup power and throttle settings loaded on app launch.
    /// </summary>
    StartupSettings Startup { get; set; }

    /// <summary>
    /// Saves settings to disk.
    /// </summary>
    void Save();
}
