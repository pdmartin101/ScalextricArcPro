using System;
using Serilog;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Service responsible for calibrating timing between wall-clock time and powerbase timestamps.
/// After power-on, the service captures the first slot notification to estimate when the
/// powerbase clock was at t=0, enabling accurate lap timing calculations.
/// </summary>
public class TimingCalibrationService : ITimingCalibrationService
{
    private bool _awaitingFirstSlotNotification;
    private DateTime? _estimatedPowerbaseT0;

    /// <inheritdoc/>
    public bool IsAwaitingCalibration => _awaitingFirstSlotNotification;

    /// <inheritdoc/>
    public DateTime? EstimatedPowerbaseT0 => _estimatedPowerbaseT0;

    /// <inheritdoc/>
    public bool IsCalibrated => _estimatedPowerbaseT0.HasValue;

    /// <inheritdoc/>
    public void Reset()
    {
        _awaitingFirstSlotNotification = true;
        _estimatedPowerbaseT0 = null;
        Log.Information("TIMING: Calibration reset, awaiting first slot notification");
    }

    /// <inheritdoc/>
    public bool ProcessSlotNotification(uint lane1Timestamp, uint lane2Timestamp, DateTime notificationArrivalTime)
    {
        if (!_awaitingFirstSlotNotification)
            return false;

        // Convert powerbase timestamps (centiseconds) to seconds
        double t1Sec = lane1Timestamp / 100.0;
        double t2Sec = lane2Timestamp / 100.0;

        // Use max of t1 and t2 as the most recent lane crossing timestamp
        double maxTimestampSec = Math.Max(t1Sec, t2Sec);

        // First notification after power-on
        // Estimate when powerbase clock was at t=0:
        // wallClockAtT0 = notificationArrivalTime - maxTimestamp (assuming minimal delay)
        // If t1 and t2 are both 0, then t=0 is approximately now
        _estimatedPowerbaseT0 = notificationArrivalTime.AddSeconds(-maxTimestampSec);
        _awaitingFirstSlotNotification = false;

        var rawTimeSec = notificationArrivalTime.TimeOfDay.TotalSeconds;
        Log.Information("TIMING: First slot notification received. t1={T1:F2}s t2={T2:F2}s (max={MaxTs:F2}s) arrived at raw={RawTime:F3}s",
            t1Sec, t2Sec, maxTimestampSec, rawTimeSec);
        Log.Information("TIMING: Estimated powerbase t=0 at wall-clock {T0:HH:mm:ss.fff}",
            _estimatedPowerbaseT0.Value);

        return true;
    }

    /// <inheritdoc/>
    public double? CalculateNotificationDelay(uint powerbaseTimestampCentiseconds, DateTime notificationArrivalTime)
    {
        if (!_estimatedPowerbaseT0.HasValue || powerbaseTimestampCentiseconds == 0)
            return null;

        double timestampSec = powerbaseTimestampCentiseconds / 100.0;

        // Expected wall-clock time for this event = t0 + powerbase timestamp
        var expectedArrivalTime = _estimatedPowerbaseT0.Value.AddSeconds(timestampSec);

        // Actual delay = when we received it - when the event actually happened
        return (notificationArrivalTime - expectedArrivalTime).TotalSeconds;
    }

    /// <inheritdoc/>
    public DateTime? CalculateTrueEventTime(uint powerbaseTimestampCentiseconds)
    {
        if (!_estimatedPowerbaseT0.HasValue)
            return null;

        if (powerbaseTimestampCentiseconds == 0)
            return null;

        double timestampSec = powerbaseTimestampCentiseconds / 100.0;
        return _estimatedPowerbaseT0.Value.AddSeconds(timestampSec);
    }
}
