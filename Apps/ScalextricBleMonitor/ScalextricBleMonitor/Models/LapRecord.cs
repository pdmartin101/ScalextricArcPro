namespace ScalextricBleMonitor.Models;

/// <summary>
/// Represents lap timing data for a single slot/controller.
/// This is a pure domain model with no UI or framework dependencies.
/// </summary>
public class LapRecord
{
    /// <summary>
    /// The current lap number. 0 = not started, 1 = first lap in progress, etc.
    /// </summary>
    public int CurrentLap { get; set; }

    /// <summary>
    /// The most recently crossed lane (1 or 2), or 0 if no lane has been crossed yet.
    /// </summary>
    public int CurrentLane { get; set; }

    /// <summary>
    /// The time of the last completed lap in seconds, or 0 if no lap has been completed.
    /// </summary>
    public double LastLapTimeSeconds { get; set; }

    /// <summary>
    /// The best lap time in seconds, or 0 if no lap has been completed.
    /// </summary>
    public double BestLapTimeSeconds { get; set; }

    /// <summary>
    /// Resets all lap timing data.
    /// </summary>
    public void Reset()
    {
        CurrentLap = 0;
        CurrentLane = 0;
        LastLapTimeSeconds = 0;
        BestLapTimeSeconds = 0;
    }

    /// <summary>
    /// Records a new lap time and updates best if applicable.
    /// </summary>
    /// <param name="lapTimeSeconds">The lap time in seconds.</param>
    /// <returns>True if this is a new best lap time.</returns>
    public bool RecordLapTime(double lapTimeSeconds)
    {
        LastLapTimeSeconds = lapTimeSeconds;

        if (BestLapTimeSeconds == 0 || lapTimeSeconds < BestLapTimeSeconds)
        {
            BestLapTimeSeconds = lapTimeSeconds;
            return true;
        }

        return false;
    }
}
