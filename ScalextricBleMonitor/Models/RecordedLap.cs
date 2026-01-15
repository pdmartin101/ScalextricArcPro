using System;
using System.Collections.Generic;

namespace ScalextricBleMonitor.Models;

/// <summary>
/// Represents a recorded lap with throttle samples for ghost car playback.
/// </summary>
public class RecordedLap
{
    /// <summary>
    /// Unique identifier for this recorded lap.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// The slot number (1-6) this lap was recorded from.
    /// </summary>
    public int SlotNumber { get; init; }

    /// <summary>
    /// When this lap was recorded.
    /// </summary>
    public DateTime RecordedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// User-friendly name for this lap (e.g., "Best Lap", "Practice 1").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Total lap time in seconds.
    /// </summary>
    public double LapTimeSeconds { get; init; }

    /// <summary>
    /// The throttle samples captured during this lap.
    /// Ordered by timestamp from lap start.
    /// </summary>
    public List<ThrottleSample> Samples { get; init; } = [];

    /// <summary>
    /// Gets the total duration of this lap in centiseconds.
    /// Calculated from the last sample's timestamp.
    /// </summary>
    public uint DurationCentiseconds => Samples.Count > 0 ? Samples[^1].TimestampCentiseconds : 0;

    /// <summary>
    /// Gets a display-friendly string for this lap.
    /// </summary>
    public string DisplayName => string.IsNullOrEmpty(Name)
        ? $"Lap {LapTimeSeconds:F2}s ({RecordedAt:g})"
        : $"{Name} - {LapTimeSeconds:F2}s";

    /// <summary>
    /// Gets the number of samples in this lap.
    /// </summary>
    public int SampleCount => Samples.Count;
}
