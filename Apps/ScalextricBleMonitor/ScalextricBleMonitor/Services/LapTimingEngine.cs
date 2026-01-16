using System;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Result of a lap timing update.
/// </summary>
public readonly struct LapTimingResult
{
    /// <summary>
    /// Whether a new lap was completed.
    /// </summary>
    public bool LapCompleted { get; init; }

    /// <summary>
    /// The current lap number (1 = first lap in progress, 2 = second lap in progress, etc.).
    /// </summary>
    public int CurrentLap { get; init; }

    /// <summary>
    /// The lane that was crossed (1 or 2), or 0 if no lane was crossed.
    /// </summary>
    public int CrossedLane { get; init; }

    /// <summary>
    /// The lap time in seconds for the completed lap, or 0 if no lap was completed
    /// or this was the first crossing (starting lap 1).
    /// </summary>
    public double LapTimeSeconds { get; init; }

    /// <summary>
    /// Whether this is a new best lap time.
    /// </summary>
    public bool IsNewBestLap { get; init; }

    /// <summary>
    /// The best lap time in seconds, or 0 if no lap has been completed.
    /// </summary>
    public double BestLapTimeSeconds { get; init; }

    /// <summary>
    /// Creates a result indicating no change occurred.
    /// </summary>
    public static LapTimingResult NoChange(int currentLap, int currentLane, double bestLapTime) => new()
    {
        LapCompleted = false,
        CurrentLap = currentLap,
        CrossedLane = currentLane,
        LapTimeSeconds = 0,
        IsNewBestLap = false,
        BestLapTimeSeconds = bestLapTime
    };
}

/// <summary>
/// Encapsulates lap timing logic for a single car/slot.
/// Extracted from ControllerViewModel for testability and separation of concerns.
///
/// The Scalextric ARC Pro powerbase has two finish line sensors (one per lane).
/// Each sensor records a timestamp (in centiseconds) when crossed. The higher
/// timestamp indicates which lane was crossed most recently.
/// </summary>
public class LapTimingEngine
{
    // Timestamp conversion factor: timestamps are in centiseconds (1/100th second = 10ms)
    private const double TimestampUnitsPerSecond = 100.0;

    // Track the highest timestamp seen - whichever lane was crossed most recently has the higher value
    private uint _lastMaxTimestamp;

    // Track whether we've established a valid baseline timestamp
    private bool _hasBaselineTimestamp;

    /// <summary>
    /// The current lap number. 0 = not started, 1 = first lap in progress, etc.
    /// </summary>
    public int CurrentLap { get; private set; }

    /// <summary>
    /// The most recently crossed lane (1 or 2), or 0 if no lane has been crossed yet.
    /// </summary>
    public int CurrentLane { get; private set; }

    /// <summary>
    /// The time of the last completed lap in seconds, or 0 if no lap has been completed.
    /// </summary>
    public double LastLapTimeSeconds { get; private set; }

    /// <summary>
    /// The best lap time in seconds, or 0 if no lap has been completed.
    /// </summary>
    public double BestLapTimeSeconds { get; private set; }

    /// <summary>
    /// Updates the lap timing state based on finish line sensor timestamps.
    /// </summary>
    /// <param name="lane1Timestamp">The finish line timestamp for lane 1 (centiseconds).</param>
    /// <param name="lane2Timestamp">The finish line timestamp for lane 2 (centiseconds).</param>
    /// <returns>A result indicating what changed.</returns>
    public LapTimingResult UpdateTimestamps(uint lane1Timestamp, uint lane2Timestamp)
    {
        // Take the higher of the two timestamps - that's the lane that was most recently crossed
        uint currentMaxTimestamp = Math.Max(lane1Timestamp, lane2Timestamp);

        // Ignore zero timestamps
        if (currentMaxTimestamp == 0)
            return LapTimingResult.NoChange(CurrentLap, CurrentLane, BestLapTimeSeconds);

        // First time seeing a non-zero timestamp after reset: this IS the first crossing.
        // The car has crossed the start line and is now on lap 1.
        if (!_hasBaselineTimestamp)
        {
            _lastMaxTimestamp = currentMaxTimestamp;
            _hasBaselineTimestamp = true;
            CurrentLap = 1;
            CurrentLane = lane1Timestamp >= lane2Timestamp ? 1 : 2;

            return new LapTimingResult
            {
                LapCompleted = true,  // First crossing counts as starting lap 1
                CurrentLap = CurrentLap,
                CrossedLane = CurrentLane,
                LapTimeSeconds = 0,   // No lap time yet - just started
                IsNewBestLap = false,
                BestLapTimeSeconds = 0
            };
        }

        // If the max timestamp changed, the car actually crossed a finish line
        if (currentMaxTimestamp != _lastMaxTimestamp)
        {
            // Determine which lane was crossed (whichever has the higher timestamp)
            int crossedLane = lane1Timestamp >= lane2Timestamp ? 1 : 2;

            // Increment lap count
            // CurrentLap 0 -> 1: Starting lap 1 (first crossing)
            // CurrentLap 1 -> 2: Finished lap 1, starting lap 2
            // CurrentLap 2 -> 3: Finished lap 2, starting lap 3, etc.
            CurrentLap++;
            CurrentLane = crossedLane;

            double lapTimeSeconds = 0;
            bool isNewBest = false;

            // Only calculate lap time if we just finished a lap (CurrentLap >= 2)
            // CurrentLap == 1 means we just started lap 1, no completed lap yet
            if (CurrentLap >= 2)
            {
                // Handle timestamp overflow (uint wraps at ~497 days of continuous operation)
                uint timeDiff = currentMaxTimestamp >= _lastMaxTimestamp
                    ? currentMaxTimestamp - _lastMaxTimestamp
                    : (uint.MaxValue - _lastMaxTimestamp) + currentMaxTimestamp + 1;
                lapTimeSeconds = timeDiff / TimestampUnitsPerSecond;

                // Record lap time
                LastLapTimeSeconds = lapTimeSeconds;

                // Update best lap time if this is a new best
                if (BestLapTimeSeconds == 0 || LastLapTimeSeconds < BestLapTimeSeconds)
                {
                    BestLapTimeSeconds = LastLapTimeSeconds;
                    isNewBest = true;
                }
            }

            // Update baseline for next lap
            _lastMaxTimestamp = currentMaxTimestamp;

            return new LapTimingResult
            {
                LapCompleted = true,
                CurrentLap = CurrentLap,
                CrossedLane = crossedLane,
                LapTimeSeconds = lapTimeSeconds,
                IsNewBestLap = isNewBest,
                BestLapTimeSeconds = BestLapTimeSeconds
            };
        }

        return LapTimingResult.NoChange(CurrentLap, CurrentLane, BestLapTimeSeconds);
    }

    /// <summary>
    /// Resets all lap timing state.
    /// </summary>
    public void Reset()
    {
        CurrentLap = 0;
        CurrentLane = 0;
        LastLapTimeSeconds = 0;
        BestLapTimeSeconds = 0;
        _lastMaxTimestamp = 0;
        _hasBaselineTimestamp = false;
    }
}
