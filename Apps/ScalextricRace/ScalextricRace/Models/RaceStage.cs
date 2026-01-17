namespace ScalextricRace.Models;

/// <summary>
/// Configuration for a single stage of a race (Free Practice, Qualifying, or Race).
/// </summary>
public class RaceStage
{
    /// <summary>
    /// Whether this stage is included in the race.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether the stage is measured by laps or time.
    /// </summary>
    public RaceStageMode Mode { get; set; } = RaceStageMode.Laps;

    /// <summary>
    /// Number of laps for this stage (used when Mode = Laps).
    /// </summary>
    public int LapCount { get; set; } = 5;

    /// <summary>
    /// Duration in minutes for this stage (used when Mode = Time).
    /// </summary>
    public int TimeMinutes { get; set; } = 5;
}
