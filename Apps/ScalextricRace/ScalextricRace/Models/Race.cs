namespace ScalextricRace.Models;

/// <summary>
/// Represents a race configuration with optional stages.
/// </summary>
public class Race
{
    /// <summary>
    /// Well-known ID for the default race that cannot be deleted.
    /// </summary>
    public static readonly Guid DefaultRaceId = new("00000000-0000-0000-0000-000000000003");

    /// <summary>
    /// Unique identifier for this race configuration.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name for the race configuration.
    /// </summary>
    public string Name { get; set; } = "New Race";

    /// <summary>
    /// Optional path to race image.
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// Free practice stage configuration.
    /// </summary>
    public RaceStage FreePractice { get; set; } = new() { LapCount = 5, TimeMinutes = 5 };

    /// <summary>
    /// Qualifying stage configuration.
    /// </summary>
    public RaceStage Qualifying { get; set; } = new() { LapCount = 3, TimeMinutes = 3 };

    /// <summary>
    /// Race stage configuration.
    /// </summary>
    public RaceStage RaceStage { get; set; } = new() { LapCount = 10, TimeMinutes = 10 };

    /// <summary>
    /// Creates the default race configuration.
    /// </summary>
    public static Race CreateDefault() => new()
    {
        Id = DefaultRaceId,
        Name = "Standard Race"
    };
}
