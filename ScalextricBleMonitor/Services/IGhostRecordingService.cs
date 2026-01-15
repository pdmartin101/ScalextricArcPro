using System;
using System.Collections.Generic;
using ScalextricBleMonitor.Models;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Event args when a lap recording is completed.
/// </summary>
public class LapRecordingCompletedEventArgs : EventArgs
{
    /// <summary>
    /// The slot number (1-6) that completed the recording.
    /// </summary>
    public int SlotNumber { get; init; }

    /// <summary>
    /// The recorded lap data.
    /// </summary>
    public RecordedLap RecordedLap { get; init; } = null!;
}

/// <summary>
/// Service for recording throttle data during laps for ghost car playback.
/// Records throttle samples from controller input and creates RecordedLap objects.
/// </summary>
public interface IGhostRecordingService
{
    /// <summary>
    /// Raised when a lap recording is completed (car crosses finish line while recording).
    /// </summary>
    event EventHandler<LapRecordingCompletedEventArgs>? RecordingCompleted;

    /// <summary>
    /// Checks if recording is active for a specific slot.
    /// </summary>
    /// <param name="slotNumber">The slot number (1-6).</param>
    /// <returns>True if recording is active for the slot.</returns>
    bool IsRecording(int slotNumber);

    /// <summary>
    /// Starts recording throttle data for the specified slot.
    /// Recording will continue until the next lap completion or StopRecording is called.
    /// </summary>
    /// <param name="slotNumber">The slot number (1-6) to record.</param>
    void StartRecording(int slotNumber);

    /// <summary>
    /// Stops recording for the specified slot without saving.
    /// Use this to cancel a recording in progress.
    /// </summary>
    /// <param name="slotNumber">The slot number (1-6).</param>
    void StopRecording(int slotNumber);

    /// <summary>
    /// Records a throttle sample for the specified slot.
    /// Call this method when throttle notification data is received.
    /// </summary>
    /// <param name="slotNumber">The slot number (1-6).</param>
    /// <param name="throttleValue">The throttle value (0-63).</param>
    /// <param name="timestamp">The timestamp of the sample.</param>
    void RecordThrottleSample(int slotNumber, byte throttleValue, DateTime timestamp);

    /// <summary>
    /// Notifies the service that a lap was completed for the specified slot.
    /// If recording is active, this finalizes the recording and raises RecordingCompleted.
    /// </summary>
    /// <param name="slotNumber">The slot number (1-6).</param>
    /// <param name="lapTimeSeconds">The lap time in seconds.</param>
    void NotifyLapCompleted(int slotNumber, double lapTimeSeconds);

    /// <summary>
    /// Gets all recorded laps for the specified slot.
    /// </summary>
    /// <param name="slotNumber">The slot number (1-6).</param>
    /// <returns>Collection of recorded laps for the slot.</returns>
    IReadOnlyList<RecordedLap> GetRecordedLaps(int slotNumber);

    /// <summary>
    /// Gets all recorded laps across all slots.
    /// </summary>
    /// <returns>Collection of all recorded laps.</returns>
    IReadOnlyList<RecordedLap> GetAllRecordedLaps();

    /// <summary>
    /// Adds a recorded lap to the collection (for loading from persistence).
    /// </summary>
    /// <param name="lap">The recorded lap to add.</param>
    void AddRecordedLap(RecordedLap lap);

    /// <summary>
    /// Removes a recorded lap from the collection.
    /// </summary>
    /// <param name="lapId">The ID of the lap to remove.</param>
    /// <returns>True if the lap was found and removed.</returns>
    bool RemoveRecordedLap(Guid lapId);

    /// <summary>
    /// Clears all recorded laps.
    /// </summary>
    void ClearAllRecordedLaps();
}
