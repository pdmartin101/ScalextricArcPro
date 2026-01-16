namespace ScalextricRace.Models;

/// <summary>
/// Defines the main navigation modes/pages within the application.
/// </summary>
public enum NavigationMode
{
    /// <summary>
    /// Race mode - select race type and start racing.
    /// This is the default mode on startup.
    /// </summary>
    Race,

    /// <summary>
    /// Cars mode - manage car configurations.
    /// View, add, edit, and delete cars.
    /// </summary>
    Cars,

    /// <summary>
    /// Drivers mode - manage driver profiles.
    /// View, add, edit, and delete drivers.
    /// </summary>
    Drivers,

    /// <summary>
    /// Settings mode - application configuration.
    /// </summary>
    Settings
}
