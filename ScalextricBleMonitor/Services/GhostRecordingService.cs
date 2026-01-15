using System;
using System.Collections.Generic;
using System.Linq;
using ScalextricBleMonitor.Models;
using Serilog;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Service for recording throttle data during laps for ghost car playback.
/// Maintains recording state per slot and stores completed recordings.
///
/// Recording phases:
/// 1. WaitingForLapStart - User pressed Record, buffering throttle samples
/// 2. Recording - Car crossed finish line (lap started), continuing to buffer samples
/// 3. Complete - Car crossed finish line again (lap finished), extract samples and save
///
/// Key insight: Slot notifications are delayed by 0-1.8s, but contain accurate timestamps.
/// We use the delay-adjusted "true event time" to correctly identify which buffered
/// throttle samples belong to the lap.
///
/// Buffer approach:
/// - Continuously buffer throttle samples with wall-clock timestamps
/// - On lap start: record the true event time (delay-adjusted)
/// - On lap end: extract samples between true start and true end times
/// - Timestamps are then made relative to lap start (0 = lap start)
/// </summary>
public class GhostRecordingService : IGhostRecordingService
{
    private const int MaxSlots = 6;
    private const int MaxBufferSize = 1000; // ~5 minutes at 3 samples/sec
    private const double MaxBufferAgeSeconds = 300.0; // 5 minutes

    // Per-slot recording state
    private readonly RecordingState[] _recordingStates = new RecordingState[MaxSlots];

    // Completed recordings organized by slot
    private readonly List<RecordedLap>[] _recordedLaps = new List<RecordedLap>[MaxSlots];

    /// <inheritdoc />
    public event EventHandler<LapRecordingStartedEventArgs>? RecordingStarted;

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
        var state = _recordingStates[slotNumber - 1];
        return state.Phase != RecordingPhase.Idle;
    }

    /// <inheritdoc />
    public void StartRecording(int slotNumber)
    {
        ValidateSlotNumber(slotNumber);
        var state = _recordingStates[slotNumber - 1];

        state.Phase = RecordingPhase.WaitingForLapStart;
        state.TrueLapStartTime = null;
        state.ThrottleBuffer.Clear();

        var rawTimeSec = DateTime.UtcNow.TimeOfDay.TotalSeconds;
        Log.Information("Started recording for slot {SlotNumber} at raw={RawTime:F2}s - buffering throttle samples, waiting for lap start",
            slotNumber, rawTimeSec);
    }

    /// <inheritdoc />
    public void StopRecording(int slotNumber)
    {
        ValidateSlotNumber(slotNumber);
        var state = _recordingStates[slotNumber - 1];

        if (state.Phase != RecordingPhase.Idle)
        {
            var wasPhase = state.Phase;
            state.Phase = RecordingPhase.Idle;
            state.ThrottleBuffer.Clear();
            state.TrueLapStartTime = null;
            Log.Information("Stopped recording for slot {SlotNumber} (cancelled from phase: {Phase})", slotNumber, wasPhase);
        }
    }

    /// <inheritdoc />
    public void ContinueRecording(int slotNumber, DateTime lapStartTime)
    {
        ValidateSlotNumber(slotNumber);
        var state = _recordingStates[slotNumber - 1];

        // Go directly to Recording phase with the lap start time already set
        state.Phase = RecordingPhase.Recording;
        state.TrueLapStartTime = lapStartTime;
        // Keep the buffer - it may have samples from before the lap start that we'll filter later

        var rawTimeSec = lapStartTime.TimeOfDay.TotalSeconds;
        Log.Information("Continuing recording for slot {SlotNumber} - lap started at trueTime={TrueTime:F2}s",
            slotNumber, rawTimeSec);

        // Raise the RecordingStarted event since we're now actively recording
        RecordingStarted?.Invoke(this, new LapRecordingStartedEventArgs
        {
            SlotNumber = slotNumber
        });
    }

    /// <inheritdoc />
    public void RecordThrottleSample(int slotNumber, byte throttleValue, DateTime timestamp)
    {
        ValidateSlotNumber(slotNumber);
        var state = _recordingStates[slotNumber - 1];

        // Only buffer samples when recording is active (either phase)
        if (state.Phase == RecordingPhase.Idle)
            return;

        // Add to circular buffer with wall-clock timestamp
        var bufferedSample = new BufferedThrottleSample(timestamp, throttleValue);
        state.ThrottleBuffer.Add(bufferedSample);

        // Trim old samples to prevent unbounded growth
        TrimBuffer(state.ThrottleBuffer);

        var rawTimeSec = timestamp.TimeOfDay.TotalSeconds;
        var phaseLabel = state.Phase == RecordingPhase.WaitingForLapStart ? "WAITING" : "RECORDING";
        Log.Debug("THROTTLE slot {SlotNumber} [{Phase}]: throttle={Throttle} raw={RawTime:F3}s buffer={BufferSize}",
            slotNumber, phaseLabel, throttleValue, rawTimeSec, state.ThrottleBuffer.Count);
    }

    /// <inheritdoc />
    public void NotifyLapCompleted(int slotNumber, double lapTimeSeconds, DateTime trueEventTime)
    {
        ValidateSlotNumber(slotNumber);
        var state = _recordingStates[slotNumber - 1];
        var notificationTime = DateTime.UtcNow;
        var rawTimeSec = notificationTime.TimeOfDay.TotalSeconds;
        var trueTimeSec = trueEventTime.TimeOfDay.TotalSeconds;
        var delaySec = (notificationTime - trueEventTime).TotalSeconds;

        switch (state.Phase)
        {
            case RecordingPhase.Idle:
                return;

            case RecordingPhase.WaitingForLapStart:
                // First finish line crossing - this is the START of the lap
                Log.Information("===== LAP START slot {SlotNumber} ===== trueTime={TrueTime:F3}s raw={RawTime:F3}s delay={Delay:F3}s",
                    slotNumber, trueTimeSec, rawTimeSec, delaySec);

                state.Phase = RecordingPhase.Recording;
                state.TrueLapStartTime = trueEventTime;

                // Raise the RecordingStarted event
                RecordingStarted?.Invoke(this, new LapRecordingStartedEventArgs
                {
                    SlotNumber = slotNumber
                });
                return;

            case RecordingPhase.Recording:
                // Second finish line crossing - lap is COMPLETE
                Log.Information("===== LAP END slot {SlotNumber} ===== trueTime={TrueTime:F3}s raw={RawTime:F3}s delay={Delay:F3}s lapTime={LapTime:F3}s",
                    slotNumber, trueTimeSec, rawTimeSec, delaySec, lapTimeSeconds);

                SaveRecording(slotNumber, state, lapTimeSeconds, trueEventTime);
                return;
        }
    }

    private void SaveRecording(int slotNumber, RecordingState state, double lapTimeSeconds, DateTime trueLapEndTime)
    {
        state.Phase = RecordingPhase.Idle;

        if (state.TrueLapStartTime == null)
        {
            Log.Warning("Recording completed for slot {SlotNumber} but TrueLapStartTime not set", slotNumber);
            state.ThrottleBuffer.Clear();
            return;
        }

        var trueLapStartTime = state.TrueLapStartTime.Value;

        // Extract samples from buffer that fall within the lap time window
        var lapSamples = state.ThrottleBuffer
            .Where(s => s.Timestamp >= trueLapStartTime && s.Timestamp <= trueLapEndTime)
            .OrderBy(s => s.Timestamp)
            .ToList();

        Log.Debug("Extracted {SampleCount} samples from buffer of {BufferSize} for lap window {Start:F3}s to {End:F3}s",
            lapSamples.Count, state.ThrottleBuffer.Count,
            trueLapStartTime.TimeOfDay.TotalSeconds, trueLapEndTime.TimeOfDay.TotalSeconds);

        if (lapSamples.Count == 0)
        {
            Log.Warning("Recording completed for slot {SlotNumber} but no samples found in lap window", slotNumber);
            state.ThrottleBuffer.Clear();
            state.TrueLapStartTime = null;
            return;
        }

        // Convert to relative timestamps (centiseconds from lap start)
        // The powerbase gives us the accurate lap time, so we scale timestamps to match
        var actualLapTimeCs = (uint)(lapTimeSeconds * 100.0);
        var recordedDurationMs = (trueLapEndTime - trueLapStartTime).TotalMilliseconds;
        var recordedDurationCs = (uint)(recordedDurationMs / 10.0);

        // Calculate scale factor to align recorded duration with actual lap time
        double scaleFactor = recordedDurationCs > 0 ? (double)actualLapTimeCs / recordedDurationCs : 1.0;

        Log.Debug("Timestamp scaling for slot {SlotNumber}: recorded={RecordedCs}cs, actual={ActualCs}cs, scale={Scale:F3}",
            slotNumber, recordedDurationCs, actualLapTimeCs, scaleFactor);

        var alignedSamples = new List<ThrottleSample>(lapSamples.Count);
        foreach (var buffered in lapSamples)
        {
            // Calculate relative time from lap start
            var relativeMs = (buffered.Timestamp - trueLapStartTime).TotalMilliseconds;
            var relativeCs = (uint)(relativeMs / 10.0);

            // Apply scaling to match actual lap duration
            var scaledCs = (uint)(relativeCs * scaleFactor);

            alignedSamples.Add(new ThrottleSample(scaledCs, buffered.ThrottleValue));
        }

        var recordedLap = new RecordedLap
        {
            SlotNumber = slotNumber,
            RecordedAt = DateTime.UtcNow,
            LapTimeSeconds = lapTimeSeconds,
            Samples = alignedSamples
        };

        _recordedLaps[slotNumber - 1].Add(recordedLap);

        // Calculate and log statistics
        LogRecordingStatistics(slotNumber, recordedLap, lapTimeSeconds);

        // Raise the event
        RecordingCompleted?.Invoke(this, new LapRecordingCompletedEventArgs
        {
            SlotNumber = slotNumber,
            RecordedLap = recordedLap,
            TrueLapEndTime = trueLapEndTime
        });

        // Clear state for next recording
        state.ThrottleBuffer.Clear();
        state.TrueLapStartTime = null;
    }

    private static void LogRecordingStatistics(int slotNumber, RecordedLap recordedLap, double lapTimeSeconds)
    {
        if (recordedLap.SampleCount == 0)
            return;

        var firstSample = recordedLap.Samples[0];
        var lastSample = recordedLap.Samples[^1];
        var recordingDurationCs = lastSample.TimestampCentiseconds - firstSample.TimestampCentiseconds;
        var recordingDurationSec = recordingDurationCs / 100.0;
        var avgSamplesPerSec = recordedLap.SampleCount / (recordingDurationSec > 0 ? recordingDurationSec : 1);

        byte minThrottle = 63, maxThrottle = 0;
        foreach (var sample in recordedLap.Samples)
        {
            if (sample.ThrottleValue < minThrottle) minThrottle = sample.ThrottleValue;
            if (sample.ThrottleValue > maxThrottle) maxThrottle = sample.ThrottleValue;
        }

        Log.Information(
            "Recording completed for slot {SlotNumber}: " +
            "{SampleCount} samples over {RecordingDuration:F2}s ({SamplesPerSec:F1} samples/sec), " +
            "lap time={LapTime:F2}s, throttle range={MinThrottle}-{MaxThrottle}",
            slotNumber, recordedLap.SampleCount, recordingDurationSec, avgSamplesPerSec,
            lapTimeSeconds, minThrottle, maxThrottle);

        // Log first and last samples for verification
        Log.Debug("Slot {SlotNumber} recording - First 10 samples:", slotNumber);
        for (int i = 0; i < Math.Min(10, recordedLap.SampleCount); i++)
        {
            var s = recordedLap.Samples[i];
            Log.Debug("  [{Index}] t={TimeCs}cs ({TimeSec:F2}s) throttle={Throttle}",
                i, s.TimestampCentiseconds, s.TimestampCentiseconds / 100.0, s.ThrottleValue);
        }

        if (recordedLap.SampleCount > 10)
        {
            Log.Debug("Slot {SlotNumber} recording - Last 10 samples:", slotNumber);
            for (int i = Math.Max(0, recordedLap.SampleCount - 10); i < recordedLap.SampleCount; i++)
            {
                var s = recordedLap.Samples[i];
                Log.Debug("  [{Index}] t={TimeCs}cs ({TimeSec:F2}s) throttle={Throttle}",
                    i, s.TimestampCentiseconds, s.TimestampCentiseconds / 100.0, s.ThrottleValue);
            }
        }
    }

    private static void TrimBuffer(List<BufferedThrottleSample> buffer)
    {
        // Remove samples that are too old
        var cutoffTime = DateTime.UtcNow.AddSeconds(-MaxBufferAgeSeconds);
        buffer.RemoveAll(s => s.Timestamp < cutoffTime);

        // Also enforce max size
        while (buffer.Count > MaxBufferSize)
        {
            buffer.RemoveAt(0);
        }
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
    /// Phases of the lap recording process.
    /// </summary>
    private enum RecordingPhase
    {
        /// <summary>Recording is not active.</summary>
        Idle,

        /// <summary>User pressed Record, buffering samples, waiting for lap start.</summary>
        WaitingForLapStart,

        /// <summary>Lap started, continuing to buffer samples until lap end.</summary>
        Recording
    }

    /// <summary>
    /// A throttle sample with wall-clock timestamp for buffering.
    /// </summary>
    private readonly record struct BufferedThrottleSample(DateTime Timestamp, byte ThrottleValue);

    /// <summary>
    /// Internal state for tracking an active recording.
    /// </summary>
    private class RecordingState
    {
        public RecordingPhase Phase { get; set; } = RecordingPhase.Idle;
        public DateTime? TrueLapStartTime { get; set; }
        public List<BufferedThrottleSample> ThrottleBuffer { get; } = [];
    }
}
