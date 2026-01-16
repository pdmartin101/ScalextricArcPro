using System;
using ScalextricBleMonitor.Models;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Service for playing back recorded laps as ghost car throttle data.
/// Provides interpolated throttle values based on elapsed time since playback started.
///
/// Playback has two phases:
/// 1. WaitingForLap - Uses fixed speed until the ghost car crosses the finish line
/// 2. Playing - Replays the recorded throttle values synchronized to lap timing
///
/// This two-phase approach handles the fact that when playback starts, we don't know
/// where the car is on the track. Running at fixed speed until the finish line ensures
/// the recorded lap playback is synchronized to the actual track position.
///
/// Usage:
/// 1. Call StartPlayback() with a recorded lap and approach speed
/// 2. Call GetCurrentThrottleValue() in the power heartbeat loop
///    - Returns fixedApproachSpeed while waiting for first lap
///    - Returns interpolated recorded values once lap has started
/// 3. Call NotifyLapCompleted() when the ghost car crosses the finish line
/// 4. Call StopPlayback() when ghost mode is disabled or slot is deactivated
/// </summary>
public interface IGhostPlaybackService
{
    /// <summary>
    /// Starts playback of a recorded lap for the specified slot.
    /// Begins in "waiting for lap" phase using the approach speed until first finish line crossing.
    /// </summary>
    /// <param name="slotNumber">The slot number (1-6).</param>
    /// <param name="lap">The recorded lap to play back.</param>
    /// <param name="approachSpeed">Fixed throttle value (0-63) to use while approaching the finish line.</param>
    void StartPlayback(int slotNumber, RecordedLap lap, byte approachSpeed);

    /// <summary>
    /// Stops playback for the specified slot.
    /// </summary>
    /// <param name="slotNumber">The slot number (1-6).</param>
    void StopPlayback(int slotNumber);

    /// <summary>
    /// Checks if playback is active for a specific slot (either waiting or playing).
    /// </summary>
    /// <param name="slotNumber">The slot number (1-6).</param>
    /// <returns>True if playback is active for the slot.</returns>
    bool IsPlaying(int slotNumber);

    /// <summary>
    /// Checks if the slot is waiting for the first lap to start.
    /// In this phase, GetCurrentThrottleValue returns the fixed approach speed.
    /// </summary>
    /// <param name="slotNumber">The slot number (1-6).</param>
    /// <returns>True if waiting for first finish line crossing.</returns>
    bool IsWaitingForLap(int slotNumber);

    /// <summary>
    /// Gets the current throttle value for the specified slot based on elapsed playback time.
    /// Uses linear interpolation between recorded samples.
    /// </summary>
    /// <param name="slotNumber">The slot number (1-6).</param>
    /// <returns>Interpolated throttle value (0-63), or 0 if not playing.</returns>
    byte GetCurrentThrottleValue(int slotNumber);

    /// <summary>
    /// Gets the recorded lap currently being played back for the specified slot.
    /// </summary>
    /// <param name="slotNumber">The slot number (1-6).</param>
    /// <returns>The recorded lap being played, or null if not playing.</returns>
    RecordedLap? GetCurrentLap(int slotNumber);

    /// <summary>
    /// Notifies the service that the ghost car crossed the finish line.
    /// This restarts the lap playback from the beginning.
    /// </summary>
    /// <param name="slotNumber">The slot number (1-6).</param>
    void NotifyLapCompleted(int slotNumber);

    /// <summary>
    /// Gets the elapsed time in centiseconds since playback started for the current lap.
    /// </summary>
    /// <param name="slotNumber">The slot number (1-6).</param>
    /// <returns>Elapsed time in centiseconds, or 0 if not playing.</returns>
    uint GetElapsedCentiseconds(int slotNumber);
}
