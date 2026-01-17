namespace ScalextricRace.Models;

/// <summary>
/// Defines the top-level application modes.
/// </summary>
public enum AppMode
{
    /// <summary>
    /// Setup mode - configure cars, drivers, and races.
    /// </summary>
    Setup,

    /// <summary>
    /// Configure mode - set up car/driver pairings and race settings before starting.
    /// </summary>
    Configure,

    /// <summary>
    /// Racing mode - actively running a race.
    /// </summary>
    Racing
}
