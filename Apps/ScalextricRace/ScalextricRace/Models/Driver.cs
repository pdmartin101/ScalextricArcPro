namespace ScalextricRace.Models;

/// <summary>
/// Represents a driver with their name and skill-based power limit.
/// The PowerLimit effectively defines the driver's experience level -
/// lower limits for beginners, higher or no limit for experienced drivers.
/// </summary>
public class Driver
{
    /// <summary>
    /// Unique identifier for the driver.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name for the driver.
    /// </summary>
    public string Name { get; set; } = "New Driver";

    /// <summary>
    /// Optional path to driver image/avatar for UI display.
    /// Children love personalized avatars!
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// Maximum power level this driver can use as a percentage (50-100).
    /// Null means no limit - driver can use full car power (100%).
    /// Lower values = safer for beginners.
    /// </summary>
    public int? PowerLimit { get; set; }

    /// <summary>
    /// Creates a new driver with default values.
    /// </summary>
    public Driver()
    {
    }

    /// <summary>
    /// Creates a new driver with the specified name.
    /// </summary>
    /// <param name="name">Display name for the driver.</param>
    public Driver(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Creates a new driver with name and power limit.
    /// </summary>
    /// <param name="name">Display name for the driver.</param>
    /// <param name="powerLimit">Maximum power level (null for no limit).</param>
    public Driver(string name, int? powerLimit)
    {
        Name = name;
        PowerLimit = powerLimit.HasValue ? Math.Clamp(powerLimit.Value, 50, 100) : null;
    }

    /// <summary>
    /// The well-known ID for the default driver.
    /// This driver is always available and has no power limit.
    /// </summary>
    public static readonly Guid DefaultDriverId = new("00000000-0000-0000-0000-000000000002");

    /// <summary>
    /// Creates the default driver with no power limit.
    /// This driver is always available for quick racing.
    /// </summary>
    /// <returns>A driver with no power restrictions.</returns>
    public static Driver CreateDefault()
    {
        return new Driver
        {
            Id = DefaultDriverId,
            Name = "Default Driver",
            PowerLimit = null  // No limit - full power available
        };
    }
}
