namespace ScalextricRace.Models;

/// <summary>
/// Represents a slot car with its physical characteristics and power settings.
/// These values are tuned per car to provide optimal and safe racing settings.
/// </summary>
public class Car
{
    /// <summary>
    /// Unique identifier for the car.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name for the car (e.g., "Red Ferrari F1", "Blue McLaren").
    /// </summary>
    public string Name { get; set; } = "New Car";

    /// <summary>
    /// Optional path to car image for UI display.
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// Default power level for normal driving (0-63).
    /// This is the typical power setting for this car.
    /// </summary>
    public int DefaultPower { get; set; } = 63;

    /// <summary>
    /// Maximum ghost mode power without crashing (0-63).
    /// This is the fastest safe autonomous speed for this car on the track.
    /// Used as a safety cap for driver power limits.
    /// </summary>
    public int GhostMaxPower { get; set; } = 45;

    /// <summary>
    /// Minimum power to keep the car moving (0-63).
    /// Below this level, the car may stall on climbs or tight curves.
    /// </summary>
    public int MinPower { get; set; } = 10;

    /// <summary>
    /// Creates a new car with default values.
    /// </summary>
    public Car()
    {
    }

    /// <summary>
    /// Creates a new car with the specified name.
    /// </summary>
    /// <param name="name">Display name for the car.</param>
    public Car(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Creates a new car with full configuration.
    /// </summary>
    /// <param name="name">Display name for the car.</param>
    /// <param name="defaultPower">Default power level (0-63).</param>
    /// <param name="ghostMaxPower">Maximum ghost power without crashing (0-63).</param>
    /// <param name="minPower">Minimum power to keep moving (0-63).</param>
    public Car(string name, int defaultPower, int ghostMaxPower, int minPower)
    {
        Name = name;
        DefaultPower = Math.Clamp(defaultPower, 0, 63);
        GhostMaxPower = Math.Clamp(ghostMaxPower, 0, 63);
        MinPower = Math.Clamp(minPower, 0, 63);
    }
}
