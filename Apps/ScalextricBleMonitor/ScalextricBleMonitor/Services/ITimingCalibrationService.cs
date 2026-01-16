using System;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Service responsible for calibrating timing between wall-clock time and powerbase timestamps.
/// After power-on, the service captures the first slot notification to estimate when the
/// powerbase clock was at t=0, enabling accurate lap timing calculations.
/// </summary>
public interface ITimingCalibrationService
{
    /// <summary>
    /// Gets whether the service is currently awaiting the first slot notification for calibration.
    /// </summary>
    bool IsAwaitingCalibration { get; }

    /// <summary>
    /// Gets the estimated wall-clock time when the powerbase clock was at t=0.
    /// Returns null if calibration has not been completed.
    /// </summary>
    DateTime? EstimatedPowerbaseT0 { get; }

    /// <summary>
    /// Gets whether calibration has been completed and timing data is available.
    /// </summary>
    bool IsCalibrated { get; }

    /// <summary>
    /// Resets the calibration state to await the next slot notification.
    /// Call this when power is enabled to start fresh calibration.
    /// </summary>
    void Reset();

    /// <summary>
    /// Processes a slot notification to calibrate timing if awaiting calibration.
    /// </summary>
    /// <param name="lane1Timestamp">Lane 1 timestamp from the powerbase (centiseconds).</param>
    /// <param name="lane2Timestamp">Lane 2 timestamp from the powerbase (centiseconds).</param>
    /// <param name="notificationArrivalTime">Wall-clock time when the notification arrived.</param>
    /// <returns>True if this notification completed calibration, false otherwise.</returns>
    bool ProcessSlotNotification(uint lane1Timestamp, uint lane2Timestamp, DateTime notificationArrivalTime);

    /// <summary>
    /// Calculates the estimated delay between when an event occurred and when we received notification.
    /// </summary>
    /// <param name="powerbaseTimestampCentiseconds">The powerbase timestamp in centiseconds.</param>
    /// <param name="notificationArrivalTime">Wall-clock time when the notification arrived.</param>
    /// <returns>Estimated delay in seconds, or null if not calibrated or timestamp is zero.</returns>
    double? CalculateNotificationDelay(uint powerbaseTimestampCentiseconds, DateTime notificationArrivalTime);

    /// <summary>
    /// Calculates the true wall-clock time when a powerbase event occurred.
    /// </summary>
    /// <param name="powerbaseTimestampCentiseconds">The powerbase timestamp in centiseconds.</param>
    /// <returns>Estimated wall-clock time of the event, or null if not calibrated.</returns>
    DateTime? CalculateTrueEventTime(uint powerbaseTimestampCentiseconds);
}
