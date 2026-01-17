using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Scalextric;
using ScalextricBleMonitor.Models;
using ScalextricBleMonitor.Services;
using Serilog;

namespace ScalextricBleMonitor.ViewModels;

/// <summary>
/// Manages ghost car recording and playback functionality.
/// Handles controller initialization, ghost mode coordination, and recording/playback state.
/// </summary>
public partial class GhostControlViewModel : ObservableObject
{
    private readonly IGhostRecordingService _ghostRecordingService;
    private readonly IGhostPlaybackService _ghostPlaybackService;
    private readonly AppSettings _settings;

    private const int MaxControllers = 6;

    /// <summary>
    /// Controller status for each slot (1-6).
    /// </summary>
    public ObservableCollection<ControllerViewModel> Controllers { get; } = [];

    /// <summary>
    /// Returns true if any controller is currently in ghost mode.
    /// </summary>
    public bool HasAnyGhostMode => Controllers.Any(c => c.IsGhostMode);

    /// <summary>
    /// Event raised when a controller's throttle profile changes (for settings persistence).
    /// </summary>
    public event EventHandler<(int SlotIndex, ThrottleProfileType Profile)>? ControllerThrottleProfileChanged;

    /// <summary>
    /// Event raised when a controller's ghost source changes (for settings persistence).
    /// </summary>
    public event EventHandler<(int SlotIndex, GhostSourceType Source)>? ControllerGhostSourceChanged;

    /// <summary>
    /// Initializes a new instance of the GhostControlViewModel.
    /// </summary>
    public GhostControlViewModel(
        IGhostRecordingService ghostRecordingService,
        IGhostPlaybackService ghostPlaybackService,
        AppSettings settings)
    {
        _ghostRecordingService = ghostRecordingService;
        _ghostPlaybackService = ghostPlaybackService;
        _settings = settings;

        // Subscribe to recording events
        _ghostRecordingService.RecordingStarted += OnRecordingStarted;
        _ghostRecordingService.RecordingCompleted += OnRecordingCompleted;

        // Load recorded laps from storage before initializing controllers
        _ghostRecordingService.LoadFromStorage();

        InitializeControllers();
    }

    private void InitializeControllers()
    {
        Controllers.Clear();
        for (int i = 0; i < MaxControllers; i++)
        {
            var powerLevel = _settings.SlotPowerLevels.Length > i ? _settings.SlotPowerLevels[i] : 63;
            var isGhostMode = _settings.SlotGhostModes.Length > i && _settings.SlotGhostModes[i];

            // Load throttle profile from settings
            var profileType = ThrottleProfileType.Linear;
            if (_settings.SlotThrottleProfiles.Length > i &&
                Enum.TryParse<ThrottleProfileType>(_settings.SlotThrottleProfiles[i], out var parsedProfile))
            {
                profileType = parsedProfile;
            }

            // Load ghost throttle level from settings
            var ghostThrottleLevel = _settings.SlotGhostThrottleLevels.Length > i ? _settings.SlotGhostThrottleLevels[i] : 0;

            // Load ghost source from settings
            var ghostSource = GhostSourceType.FixedSpeed;
            if (_settings.SlotGhostSources.Length > i &&
                Enum.TryParse<GhostSourceType>(_settings.SlotGhostSources[i], out var parsedSource))
            {
                ghostSource = parsedSource;
            }

            var controller = new ControllerViewModel
            {
                SlotNumber = i + 1,
                PowerLevel = powerLevel,
                IsGhostMode = isGhostMode,
                GhostThrottleLevel = ghostThrottleLevel,
                GhostSource = ghostSource,
                ThrottleProfile = profileType
            };

            // Subscribe to profile changes to persist settings
            controller.ThrottleProfileChanged += OnControllerThrottleProfileChanged;

            // Subscribe to ghost source changes to persist settings
            controller.GhostSourceChanged += OnControllerGhostSourceChanged;

            // Subscribe to ghost mode changes to update HasAnyGhostMode and playback state
            controller.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ControllerViewModel.IsGhostMode))
                {
                    OnPropertyChanged(nameof(HasAnyGhostMode));
                    if (s is ControllerViewModel ctrl)
                        UpdatePlaybackForController(ctrl);
                }
            };

            // Subscribe to recording state changes
            controller.RecordingStateChanged += OnControllerRecordingStateChanged;

            // Subscribe to selected lap changes for playback control
            controller.SelectedRecordedLapChanged += OnControllerSelectedLapChanged;

            // Populate available recorded laps from the service
            foreach (var lap in _ghostRecordingService.GetRecordedLaps(i + 1))
            {
                controller.AvailableRecordedLaps.Add(lap);
            }

            Controllers.Add(controller);
        }
    }

    private void OnControllerThrottleProfileChanged(object? sender, ThrottleProfileType profile)
    {
        if (sender is ControllerViewModel controller)
        {
            int index = controller.SlotNumber - 1;
            ControllerThrottleProfileChanged?.Invoke(this, (index, profile));
        }
    }

    private void OnControllerGhostSourceChanged(object? sender, GhostSourceType source)
    {
        if (sender is ControllerViewModel controller)
        {
            int index = controller.SlotNumber - 1;
            ControllerGhostSourceChanged?.Invoke(this, (index, source));

            // Update playback state when ghost source changes
            UpdatePlaybackForController(controller);
        }
    }

    private void OnControllerRecordingStateChanged(object? sender, bool isRecording)
    {
        if (sender is ControllerViewModel controller)
        {
            if (isRecording)
            {
                _ghostRecordingService.StartRecording(controller.SlotNumber);
                Log.Information("Started recording for slot {SlotNumber}", controller.SlotNumber);
            }
            else
            {
                _ghostRecordingService.StopRecording(controller.SlotNumber);
                Log.Information("Stopped recording for slot {SlotNumber}", controller.SlotNumber);
            }
        }
    }

    private void OnControllerSelectedLapChanged(object? sender, RecordedLap? selectedLap)
    {
        if (sender is ControllerViewModel controller)
        {
            // Start or stop playback based on selected lap and ghost mode settings
            UpdatePlaybackForController(controller);
        }
    }

    /// <summary>
    /// Updates playback state for a controller based on its current settings.
    /// Starts playback if ghost mode is enabled, source is RecordedLap, and a lap is selected.
    /// Stops playback otherwise.
    /// </summary>
    /// <param name="controller">The controller to update.</param>
    /// <param name="isPowerEnabled">Whether track power is currently enabled.</param>
    public void UpdatePlaybackForController(ControllerViewModel controller, bool isPowerEnabled = false)
    {
        int slotNumber = controller.SlotNumber;

        // Should playback be active?
        bool shouldPlay = controller.IsGhostMode &&
                          controller.GhostSource == GhostSourceType.RecordedLap &&
                          controller.SelectedRecordedLap != null &&
                          isPowerEnabled;

        if (shouldPlay && controller.SelectedRecordedLap != null)
        {
            // Start or update playback with the selected lap
            if (!_ghostPlaybackService.IsPlaying(slotNumber) ||
                _ghostPlaybackService.GetCurrentLap(slotNumber)?.Id != controller.SelectedRecordedLap.Id)
            {
                // Use GhostThrottleLevel as the approach speed while waiting for the first lap
                byte approachSpeed = (byte)controller.GhostThrottleLevel;
                _ghostPlaybackService.StartPlayback(slotNumber, controller.SelectedRecordedLap, approachSpeed);
                Log.Information("Started playback for slot {SlotNumber}: {LapName}, approach speed={ApproachSpeed}",
                    slotNumber, controller.SelectedRecordedLap.DisplayName, approachSpeed);
            }
        }
        else
        {
            // Stop playback if active
            if (_ghostPlaybackService.IsPlaying(slotNumber))
            {
                _ghostPlaybackService.StopPlayback(slotNumber);
                Log.Information("Stopped playback for slot {SlotNumber}", slotNumber);
            }
        }
    }

    /// <summary>
    /// Updates playback state for all controllers when power state changes.
    /// </summary>
    /// <param name="isPowerEnabled">Whether track power is enabled.</param>
    public void UpdateAllPlaybackStates(bool isPowerEnabled)
    {
        foreach (var controller in Controllers)
        {
            UpdatePlaybackForController(controller, isPowerEnabled);
        }
    }

    /// <summary>
    /// Stops all playback for all slots.
    /// </summary>
    public void StopAllPlayback()
    {
        for (int i = 1; i <= MaxControllers; i++)
        {
            _ghostPlaybackService.StopPlayback(i);
        }
    }

    private void OnRecordingStarted(object? sender, LapRecordingStartedEventArgs e)
    {
        // Update controller to show actively recording (after first finish line crossing)
        Dispatcher.UIThread.Post(() =>
        {
            var controller = Controllers.FirstOrDefault(c => c.SlotNumber == e.SlotNumber);
            if (controller != null)
            {
                controller.IsActivelyRecording = true;
                Log.Information("Lap started for slot {SlotNumber} - actively recording throttle samples", e.SlotNumber);
            }
        });
    }

    private void OnRecordingCompleted(object? sender, LapRecordingCompletedEventArgs e)
    {
        // Add the recorded lap to the appropriate controller's available laps
        Dispatcher.UIThread.Post(() =>
        {
            var controller = Controllers.FirstOrDefault(c => c.SlotNumber == e.SlotNumber);
            if (controller != null)
            {
                controller.AvailableRecordedLaps.Add(e.RecordedLap);
                controller.RecordedLapCount++;

                // Save to persistent storage after each lap is recorded
                _ghostRecordingService.SaveToStorage();

                Log.Information(
                    "Recording completed for slot {SlotNumber}: lap {LapNum}/{TotalLaps}, {SampleCount} samples, {LapTime:F2}s",
                    e.SlotNumber, controller.RecordedLapCount, controller.LapsToRecord,
                    e.RecordedLap.SampleCount, e.RecordedLap.LapTimeSeconds);

                // Check if we need to record more laps
                if (controller.RecordedLapCount < controller.LapsToRecord)
                {
                    // Continue recording the next lap - the lap end is also the next lap start
                    // Use ContinueRecording to skip the "waiting for lap start" phase
                    _ghostRecordingService.ContinueRecording(e.SlotNumber, e.TrueLapEndTime);
                    Log.Information("Continuing recording for lap {NextLap}/{TotalLaps} on slot {SlotNumber}",
                        controller.RecordedLapCount + 1, controller.LapsToRecord, e.SlotNumber);
                }
                else
                {
                    // All laps recorded - stop recording
                    controller.IsRecording = false;
                    controller.IsActivelyRecording = false;

                    // Find the best lap from this recording session (most recent laps)
                    var sessionLaps = controller.AvailableRecordedLaps
                        .OrderByDescending(l => l.RecordedAt)
                        .Take(controller.LapsToRecord)
                        .ToList();
                    var bestLap = sessionLaps.OrderBy(l => l.LapTimeSeconds).FirstOrDefault();

                    Log.Information(
                        "Recording completed for slot {SlotNumber}: {LapCount} lap(s) recorded, best time {BestTime:F2}s. Select a lap to play back.",
                        e.SlotNumber, controller.LapsToRecord, bestLap?.LapTimeSeconds ?? 0);

                    // Note: We do NOT auto-select the lap to avoid immediately starting playback.
                    // The user should manually select which lap they want to use for ghost playback.
                }
            }
        });
    }

    /// <summary>
    /// Resets all controllers to their initial state.
    /// </summary>
    public void ResetControllers()
    {
        foreach (var controller in Controllers)
        {
            controller.Reset();
        }
    }

    /// <summary>
    /// Checks if recording is active for a specific slot.
    /// </summary>
    public bool IsRecording(int slotNumber) => _ghostRecordingService.IsRecording(slotNumber);

    /// <summary>
    /// Records a throttle sample for the specified slot.
    /// </summary>
    public void RecordThrottleSample(int slotNumber, byte throttleValue, int powerLevel, DateTime timestamp)
    {
        _ghostRecordingService.RecordThrottleSample(slotNumber, throttleValue, powerLevel, timestamp);
    }

    /// <summary>
    /// Notifies recording service that a lap was completed.
    /// </summary>
    public void NotifyRecordingLapCompleted(int slotNumber, double lapTimeSeconds, DateTime trueEventTime)
    {
        _ghostRecordingService.NotifyLapCompleted(slotNumber, lapTimeSeconds, trueEventTime);
    }

    /// <summary>
    /// Checks if playback is active for a specific slot.
    /// </summary>
    public bool IsPlaybackActive(int slotNumber) => _ghostPlaybackService.IsPlaying(slotNumber);

    /// <summary>
    /// Notifies playback service that a lap was completed.
    /// </summary>
    public void NotifyPlaybackLapCompleted(int slotNumber)
    {
        _ghostPlaybackService.NotifyLapCompleted(slotNumber);
    }

    /// <summary>
    /// Gets the current throttle value for playback.
    /// </summary>
    public byte GetPlaybackThrottleValue(int slotNumber) => _ghostPlaybackService.GetCurrentThrottleValue(slotNumber);
}
