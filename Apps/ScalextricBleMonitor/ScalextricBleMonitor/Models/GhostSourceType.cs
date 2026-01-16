namespace ScalextricBleMonitor.Models;

/// <summary>
/// Ghost mode throttle source types.
/// Determines where the ghost car gets its throttle values from.
/// </summary>
public enum GhostSourceType
{
    /// <summary>Fixed speed - uses GhostThrottleLevel as constant throttle value.</summary>
    FixedSpeed,

    /// <summary>Recorded lap - replays throttle values from a previously recorded lap.</summary>
    RecordedLap
}
