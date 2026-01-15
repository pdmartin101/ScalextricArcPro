using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ScalextricBleMonitor.ViewModels;

/// <summary>
/// View model for individual controller/slot status.
/// </summary>
public partial class ControllerViewModel : ObservableObject
{
    private bool _previousBrakeState;
    private bool _previousLaneChangeState;
    // Track the highest timestamp seen - whichever lane was crossed most recently has the higher value
    private uint _lastMaxTimestamp;
    // Track whether we've established a valid baseline timestamp
    private bool _hasBaselineTimestamp;

    [ObservableProperty]
    private int _slotNumber;

    /// <summary>
    /// Power level for this controller (0-63). Used as a multiplier for track power.
    /// In ghost mode, this becomes the direct throttle index (0-63).
    /// </summary>
    [ObservableProperty]
    private int _powerLevel = 63;

    /// <summary>
    /// When true, this slot operates in ghost mode - PowerLevel becomes a direct throttle
    /// index rather than a multiplier, allowing autonomous car control without a physical controller.
    /// </summary>
    [ObservableProperty]
    private bool _isGhostMode;

    [ObservableProperty]
    private int _throttle;

    [ObservableProperty]
    private bool _isBrakePressed;

    [ObservableProperty]
    private bool _isLaneChangePressed;

    [ObservableProperty]
    private int _brakeCount;

    [ObservableProperty]
    private int _laneChangeCount;

    [ObservableProperty]
    private int _currentLap;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LapTimeDisplay))]
    private double _lastLapTimeSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BestLapTimeDisplay))]
    private double _bestLapTimeSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LaneDisplay))]
    private int _currentLane;

    /// <summary>
    /// Formatted display of the last lap time.
    /// </summary>
    public string LapTimeDisplay => LastLapTimeSeconds > 0
        ? $"{LastLapTimeSeconds:F2}s"
        : "--";

    /// <summary>
    /// Formatted display of the current lane.
    /// </summary>
    public string LaneDisplay => CurrentLane > 0
        ? $"L{CurrentLane}"
        : "--";

    /// <summary>
    /// Formatted display of the best lap time.
    /// </summary>
    public string BestLapTimeDisplay => BestLapTimeSeconds > 0
        ? $"{BestLapTimeSeconds:F2}s"
        : "--";

    public string SlotLabel => $"Controller {SlotNumber}";

    public void UpdateFromByte(byte data)
    {
        // Decode: Bits 0-5 = throttle (0-63), Bit 6 = brake, Bit 7 = lane change
        Throttle = data & 0x3F;
        bool currentBrake = (data & 0x40) != 0;
        bool currentLaneChange = (data & 0x80) != 0;

        // Detect rising edge for brake
        if (currentBrake && !_previousBrakeState)
        {
            BrakeCount++;
        }

        // Detect rising edge for lane change
        if (currentLaneChange && !_previousLaneChangeState)
        {
            LaneChangeCount++;
        }

        IsBrakePressed = currentBrake;
        IsLaneChangePressed = currentLaneChange;

        _previousBrakeState = currentBrake;
        _previousLaneChangeState = currentLaneChange;
    }

    // Timestamp conversion factor: timestamps are in centiseconds (1/100th second = 10ms)
    // Verified: 622004 - 620021 = 1983 units for 10s, 631989 - 622004 = 9985 units for 100s
    private const double TimestampUnitsPerSecond = 100.0;

    /// <summary>
    /// Updates the lap count and lap time based on finish line timestamps.
    /// The powerbase has two finish line sensors (one per lane). Whichever lane
    /// was crossed most recently will have the higher timestamp value.
    /// We simply take the max of both timestamps - if it changed, a lap was completed.
    /// </summary>
    /// <param name="lane1Timestamp">The finish line timestamp for lane 1 (bytes 2-5).</param>
    /// <param name="lane2Timestamp">The finish line timestamp for lane 2 (bytes 6-9).</param>
    /// <returns>True if a new lap was counted, false otherwise.</returns>
    public bool UpdateFinishLineTimestamps(uint lane1Timestamp, uint lane2Timestamp)
    {
        // Take the higher of the two timestamps - that's the lane that was most recently crossed
        uint currentMaxTimestamp = Math.Max(lane1Timestamp, lane2Timestamp);

        // Ignore zero timestamps
        if (currentMaxTimestamp == 0)
            return false;

        // First time seeing any timestamp: just store it as baseline, don't count a lap.
        // The powerbase retains timestamps from previous sessions, so the first value
        // we see is stale data - we need to wait for an actual crossing to detect a change.
        if (!_hasBaselineTimestamp)
        {
            _lastMaxTimestamp = currentMaxTimestamp;
            _hasBaselineTimestamp = true;
            return false;
        }

        // If the max timestamp changed, the car actually crossed a finish line
        if (currentMaxTimestamp != _lastMaxTimestamp)
        {
            // Determine which lane was crossed (whichever has the higher timestamp)
            int crossedLane = lane1Timestamp >= lane2Timestamp ? 1 : 2;

            // Increment lap count first
            // CurrentLap 0 -> 1: Starting lap 1 (first crossing)
            // CurrentLap 1 -> 2: Finished lap 1, starting lap 2
            // CurrentLap 2 -> 3: Finished lap 2, starting lap 3, etc.
            CurrentLap++;

            // Update current lane
            CurrentLane = crossedLane;

            // Only calculate lap time if we just finished a lap (CurrentLap >= 2)
            // CurrentLap == 1 means we just started lap 1, no completed lap yet
            if (CurrentLap >= 2)
            {
                // Handle timestamp overflow (uint wraps at ~497 days of continuous operation)
                uint timeDiff = currentMaxTimestamp >= _lastMaxTimestamp
                    ? currentMaxTimestamp - _lastMaxTimestamp
                    : (uint.MaxValue - _lastMaxTimestamp) + currentMaxTimestamp + 1;
                double lapTimeSeconds = timeDiff / TimestampUnitsPerSecond;

                // Record lap time
                LastLapTimeSeconds = lapTimeSeconds;

                // Update best lap time if this is a new best
                if (BestLapTimeSeconds == 0 || LastLapTimeSeconds < BestLapTimeSeconds)
                {
                    BestLapTimeSeconds = LastLapTimeSeconds;
                }
            }

            // Update baseline for next lap
            _lastMaxTimestamp = currentMaxTimestamp;
            return true;
        }

        return false;
    }

    public void Reset()
    {
        Throttle = 0;
        IsBrakePressed = false;
        IsLaneChangePressed = false;
        BrakeCount = 0;
        LaneChangeCount = 0;
        CurrentLap = 0;
        LastLapTimeSeconds = 0;
        BestLapTimeSeconds = 0;
        _previousBrakeState = false;
        _previousLaneChangeState = false;
        _lastMaxTimestamp = 0;
        _hasBaselineTimestamp = false;
        CurrentLane = 0;
    }
}
