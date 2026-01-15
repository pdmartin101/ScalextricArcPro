namespace ScalextricBleMonitor.Models;

/// <summary>
/// Represents a single throttle sample captured during lap recording.
/// Immutable record type for efficient storage and comparison.
/// </summary>
/// <param name="TimestampCentiseconds">Timestamp relative to lap start, in centiseconds (1/100th second).</param>
/// <param name="ThrottleValue">Throttle value (0-63) at this timestamp.</param>
public record ThrottleSample(uint TimestampCentiseconds, byte ThrottleValue);
