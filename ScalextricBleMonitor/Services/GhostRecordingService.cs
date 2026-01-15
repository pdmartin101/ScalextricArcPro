using System;
using System.Collections.Generic;
using System.Linq;
using ScalextricBleMonitor.Models;
using Serilog;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Service for recording throttle data during laps for ghost car playback.
/// Maintains recording state per slot and stores completed recordings.
/// </summary>
public class GhostRecordingService : IGhostRecordingService
{
    private const int MaxSlots = 6;

    // Per-slot recording state
    private readonly RecordingState[] _recordingStates = new RecordingState[MaxSlots];

    // Completed recordings organized by slot
    private readonly List<RecordedLap>[] _recordedLaps = new List<RecordedLap>[MaxSlots];

    /// <inheritdoc />
    public event EventHandler<LapRecordingCompletedEventArgs>? RecordingCompleted;

    public GhostRecordingService()
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            _recordingStates[i] = new RecordingState();
            _recordedLaps[i] = [];
        }
    }

    /// <inheritdoc />
    public bool IsRecording(int slotNumber)
    {
        ValidateSlotNumber(slotNumber);
        return _recordingStates[slotNumber - 1].IsActive;
    }

    /// <inheritdoc />
    public void StartRecording(int slotNumber)
    {
        ValidateSlotNumber(slotNumber);
        var state = _recordingStates[slotNumber - 1];

        state.IsActive = true;
        state.StartTime = null; // Will be set on first sample
        state.Samples.Clear();

        Log.Information("Started recording for slot {SlotNumber}", slotNumber);
    }

    /// <inheritdoc />
    public void StopRecording(int slotNumber)
    {
        ValidateSlotNumber(slotNumber);
        var state = _recordingStates[slotNumber - 1];

        if (state.IsActive)
        {
            state.IsActive = false;
            state.Samples.Clear();
            Log.Information("Stopped recording for slot {SlotNumber} (cancelled)", slotNumber);
        }
    }

    /// <inheritdoc />
    public void RecordThrottleSample(int slotNumber, byte throttleValue, DateTime timestamp)
    {
        ValidateSlotNumber(slotNumber);
        var state = _recordingStates[slotNumber - 1];

        if (!state.IsActive)
            return;

        // Set start time on first sample
        if (state.StartTime == null)
        {
            state.StartTime = timestamp;
        }

        // Calculate timestamp relative to lap start in centiseconds
        var elapsedMs = (timestamp - state.StartTime.Value).TotalMilliseconds;
        var centiseconds = (uint)(elapsedMs / 10.0);

        var sample = new ThrottleSample(centiseconds, throttleValue);
        state.Samples.Add(sample);
    }

    /// <inheritdoc />
    public void NotifyLapCompleted(int slotNumber, double lapTimeSeconds)
    {
        ValidateSlotNumber(slotNumber);
        var state = _recordingStates[slotNumber - 1];

        if (!state.IsActive)
            return;

        // Stop recording
        state.IsActive = false;

        // Create the recorded lap if we have samples
        if (state.Samples.Count > 0)
        {
            var recordedLap = new RecordedLap
            {
                SlotNumber = slotNumber,
                RecordedAt = DateTime.UtcNow,
                LapTimeSeconds = lapTimeSeconds,
                Samples = [.. state.Samples] // Copy the samples
            };

            // Add to the slot's recordings
            _recordedLaps[slotNumber - 1].Add(recordedLap);

            Log.Information(
                "Recording completed for slot {SlotNumber}: {SampleCount} samples, {LapTime:F2}s",
                slotNumber, recordedLap.SampleCount, lapTimeSeconds);

            // Raise the event
            RecordingCompleted?.Invoke(this, new LapRecordingCompletedEventArgs
            {
                SlotNumber = slotNumber,
                RecordedLap = recordedLap
            });
        }
        else
        {
            Log.Warning("Recording completed for slot {SlotNumber} but no samples were captured", slotNumber);
        }

        // Clear samples for next recording
        state.Samples.Clear();
        state.StartTime = null;
    }

    /// <inheritdoc />
    public IReadOnlyList<RecordedLap> GetRecordedLaps(int slotNumber)
    {
        ValidateSlotNumber(slotNumber);
        return _recordedLaps[slotNumber - 1].AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<RecordedLap> GetAllRecordedLaps()
    {
        return _recordedLaps.SelectMany(list => list).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public void AddRecordedLap(RecordedLap lap)
    {
        ValidateSlotNumber(lap.SlotNumber);
        _recordedLaps[lap.SlotNumber - 1].Add(lap);
        Log.Debug("Added recorded lap for slot {SlotNumber}: {LapId}", lap.SlotNumber, lap.Id);
    }

    /// <inheritdoc />
    public bool RemoveRecordedLap(Guid lapId)
    {
        foreach (var slotLaps in _recordedLaps)
        {
            var lap = slotLaps.FirstOrDefault(l => l.Id == lapId);
            if (lap != null)
            {
                slotLaps.Remove(lap);
                Log.Information("Removed recorded lap {LapId} for slot {SlotNumber}", lapId, lap.SlotNumber);
                return true;
            }
        }
        return false;
    }

    /// <inheritdoc />
    public void ClearAllRecordedLaps()
    {
        foreach (var slotLaps in _recordedLaps)
        {
            slotLaps.Clear();
        }
        Log.Information("Cleared all recorded laps");
    }

    private static void ValidateSlotNumber(int slotNumber)
    {
        if (slotNumber < 1 || slotNumber > MaxSlots)
        {
            throw new ArgumentOutOfRangeException(nameof(slotNumber), $"Slot number must be 1-{MaxSlots}");
        }
    }

    /// <summary>
    /// Internal state for tracking an active recording.
    /// </summary>
    private class RecordingState
    {
        public bool IsActive { get; set; }
        public DateTime? StartTime { get; set; }
        public List<ThrottleSample> Samples { get; } = [];
    }
}
