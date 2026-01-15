using CommunityToolkit.Mvvm.ComponentModel;
using ScalextricBleMonitor.Services;

namespace ScalextricBleMonitor.ViewModels;

/// <summary>
/// View model for individual controller/slot status.
/// </summary>
public partial class ControllerViewModel : ObservableObject
{
    private bool _previousBrakeState;
    private bool _previousLaneChangeState;

    // Lap timing is delegated to a separate engine for testability
    private readonly LapTimingEngine _lapTimingEngine = new();

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

    /// <summary>
    /// Updates the lap count and lap time based on finish line timestamps.
    /// Delegates to LapTimingEngine for the actual timing logic.
    /// </summary>
    /// <param name="lane1Timestamp">The finish line timestamp for lane 1 (bytes 2-5).</param>
    /// <param name="lane2Timestamp">The finish line timestamp for lane 2 (bytes 6-9).</param>
    /// <returns>True if a new lap was counted, false otherwise.</returns>
    public bool UpdateFinishLineTimestamps(uint lane1Timestamp, uint lane2Timestamp)
    {
        var result = _lapTimingEngine.UpdateTimestamps(lane1Timestamp, lane2Timestamp);

        // Sync ViewModel properties from the engine state
        if (result.LapCompleted)
        {
            CurrentLap = result.CurrentLap;
            CurrentLane = result.CrossedLane;
            LastLapTimeSeconds = result.LapTimeSeconds;
            BestLapTimeSeconds = result.BestLapTimeSeconds;
        }

        return result.LapCompleted;
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
        CurrentLane = 0;

        // Reset the lap timing engine
        _lapTimingEngine.Reset();
    }
}
