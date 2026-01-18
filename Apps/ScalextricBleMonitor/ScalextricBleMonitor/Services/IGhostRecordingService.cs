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
    public required int SlotNumber { get; init; }

    /// <summary>
    /// The recorded lap data.
    /// </summary>
    public required RecordedLap RecordedLap { get; init; }

    /// <summary>
    /// The true event time when the lap ended (delay-adjusted).
    /// This is also the start time of the next lap for multi-lap recording.
    /// </summary>
    public required DateTime TrueLapEndTime { get; init; }
}

/// <summary>
/// Event args when a lap recording starts (first finish line crossing).
/// </summary>
public class LapRecordingStartedEventArgs : EventArgs
{
    /// <summary>
    /// The slot number (1-6) that started recording.
    /// </summary>
    public required int SlotNumber { get; init; }
}

/// <summary>
/// Service for recording throttle data during laps for ghost car playback.
/// Records throttle samples from controller input and creates RecordedLap objects.
///
/// Recording follows a two-phase process:
/// 1. After StartRecording(), waits for first finish line crossing (lap START)
/// 2. Records throttle samples until second finish line crossing (lap END)
///
/// This ensures complete laps are captured rather than partial ones.
/// </summary>
public interface IGhostRecordingService
{
    /// <summary>
    /// Raised when lap recording actually starts (first finish line crossing).
    /// This indicates the car crossed the finish line and sample recording has begun.
    /// </summary>
    event EventHandler<LapRecordingStartedEventArgs>? RecordingStarted;

    /// <summary>
    /// Raised when a lap recording is completed (car crosses finish line for second time).
    /// </summary>
    event EventHandler<LapRecordingCompletedEventArgs>? RecordingCompleted;

    /// <summary>
    /// Checks if recording is active for a specific slot.
    /// Returns true if waiting for lap start OR actively recording.
    /// </summary>
    /// <param name="slotNumber">The slot number (1-6).</param>
    /// <returns>True if recording is active (either phase) for the slot.</returns>
    bool IsRecording(int slotNumber);

    /// <summary>
    /// Starts recording throttle data for the specified slot.
    /// The service will wait for the first finish line crossing (lap start),
    /// then record samples until the second crossing (lap end).
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
    /// Continues recording for the next lap, starting from the specified time.
    /// Use this when the finish line crossing that ended one lap is also the start of the next.
    /// Unlike StartRecording, this skips the "waiting for lap start" phase.
    /// </summary>
    /// <param name="slotNumber">The slot number (1-6) to record.</param>
    /// <param name="lapStartTime">The true event time of the lap start (the previous lap's end time).</param>
    void ContinueRecording(int slotNumber, DateTime lapStartTime);

    /// <summary>
    /// Records a throttle sample for the specified slot.
    /// Call this method when throttle notification data is received.
    /// Samples are only recorded after the first finish line crossing.
    /// The throttle value is scaled by the power level to capture the actual power delivered.
    /// </summary>
    /// <param name="slotNumber">The slot number (1-6).</param>
    /// <param name="throttleValue">The raw throttle value from the controller (0-63).</param>
    /// <param name="powerLevel">The power level/multiplier in effect during recording (0-63).</param>
    /// <param name="timestamp">The timestamp of the sample.</param>
    void RecordThrottleSample(int slotNumber, byte throttleValue, int powerLevel, DateTime timestamp);

    /// <summary>
    /// Notifies the service that a finish line was crossed for the specified slot.
    /// First crossing: marks lap start and begins recording samples.
    /// Second crossing: finalizes recording and raises RecordingCompleted.
    /// </summary>
    /// <param name="slotNumber">The slot number (1-6).</param>
    /// <param name="lapTimeSeconds">The lap time in seconds (from powerbase timestamps).</param>
    /// <param name="trueEventTime">The delay-adjusted wall-clock time when the event actually occurred.</param>
    void NotifyLapCompleted(int slotNumber, double lapTimeSeconds, DateTime trueEventTime);

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

    /// <summary>
    /// Loads recorded laps from persistent storage.
    /// </summary>
    void LoadFromStorage();

    /// <summary>
    /// Saves all recorded laps to persistent storage.
    /// </summary>
    void SaveToStorage();
}
