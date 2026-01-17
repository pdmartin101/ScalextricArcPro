namespace ScalextricRace.Models;

/// <summary>
/// Represents a driver with their name and power percentage.
/// The PowerPercentage acts as a multiplier on the car's power level -
/// lower percentages for beginners, 100% (or null) for experienced drivers.
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
    /// Power percentage for this driver (50-100).
    /// Multiplied with car power to get effective power level.
    /// Null means 100% - driver can use full car power.
    /// Lower values = safer for beginners.
    /// </summary>
    public int? PowerPercentage { get; set; }

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
    /// Creates a new driver with name and power percentage.
    /// </summary>
    /// <param name="name">Display name for the driver.</param>
    /// <param name="powerPercentage">Power percentage (null for 100%).</param>
    public Driver(string name, int? powerPercentage)
    {
        Name = name;
        PowerPercentage = powerPercentage.HasValue ? Math.Clamp(powerPercentage.Value, 50, 100) : null;
    }

    /// <summary>
    /// The well-known ID for the default driver.
    /// This driver is always available and has no power restriction.
    /// </summary>
    public static readonly Guid DefaultDriverId = new("00000000-0000-0000-0000-000000000002");

    /// <summary>
    /// Creates the default driver with 100% power.
    /// This driver is always available for quick racing.
    /// </summary>
    /// <returns>A driver with full power available.</returns>
    public static Driver CreateDefault()
    {
        return new Driver
        {
            Id = DefaultDriverId,
            Name = "Default Driver",
            PowerPercentage = null  // 100% - full power available
        };
    }
}
