using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scalextric;
using ScalextricBleMonitor.Models;
using ScalextricBleMonitor.Services;

namespace ScalextricBleMonitor.ViewModels;

/// <summary>
/// View model for individual controller/slot status.
/// Wraps a Controller model and LapRecord model with UI-specific properties.
/// </summary>
public partial class ControllerViewModel : ObservableObject
{
    private bool _previousBrakeState;
    private bool _previousLaneChangeState;

    // Underlying domain models
    private readonly Controller _controller = new();
    private readonly LapRecord _lapRecord = new();

    // Lap timing is delegated to a separate engine for testability
    private readonly LapTimingEngine _lapTimingEngine = new();

    /// <summary>
    /// Gets the underlying Controller model.
    /// </summary>
    public Controller Model => _controller;

    /// <summary>
    /// Gets the underlying LapRecord model.
    /// </summary>
    public LapRecord LapRecordModel => _lapRecord;

    [ObservableProperty]
    private int _slotNumber;

    partial void OnSlotNumberChanged(int value)
    {
        _controller.SlotNumber = value;
    }

    /// <summary>
    /// Power level for this controller (0-63). Used as a multiplier for track power.
    /// In ghost mode, this becomes the direct throttle index (0-63).
    /// </summary>
    [ObservableProperty]
    private int _powerLevel = ScalextricProtocol.MaxPowerLevel;

    partial void OnPowerLevelChanged(int value)
    {
        _controller.PowerLevel = value;
    }

    /// <summary>
    /// When true, this slot operates in ghost mode - GhostThrottleLevel becomes the direct throttle
    /// index rather than using PowerLevel as a multiplier, allowing autonomous car control without a physical controller.
    /// </summary>
    [ObservableProperty]
    private bool _isGhostMode;

    partial void OnIsGhostModeChanged(bool value)
    {
        _controller.IsGhostMode = value;
    }

    /// <summary>
    /// Ghost mode throttle level (0-63). When ghost mode is enabled, this value is sent
    /// directly to the car as a fixed throttle index. Defaults to 0 (stopped).
    /// Separate from PowerLevel which is used for controller max power.
    /// </summary>
    [ObservableProperty]
    private int _ghostThrottleLevel;

    partial void OnGhostThrottleLevelChanged(int value)
    {
        _controller.GhostThrottleLevel = value;
    }

    /// <summary>
    /// The source of throttle values when in ghost mode.
    /// FixedSpeed uses GhostThrottleLevel; RecordedLap uses a previously recorded lap.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFixedSpeedSource))]
    [NotifyPropertyChangedFor(nameof(IsRecordedLapSource))]
    private GhostSourceType _ghostSource = GhostSourceType.FixedSpeed;

    partial void OnGhostSourceChanged(GhostSourceType value)
    {
        _controller.GhostSource = value;
        GhostSourceChanged?.Invoke(this, value);
    }

    /// <summary>
    /// Event raised when the ghost source is changed.
    /// </summary>
    public event EventHandler<GhostSourceType>? GhostSourceChanged;

    /// <summary>
    /// Returns true when ghost source is FixedSpeed. Used for UI visibility bindings.
    /// </summary>
    public bool IsFixedSpeedSource => GhostSource == GhostSourceType.FixedSpeed;

    /// <summary>
    /// Returns true when ghost source is RecordedLap. Used for UI visibility bindings.
    /// </summary>
    public bool IsRecordedLapSource => GhostSource == GhostSourceType.RecordedLap;

    /// <summary>
    /// Available ghost source types for selection in UI.
    /// </summary>
    public static IReadOnlyList<GhostSourceType> AvailableGhostSources { get; } =
        Enum.GetValues<GhostSourceType>();

    /// <summary>
    /// Number of laps to record in a multi-lap recording session (1-5).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordingStatusText))]
    private int _lapsToRecord = 1;

    /// <summary>
    /// Maximum number of laps that can be recorded in a session.
    /// </summary>
    public const int MaxLapsToRecord = 5;

    /// <summary>
    /// Available lap count options for the UI (1-5).
    /// </summary>
    public static IReadOnlyList<int> AvailableLapCounts { get; } = [1, 2, 3, 4, 5];

    /// <summary>
    /// Number of laps recorded so far in the current recording session.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordingStatusText))]
    private int _recordedLapCount;

    /// <summary>
    /// Whether lap recording is currently active for this slot (waiting or recording).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordButtonText))]
    [NotifyPropertyChangedFor(nameof(RecordingStatusText))]
    [NotifyCanExecuteChangedFor(nameof(ToggleRecordingCommand))]
    private bool _isRecording;

    /// <summary>
    /// Whether actively recording samples (after lap start, before lap end).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordingStatusText))]
    private bool _isActivelyRecording;

    /// <summary>
    /// Event raised when recording state changes.
    /// </summary>
    public event EventHandler<bool>? RecordingStateChanged;

    partial void OnIsRecordingChanged(bool value)
    {
        RecordingStateChanged?.Invoke(this, value);
        // Reset state when recording stops
        if (!value)
        {
            IsActivelyRecording = false;
            RecordedLapCount = 0;
        }
    }

    /// <summary>
    /// Text for the record button based on recording state.
    /// </summary>
    public string RecordButtonText => IsRecording ? "● Stop" : "● Record";

    /// <summary>
    /// Status text shown during recording phases.
    /// </summary>
    public string RecordingStatusText
    {
        get
        {
            if (!IsRecording)
                return string.Empty;

            string lapProgress = LapsToRecord > 1 ? $" ({RecordedLapCount + 1}/{LapsToRecord})" : "";

            return IsActivelyRecording
                ? $"Recording lap{lapProgress}... Complete the lap to save"
                : $"Waiting for lap start{lapProgress}... Cross the finish line";
        }
    }

    /// <summary>
    /// Collection of recorded laps available for this slot.
    /// </summary>
    public ObservableCollection<RecordedLap> AvailableRecordedLaps { get; } = [];

    /// <summary>
    /// The currently selected recorded lap for playback.
    /// </summary>
    [ObservableProperty]
    private RecordedLap? _selectedRecordedLap;

    /// <summary>
    /// Event raised when the selected recorded lap changes.
    /// </summary>
    public event EventHandler<RecordedLap?>? SelectedRecordedLapChanged;

    partial void OnSelectedRecordedLapChanged(RecordedLap? value)
    {
        SelectedRecordedLapChanged?.Invoke(this, value);
    }

    /// <summary>
    /// Toggles recording state for this slot.
    /// </summary>
    [RelayCommand]
    private void ToggleRecording()
    {
        // The actual start/stop logic is handled by MainViewModel via the RecordingStateChanged event
        // This command just toggles the visual state; MainViewModel coordinates with the recording service
        IsRecording = !IsRecording;
    }

    /// <summary>
    /// Throttle profile type for this slot. Determines the throttle response curve.
    /// Changes take effect on next power enable.
    /// </summary>
    [ObservableProperty]
    private ThrottleProfileType _throttleProfile = ThrottleProfileType.Linear;

    partial void OnThrottleProfileChanged(ThrottleProfileType value)
    {
        ThrottleProfileChanged?.Invoke(this, value);
    }

    /// <summary>
    /// Event raised when the throttle profile is changed.
    /// </summary>
    public event EventHandler<ThrottleProfileType>? ThrottleProfileChanged;

    /// <summary>
    /// Available throttle profile types for selection in UI.
    /// </summary>
    public static IReadOnlyList<ThrottleProfileType> AvailableProfiles { get; } =
        Enum.GetValues<ThrottleProfileType>();

    [ObservableProperty]
    private int _throttle;

    partial void OnThrottleChanged(int value)
    {
        _controller.Throttle = value;
    }

    [ObservableProperty]
    private bool _isBrakePressed;

    partial void OnIsBrakePressedChanged(bool value)
    {
        _controller.IsBrakePressed = value;
    }

    [ObservableProperty]
    private bool _isLaneChangePressed;

    partial void OnIsLaneChangePressedChanged(bool value)
    {
        _controller.IsLaneChangePressed = value;
    }

    [ObservableProperty]
    private int _brakeCount;

    partial void OnBrakeCountChanged(int value)
    {
        _controller.BrakeCount = value;
    }

    [ObservableProperty]
    private int _laneChangeCount;

    partial void OnLaneChangeCountChanged(int value)
    {
        _controller.LaneChangeCount = value;
    }

    [ObservableProperty]
    private int _currentLap;

    partial void OnCurrentLapChanged(int value)
    {
        _lapRecord.CurrentLap = value;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LapTimeDisplay))]
    private double _lastLapTimeSeconds;

    partial void OnLastLapTimeSecondsChanged(double value)
    {
        _lapRecord.LastLapTimeSeconds = value;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BestLapTimeDisplay))]
    private double _bestLapTimeSeconds;

    partial void OnBestLapTimeSecondsChanged(double value)
    {
        _lapRecord.BestLapTimeSeconds = value;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LaneDisplay))]
    private int _currentLane;

    partial void OnCurrentLaneChanged(int value)
    {
        _lapRecord.CurrentLane = value;
    }

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

    /// <summary>
    /// Increments the ghost throttle level by 1 (max 63).
    /// Used in ghost mode to increase car speed.
    /// </summary>
    [RelayCommand]
    private void IncrementGhostThrottle()
    {
        if (GhostThrottleLevel < ScalextricProtocol.MaxPowerLevel) GhostThrottleLevel++;
    }

    /// <summary>
    /// Decrements the ghost throttle level by 1 (min 0).
    /// Used in ghost mode to decrease car speed.
    /// </summary>
    [RelayCommand]
    private void DecrementGhostThrottle()
    {
        if (GhostThrottleLevel > ScalextricProtocol.MinPowerLevel) GhostThrottleLevel--;
    }

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

        // Reset the models
        _controller.ResetInputState();
        _lapRecord.Reset();

        // Reset the lap timing engine
        _lapTimingEngine.Reset();
    }
}
