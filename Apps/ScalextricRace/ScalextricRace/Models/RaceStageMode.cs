namespace ScalextricRace.Models;

/// <summary>
/// Determines how a race stage duration is measured.
/// </summary>
public enum RaceStageMode
{
    /// <summary>
    /// Stage ends after completing a fixed number of laps.
    /// </summary>
    Laps,

    /// <summary>
    /// Stage ends after a fixed time duration.
    /// </summary>
    Time
}
