using System;
using System.Diagnostics;
using Scalextric;
using ScalextricBleMonitor.Models;
using Serilog;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Service for playing back recorded laps as ghost car throttle data.
/// Uses linear interpolation between recorded samples based on elapsed time.
///
/// Two-phase playback:
/// 1. WaitingForLap - Car runs at fixed approach speed until it crosses the finish line
/// 2. Playing - Replays recorded throttle values synchronized to the lap
///
/// This ensures the recorded lap is synchronized to track position, regardless of
/// where the car was when playback started.
/// </summary>
public class GhostPlaybackService : IGhostPlaybackService
{
    private const int MaxSlots = 6;

    // Per-slot playback state
    private readonly PlaybackState[] _playbackStates = new PlaybackState[MaxSlots];

    public GhostPlaybackService()
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            _playbackStates[i] = new PlaybackState();
        }
    }

    /// <inheritdoc />
    public void StartPlayback(int slotNumber, RecordedLap lap, byte approachSpeed)
    {
        ValidateSlotNumber(slotNumber);

        if (lap.SampleCount == 0)
        {
            Log.Warning("Cannot start playback for slot {SlotNumber}: recorded lap has no samples", slotNumber);
            return;
        }

        var state = _playbackStates[slotNumber - 1];
        state.CurrentLap = lap;
        state.ApproachSpeed = approachSpeed;
        state.Phase = PlaybackPhase.WaitingForLap;
        state.Stopwatch.Stop();
        state.Stopwatch.Reset();

        Log.Information(
            "Started playback for slot {SlotNumber}: lap={LapName} duration={Duration:F2}s samples={SampleCount}, approach speed={ApproachSpeed}",
            slotNumber, lap.DisplayName, lap.LapTimeSeconds, lap.SampleCount, approachSpeed);
    }

    /// <inheritdoc />
    public void StopPlayback(int slotNumber)
    {
        ValidateSlotNumber(slotNumber);

        var state = _playbackStates[slotNumber - 1];
        if (state.CurrentLap != null || state.Phase != PlaybackPhase.Idle)
        {
            Log.Information("Stopped playback for slot {SlotNumber}", slotNumber);
            state.CurrentLap = null;
            state.Phase = PlaybackPhase.Idle;
            state.Stopwatch.Stop();
        }
    }

    /// <inheritdoc />
    public bool IsPlaying(int slotNumber)
    {
        ValidateSlotNumber(slotNumber);
        var state = _playbackStates[slotNumber - 1];
        return state.Phase != PlaybackPhase.Idle && state.CurrentLap != null;
    }

    /// <inheritdoc />
    public bool IsWaitingForLap(int slotNumber)
    {
        ValidateSlotNumber(slotNumber);
        return _playbackStates[slotNumber - 1].Phase == PlaybackPhase.WaitingForLap;
    }

    /// <inheritdoc />
    public byte GetCurrentThrottleValue(int slotNumber)
    {
        ValidateSlotNumber(slotNumber);

        var state = _playbackStates[slotNumber - 1];
        var lap = state.CurrentLap;

        if (lap == null || state.Phase == PlaybackPhase.Idle)
            return 0;

        // If waiting for first lap crossing, return the fixed approach speed
        if (state.Phase == PlaybackPhase.WaitingForLap)
        {
            return state.ApproachSpeed;
        }

        // Playing phase - interpolate from recorded samples
        if (lap.SampleCount == 0)
            return 0;

        var elapsedCs = GetElapsedCentiseconds(slotNumber);
        var lapDurationCs = lap.DurationCentiseconds;

        // If elapsed time exceeds lap duration, loop back (modulo)
        // This handles the case where the car takes longer than the recorded lap
        if (lapDurationCs > 0 && elapsedCs >= lapDurationCs)
        {
            elapsedCs = elapsedCs % lapDurationCs;
        }

        return InterpolateThrottle(lap, elapsedCs);
    }

    /// <inheritdoc />
    public RecordedLap? GetCurrentLap(int slotNumber)
    {
        ValidateSlotNumber(slotNumber);
        return _playbackStates[slotNumber - 1].CurrentLap;
    }

    /// <inheritdoc />
    public void NotifyLapCompleted(int slotNumber)
    {
        ValidateSlotNumber(slotNumber);

        var state = _playbackStates[slotNumber - 1];
        if (state.CurrentLap == null)
            return;

        if (state.Phase == PlaybackPhase.WaitingForLap)
        {
            // First lap crossing - transition to playing phase
            state.Phase = PlaybackPhase.Playing;
            state.Stopwatch.Restart();
            Log.Information("Slot {SlotNumber} crossed finish line, starting recorded lap playback", slotNumber);
        }
        else if (state.Phase == PlaybackPhase.Playing)
        {
            // Subsequent lap crossing - restart the stopwatch for the new lap
            state.Stopwatch.Restart();
            Log.Debug("Lap completed for slot {SlotNumber}, restarting playback from beginning", slotNumber);
        }
    }

    /// <inheritdoc />
    public uint GetElapsedCentiseconds(int slotNumber)
    {
        ValidateSlotNumber(slotNumber);

        var state = _playbackStates[slotNumber - 1];
        if (state.CurrentLap == null || state.Phase != PlaybackPhase.Playing)
            return 0;

        // Convert elapsed milliseconds to centiseconds
        var elapsedMs = state.Stopwatch.ElapsedMilliseconds;
        return (uint)(elapsedMs / 10);
    }

    /// <summary>
    /// Interpolates the throttle value at the given elapsed time using linear interpolation.
    /// </summary>
    private static byte InterpolateThrottle(RecordedLap lap, uint elapsedCs)
    {
        var samples = lap.Samples;

        // Edge cases
        if (samples.Count == 0)
            return 0;

        if (samples.Count == 1)
            return samples[0].ThrottleValue;

        // Before first sample - use first sample's value
        if (elapsedCs <= samples[0].TimestampCentiseconds)
            return samples[0].ThrottleValue;

        // After last sample - use last sample's value
        if (elapsedCs >= samples[^1].TimestampCentiseconds)
            return samples[^1].ThrottleValue;

        // Binary search to find the bracketing samples
        int left = 0;
        int right = samples.Count - 1;

        while (right - left > 1)
        {
            int mid = (left + right) / 2;
            if (samples[mid].TimestampCentiseconds <= elapsedCs)
                left = mid;
            else
                right = mid;
        }

        // Now samples[left] <= elapsedCs < samples[right]
        var s1 = samples[left];
        var s2 = samples[right];

        // Linear interpolation
        uint t1 = s1.TimestampCentiseconds;
        uint t2 = s2.TimestampCentiseconds;
        byte v1 = s1.ThrottleValue;
        byte v2 = s2.ThrottleValue;

        if (t2 == t1)
            return v1; // Avoid division by zero

        // Calculate interpolation factor (0.0 to 1.0)
        double factor = (double)(elapsedCs - t1) / (t2 - t1);

        // Interpolate throttle value
        double interpolated = v1 + factor * (v2 - v1);

        // Clamp to valid range and return
        return (byte)Math.Clamp((int)Math.Round(interpolated), ScalextricProtocol.MinPowerLevel, ScalextricProtocol.MaxPowerLevel);
    }

    private static void ValidateSlotNumber(int slotNumber)
    {
        if (slotNumber < 1 || slotNumber > MaxSlots)
        {
            throw new ArgumentOutOfRangeException(nameof(slotNumber), $"Slot number must be 1-{MaxSlots}");
        }
    }

    /// <summary>
    /// Phases of ghost car playback.
    /// </summary>
    private enum PlaybackPhase
    {
        /// <summary>Playback not active.</summary>
        Idle,

        /// <summary>Running at fixed approach speed, waiting for finish line crossing.</summary>
        WaitingForLap,

        /// <summary>Playing back recorded throttle values.</summary>
        Playing
    }

    /// <summary>
    /// Internal state for tracking active playback on a slot.
    /// </summary>
    private class PlaybackState
    {
        public RecordedLap? CurrentLap { get; set; }
        public PlaybackPhase Phase { get; set; } = PlaybackPhase.Idle;
        public byte ApproachSpeed { get; set; }
        public Stopwatch Stopwatch { get; } = new();
    }
}
