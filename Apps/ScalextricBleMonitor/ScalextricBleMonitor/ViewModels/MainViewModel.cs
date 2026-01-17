using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scalextric;
using ScalextricBleMonitor.Models;
using ScalextricBleMonitor.Services;
using Serilog;

namespace ScalextricBleMonitor.ViewModels;

/// <summary>
/// Main view model for the BLE monitor window.
/// Observes BLE connection state and exposes bindable properties for the UI.
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IBleMonitorService _bleMonitorService;
    private readonly IGhostRecordingService _ghostRecordingService;
    private readonly IGhostPlaybackService _ghostPlaybackService;
    private readonly IPowerHeartbeatService _powerHeartbeatService;
    private readonly ITimingCalibrationService _timingCalibrationService;
    private readonly AppSettings _settings;
    private IWindowService? _windowService;
    private bool _disposed;

    // Brush constants for connection states
    private static readonly ISolidColorBrush ConnectedBrush = new SolidColorBrush(Color.FromRgb(0, 200, 83));   // Green
    private static readonly ISolidColorBrush DisconnectedBrush = new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Red
    private static readonly ISolidColorBrush GattConnectedBrush = new SolidColorBrush(Color.FromRgb(0, 150, 255)); // Blue
    private static readonly ISolidColorBrush ConnectedTextBrush = new SolidColorBrush(Color.FromRgb(0, 200, 83));
    private static readonly ISolidColorBrush DisconnectedTextBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128)); // Gray

    /// <summary>
    /// Indicates whether the Scalextric device is currently detected via advertisement.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusIndicatorBrush))]
    [NotifyPropertyChangedFor(nameof(StatusTextBrush))]
    [NotifyPropertyChangedFor(nameof(ConnectionStatusText))]
    private bool _isConnected;

    /// <summary>
    /// Indicates whether we have an active GATT connection.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusIndicatorBrush))]
    [NotifyPropertyChangedFor(nameof(ConnectionStatusText))]
    private bool _isGattConnected;

    /// <summary>
    /// Indicates whether track power is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isPowerEnabled;

    /// <summary>
    /// When true, use individual per-slot power levels. When false, use global PowerLevel for all slots.
    /// </summary>
    [ObservableProperty]
    private bool _usePerSlotPower = true;

    /// <summary>
    /// Global power level for all slots (0-63). Only used when UsePerSlotPower is false.
    /// </summary>
    [ObservableProperty]
    private int _powerLevel = 63;

    partial void OnPowerLevelChanged(int value)
    {
        // Update status text when power level changes while power is enabled
        if (IsPowerEnabled)
        {
            StatusText = $"Power enabled at level {value}";
        }
    }

    /// <summary>
    /// Additional status information (e.g., last seen time, error messages).
    /// </summary>
    [ObservableProperty]
    private string _statusText = "Initializing...";

    /// <summary>
    /// The device name when detected.
    /// </summary>
    [ObservableProperty]
    private string _deviceName = string.Empty;

    /// <summary>
    /// Discovered GATT services and characteristics.
    /// </summary>
    public ObservableCollection<ServiceViewModel> Services { get; } = [];

    /// <summary>
    /// Live notification data received from the powerbase.
    /// </summary>
    public ObservableCollection<NotificationDataViewModel> NotificationLog { get; } = [];

    /// <summary>
    /// Filtered notification log based on current filter settings.
    /// </summary>
    public ObservableCollection<NotificationDataViewModel> FilteredNotificationLog { get; } = [];

    /// <summary>
    /// Characteristic filter for notifications: 0=All, 1=Throttle, 2=Slot, 3=Track, 4=CarId
    /// </summary>
    [ObservableProperty]
    private int _notificationCharacteristicFilter;

    partial void OnNotificationCharacteristicFilterChanged(int value)
    {
        RefreshFilteredNotificationLog();
    }

    /// <summary>
    /// Whether the notification log is paused (not accepting new entries).
    /// </summary>
    [ObservableProperty]
    private bool _isNotificationLogPaused;

    private const int MaxNotificationLogEntries = 100;
    private const int MaxControllers = 6;

    // Notification batching to reduce UI dispatcher load at high notification rates (20-100Hz)
    private const int NotificationBatchIntervalMs = 50; // Flush batched notifications every 50ms
    private readonly ConcurrentQueue<BleNotificationEventArgs> _notificationBatch = new();
    private Timer? _notificationBatchTimer;

    /// <summary>
    /// Controller status for each slot (1-6).
    /// </summary>
    public ObservableCollection<ControllerViewModel> Controllers { get; } = [];

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
            if (index >= 0 && index < _settings.SlotThrottleProfiles.Length)
            {
                _settings.SlotThrottleProfiles[index] = profile.ToString();
                _settings.Save();
            }
        }
    }

    private void OnControllerGhostSourceChanged(object? sender, GhostSourceType source)
    {
        if (sender is ControllerViewModel controller)
        {
            int index = controller.SlotNumber - 1;
            if (index >= 0 && index < _settings.SlotGhostSources.Length)
            {
                _settings.SlotGhostSources[index] = source.ToString();
                _settings.Save();
            }

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

    private void OnControllerSelectedLapChanged(object? sender, Models.RecordedLap? selectedLap)
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
    private void UpdatePlaybackForController(ControllerViewModel controller)
    {
        int slotNumber = controller.SlotNumber;

        // Should playback be active?
        bool shouldPlay = controller.IsGhostMode &&
                          controller.GhostSource == GhostSourceType.RecordedLap &&
                          controller.SelectedRecordedLap != null &&
                          IsPowerEnabled;

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
    /// Brush for the status indicator circle.
    /// Green = detected, Blue = GATT connected, Red = not found.
    /// </summary>
    public ISolidColorBrush StatusIndicatorBrush =>
        IsGattConnected ? GattConnectedBrush :
        IsConnected ? ConnectedBrush :
        DisconnectedBrush;

    /// <summary>
    /// Brush for the connection status text.
    /// </summary>
    public ISolidColorBrush StatusTextBrush => IsConnected ? ConnectedTextBrush : DisconnectedTextBrush;

    /// <summary>
    /// Text showing connection state.
    /// </summary>
    public string ConnectionStatusText =>
        IsGattConnected ? "GATT Connected" :
        IsConnected ? "Detected" :
        "Disconnected";

    /// <summary>
    /// Creates a MainViewModel with default services. Used for design-time and simple instantiation.
    /// </summary>
    public MainViewModel() : this(
        new BleMonitorService(),
        new GhostRecordingService(),
        new GhostPlaybackService(),
        new PowerHeartbeatService(new BleMonitorService()),
        new TimingCalibrationService(),
        AppSettings.Load())
    {
    }

    /// <summary>
    /// Creates a MainViewModel with injected dependencies.
    /// </summary>
    /// <param name="bleMonitorService">The BLE monitoring service.</param>
    /// <param name="ghostRecordingService">The ghost recording service.</param>
    /// <param name="ghostPlaybackService">The ghost playback service.</param>
    /// <param name="powerHeartbeatService">The power heartbeat service.</param>
    /// <param name="timingCalibrationService">The timing calibration service.</param>
    /// <param name="settings">The application settings.</param>
    public MainViewModel(
        IBleMonitorService bleMonitorService,
        IGhostRecordingService ghostRecordingService,
        IGhostPlaybackService ghostPlaybackService,
        IPowerHeartbeatService powerHeartbeatService,
        ITimingCalibrationService timingCalibrationService,
        AppSettings settings)
    {
        _bleMonitorService = bleMonitorService;
        _ghostRecordingService = ghostRecordingService;
        _ghostPlaybackService = ghostPlaybackService;
        _powerHeartbeatService = powerHeartbeatService;
        _timingCalibrationService = timingCalibrationService;
        _settings = settings;

        _bleMonitorService.ConnectionStateChanged += OnConnectionStateChanged;
        _bleMonitorService.StatusMessageChanged += OnStatusMessageChanged;
        _bleMonitorService.ServicesDiscovered += OnServicesDiscovered;
        _bleMonitorService.NotificationReceived += OnNotificationReceived;
        _bleMonitorService.CharacteristicValueRead += OnCharacteristicValueRead;

        // Subscribe to recording events
        _ghostRecordingService.RecordingStarted += OnRecordingStarted;
        _ghostRecordingService.RecordingCompleted += OnRecordingCompleted;

        // Subscribe to power heartbeat errors
        _powerHeartbeatService.HeartbeatError += OnPowerHeartbeatError;

        // Initialize from persisted settings
        _powerLevel = _settings.PowerLevel;
        _usePerSlotPower = _settings.UsePerSlotPower;

        // Load recorded laps from storage before initializing controllers
        _ghostRecordingService.LoadFromStorage();

        InitializeControllers();

        // Start notification batch timer to reduce UI dispatcher load
        _notificationBatchTimer = new Timer(
            FlushNotificationBatch,
            null,
            NotificationBatchIntervalMs,
            NotificationBatchIntervalMs);
    }

    private void OnPowerHeartbeatError(object? sender, string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusText = message;
            IsPowerEnabled = false;
        });
    }

    /// <summary>
    /// Starts BLE scanning. Call this when the window is loaded.
    /// </summary>
    public void StartMonitoring()
    {
        _bleMonitorService.StartScanning();
    }

    /// <summary>
    /// Stops BLE scanning. Call this when the window is closing.
    /// </summary>
    public void StopMonitoring()
    {
        _bleMonitorService.StopScanning();
    }

    private void OnConnectionStateChanged(object? sender, BleConnectionStateEventArgs e)
    {
        // Must dispatch to UI thread for ObservableCollection operations
        Dispatcher.UIThread.Post(() =>
        {
            var wasGattConnected = IsGattConnected;
            IsConnected = e.IsDeviceDetected;
            IsGattConnected = e.IsGattConnected;
            DeviceName = e.DeviceName ?? string.Empty;

            // When GATT connection is first established, send power-off to reset powerbase state
            // This clears any ghost mode that may have been left from a previous session
            if (e.IsGattConnected && !wasGattConnected)
            {
                RunFireAndForget(SendInitialPowerOffAsync, "SendInitialPowerOff");
            }

            if (!e.IsGattConnected)
            {
                // Stop power heartbeat when GATT connection is lost
                _powerHeartbeatService.Stop();
                IsPowerEnabled = false;
            }

            if (!e.IsDeviceDetected)
            {
                Services.Clear();
                ResetControllers();
            }
        });
    }

    private async Task SendInitialPowerOffAsync()
    {
        // Small delay to ensure connection is stable
        await Task.Delay(100);

        await _powerHeartbeatService.SendPowerOffSequenceAsync();
    }

    private void ResetControllers()
    {
        foreach (var controller in Controllers)
        {
            controller.Reset();
        }
    }

    private void OnStatusMessageChanged(object? sender, string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusText = message;
        });
    }

    private void OnServicesDiscovered(object? sender, BleServicesDiscoveredEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Services.Clear();
            foreach (var service in e.Services)
            {
                var serviceVm = new ServiceViewModel
                {
                    Uuid = service.Uuid,
                    Name = service.Name ?? service.Uuid.ToString()
                };

                foreach (var characteristic in service.Characteristics)
                {
                    serviceVm.Characteristics.Add(new CharacteristicViewModel
                    {
                        Uuid = characteristic.Uuid,
                        ServiceUuid = service.Uuid,
                        Name = characteristic.Name ?? characteristic.Uuid.ToString(),
                        Properties = characteristic.Properties,
                        ReadAction = ReadCharacteristic
                    });
                }

                Services.Add(serviceVm);
            }

            // Auto-subscribe to notifications after services are discovered
            _bleMonitorService.SubscribeToAllNotifications();
        });
    }

    private void OnNotificationReceived(object? sender, BleNotificationEventArgs e)
    {
        // Queue notification for batched processing to reduce UI dispatcher load
        _notificationBatch.Enqueue(e);
    }

    /// <summary>
    /// Flushes batched notifications to the UI thread.
    /// Called periodically by the batch timer to aggregate multiple notifications
    /// into a single UI update, reducing dispatcher load at high notification rates.
    /// </summary>
    private void FlushNotificationBatch(object? state)
    {
        // Collect all pending notifications
        var batch = new System.Collections.Generic.List<BleNotificationEventArgs>();
        while (_notificationBatch.TryDequeue(out var notification))
        {
            batch.Add(notification);
        }

        if (batch.Count == 0)
            return;

        // Process entire batch in a single UI dispatcher call
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var e in batch)
            {
                // Only update controller states from the Throttle characteristic (0x3b09)
                // Other characteristics like Track (0x3b0c) contain sensor/timing data, not controller input
                if (e.CharacteristicUuid == ScalextricProtocol.Characteristics.Throttle)
                {
                    UpdateControllerStates(e.Data);
                }
                // Process Slot characteristic (0x3b0b) for lap counting
                // Slot notifications are sent when a car passes over the finish line sensor
                else if (e.CharacteristicUuid == ScalextricProtocol.Characteristics.Slot)
                {
                    ProcessSlotSensorData(e.Data);
                }

                // Skip adding to log if paused
                if (IsNotificationLogPaused)
                    continue;

                // Create the notification entry
                var entry = new NotificationDataViewModel
                {
                    Timestamp = e.Timestamp,
                    CharacteristicName = e.CharacteristicName ?? e.CharacteristicUuid.ToString(),
                    CharacteristicUuid = e.CharacteristicUuid,
                    RawData = e.Data,
                    HexData = BitConverter.ToString(e.Data).Replace("-", " "),
                    DecodedData = ScalextricProtocolDecoder.Decode(e.CharacteristicUuid, e.Data)
                };

                // Add to main log
                NotificationLog.Insert(0, entry);

                // Add to filtered log if it passes the filter
                if (PassesCharacteristicFilter(e.CharacteristicUuid))
                {
                    FilteredNotificationLog.Insert(0, entry);
                }
            }

            // Trim logs after batch processing
            while (NotificationLog.Count > MaxNotificationLogEntries)
            {
                NotificationLog.RemoveAt(NotificationLog.Count - 1);
            }
            while (FilteredNotificationLog.Count > MaxNotificationLogEntries)
            {
                FilteredNotificationLog.RemoveAt(FilteredNotificationLog.Count - 1);
            }
        });
    }

    private bool PassesCharacteristicFilter(Guid characteristicUuid)
    {
        return NotificationCharacteristicFilter switch
        {
            0 => true, // All
            1 => characteristicUuid == ScalextricProtocol.Characteristics.Throttle,
            2 => characteristicUuid == ScalextricProtocol.Characteristics.Slot,
            3 => characteristicUuid == ScalextricProtocol.Characteristics.Track,
            4 => characteristicUuid == ScalextricProtocol.Characteristics.CarId,
            _ => true
        };
    }

    private void RefreshFilteredNotificationLog()
    {
        FilteredNotificationLog.Clear();
        foreach (var entry in NotificationLog)
        {
            if (PassesCharacteristicFilter(entry.CharacteristicUuid))
            {
                FilteredNotificationLog.Add(entry);
            }
        }
    }

    private void UpdateControllerStates(byte[] data)
    {
        // First byte appears to be a header/status byte, controller data starts at index 1
        // data[0] = header/status
        // data[1] = Controller 1
        // data[2] = Controller 2
        // etc.
        var timestamp = DateTime.UtcNow;
        for (int i = 1; i < data.Length && (i - 1) < Controllers.Count; i++)
        {
            var controller = Controllers[i - 1];
            controller.UpdateFromByte(data[i]);

            // Feed throttle samples to the recording service if recording is active
            if (_ghostRecordingService.IsRecording(controller.SlotNumber))
            {
                byte throttleValue = (byte)(data[i] & ScalextricProtocol.ThrottleData.ThrottleMask);
                // Pass the current power level so the recording captures actual power delivered
                int powerLevel = UsePerSlotPower ? controller.PowerLevel : PowerLevel;
                _ghostRecordingService.RecordThrottleSample(controller.SlotNumber, throttleValue, powerLevel, timestamp);
            }
        }
    }

    private void ProcessSlotSensorData(byte[] data)
    {
        // Slot characteristic notification format - see ScalextricProtocol.SlotData for byte layout
        if (data.Length >= ScalextricProtocol.SlotData.MinLength)
        {
            int slotId = data[ScalextricProtocol.SlotData.SlotIdOffset];

            // Extract Lane 1 entry timestamp (t1)
            uint lane1Timestamp = ReadUInt32LittleEndian(data, ScalextricProtocol.SlotData.Lane1EntryOffset);

            // Extract Lane 2 entry timestamp (t2)
            uint lane2Timestamp = ReadUInt32LittleEndian(data, ScalextricProtocol.SlotData.Lane2EntryOffset);

            // Capture wall-clock time when notification arrived
            var notificationArrivalTime = DateTime.UtcNow;

            // Process timing calibration if awaiting first slot notification
            _timingCalibrationService.ProcessSlotNotification(lane1Timestamp, lane2Timestamp, notificationArrivalTime);

            // Use max of lane timestamps for event time calculation
            uint maxTimestamp = Math.Max(lane1Timestamp, lane2Timestamp);

            // Valid slot IDs are 1-6
            if (slotId >= 1 && slotId <= MaxControllers)
            {
                var controller = Controllers[slotId - 1];

                // Update the controller with both lane timestamps
                bool lapCompleted = controller.UpdateFinishLineTimestamps(lane1Timestamp, lane2Timestamp);

                // Notify recording service if a lap was completed while recording
                if (lapCompleted && _ghostRecordingService.IsRecording(slotId))
                {
                    // Calculate the true event time using calibration
                    var trueEventTime = _timingCalibrationService.CalculateTrueEventTime(maxTimestamp);
                    if (trueEventTime.HasValue)
                    {
                        _ghostRecordingService.NotifyLapCompleted(slotId, controller.LastLapTimeSeconds, trueEventTime.Value);
                    }
                    else
                    {
                        // Fallback if calibration not available - use notification arrival time
                        Log.Warning("TIMING: No calibration available for lap recording, using notification time as fallback");
                        _ghostRecordingService.NotifyLapCompleted(slotId, controller.LastLapTimeSeconds, notificationArrivalTime);
                    }
                }

                // Notify playback service if a lap was completed (to restart ghost car playback)
                if (lapCompleted && _ghostPlaybackService.IsPlaying(slotId))
                {
                    _ghostPlaybackService.NotifyLapCompleted(slotId);
                    Log.Debug("Ghost car lap completed for slot {SlotId}, restarting playback", slotId);
                }
            }
        }
    }

    /// <summary>
    /// Reads a 32-bit unsigned integer from a byte array in little-endian format.
    /// </summary>
    private static uint ReadUInt32LittleEndian(byte[] data, int offset)
    {
        return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Save settings before disposing
        _settings.PowerLevel = PowerLevel;
        _settings.UsePerSlotPower = UsePerSlotPower;
        for (int i = 0; i < Controllers.Count && i < _settings.SlotPowerLevels.Length; i++)
        {
            _settings.SlotPowerLevels[i] = Controllers[i].PowerLevel;
            if (i < _settings.SlotGhostModes.Length)
            {
                _settings.SlotGhostModes[i] = Controllers[i].IsGhostMode;
            }
            if (i < _settings.SlotGhostThrottleLevels.Length)
            {
                _settings.SlotGhostThrottleLevels[i] = Controllers[i].GhostThrottleLevel;
            }
            if (i < _settings.SlotGhostSources.Length)
            {
                _settings.SlotGhostSources[i] = Controllers[i].GhostSource.ToString();
            }
        }
        _settings.Save();

        // Stop notification batch timer
        _notificationBatchTimer?.Dispose();
        _notificationBatchTimer = null;

        // Send power-off commands to stop any ghost cars before disconnecting
        // This must happen before we dispose the BLE service
        if (IsGattConnected)
        {
            SendShutdownPowerOff();
        }

        _bleMonitorService.ConnectionStateChanged -= OnConnectionStateChanged;
        _bleMonitorService.StatusMessageChanged -= OnStatusMessageChanged;
        _bleMonitorService.ServicesDiscovered -= OnServicesDiscovered;
        _bleMonitorService.NotificationReceived -= OnNotificationReceived;
        _bleMonitorService.CharacteristicValueRead -= OnCharacteristicValueRead;
        _bleMonitorService.Dispose();

        _ghostRecordingService.RecordingStarted -= OnRecordingStarted;
        _ghostRecordingService.RecordingCompleted -= OnRecordingCompleted;

        _powerHeartbeatService.HeartbeatError -= OnPowerHeartbeatError;
        _powerHeartbeatService.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Sends power-off commands during shutdown to stop ghost cars.
    /// Uses best-effort approach: sends commands synchronously but with short timeout
    /// to avoid blocking the UI thread during shutdown.
    /// </summary>
    private void SendShutdownPowerOff()
    {
        try
        {
            // Best-effort: send power-off sequence
            // We don't wait for responses - just fire and let the service handle it
            // This is acceptable during shutdown since we're disposing anyway
            _powerHeartbeatService.SendPowerOffSequenceAsync().Wait(200);
        }
        catch
        {
            // Ignore errors during shutdown - we're disposing anyway
        }
    }

    private void OnCharacteristicValueRead(object? sender, BleCharacteristicReadEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Find the characteristic in our Services collection and update its value
            foreach (var service in Services)
            {
                if (service.Uuid == e.ServiceUuid)
                {
                    foreach (var characteristic in service.Characteristics)
                    {
                        if (characteristic.Uuid == e.CharacteristicUuid)
                        {
                            if (e.Success)
                            {
                                characteristic.LastReadValue = e.Data;
                                characteristic.LastReadHex = BitConverter.ToString(e.Data).Replace("-", " ");
                                characteristic.LastReadText = TryDecodeAsText(e.Data);
                                characteristic.LastReadError = null;
                            }
                            else
                            {
                                characteristic.LastReadValue = null;
                                characteristic.LastReadHex = null;
                                characteristic.LastReadText = null;
                                characteristic.LastReadError = e.ErrorMessage;
                            }
                            return;
                        }
                    }
                }
            }
        });
    }

    private static string? TryDecodeAsText(byte[] data)
    {
        if (data.Length == 0) return null;

        // Check if it looks like printable ASCII
        bool isPrintable = data.All(b => b >= 32 && b < 127);
        if (isPrintable)
        {
            return System.Text.Encoding.ASCII.GetString(data);
        }

        return null;
    }

    /// <summary>
    /// Requests a read of the specified characteristic.
    /// </summary>
    public void ReadCharacteristic(Guid serviceUuid, Guid characteristicUuid)
    {
        _bleMonitorService.ReadCharacteristic(serviceUuid, characteristicUuid);
    }

    // Delay between BLE write operations to avoid flooding the connection
    private const int BleWriteDelayMs = 50;

    /// <summary>
    /// Enables track power with the current power level.
    /// </summary>
    public void EnablePower()
    {
        if (!IsGattConnected) return;

        // Run the async enable operation without blocking
        RunFireAndForget(EnablePowerAsync, "EnablePower");
    }

    private async Task EnablePowerAsync()
    {
        StatusText = "Writing throttle profiles...";

        // First write the throttle profiles for all slots sequentially
        var profilesWritten = await WriteThrottleProfilesAsync();

        if (!profilesWritten)
        {
            StatusText = "Failed to write throttle profiles";
            return;
        }

        // Small delay before starting power
        await Task.Delay(BleWriteDelayMs);

        // Reset timing calibration - wait for first slot notification after power-on
        _timingCalibrationService.Reset();

        // Start the power heartbeat
        IsPowerEnabled = true;
        StatusText = $"Power enabled at level {PowerLevel}";

        // Start playback for any controllers configured for recorded lap ghost mode
        foreach (var controller in Controllers)
        {
            UpdatePlaybackForController(controller);
        }

        // Start continuous power command sending using the heartbeat service
        _powerHeartbeatService.Start(BuildPowerCommand);
    }

    /// <summary>
    /// Builds a power command using per-controller power levels and ghost mode settings.
    /// In ghost mode, uses GhostThrottleLevel as direct throttle; otherwise uses PowerLevel as multiplier.
    /// </summary>
    private byte[] BuildPowerCommand()
    {
        var builder = new ScalextricProtocol.CommandBuilder
        {
            Type = ScalextricProtocol.CommandType.PowerOnRacing
        };

        // Always set per-slot settings to handle ghost mode correctly
        for (int i = 0; i < Controllers.Count; i++)
        {
            var controller = Controllers[i];
            var slot = builder.GetSlot(i + 1);

            // When recording is active, disable ghost mode so controller input works
            bool isRecording = controller.IsRecording;

            if (controller.IsGhostMode && !isRecording)
            {
                // Ghost mode (not recording): determine throttle based on GhostSource
                slot.GhostMode = true;

                if (controller.GhostSource == GhostSourceType.FixedSpeed)
                {
                    // Fixed speed mode: use GhostThrottleLevel as constant throttle
                    slot.PowerMultiplier = (byte)controller.GhostThrottleLevel;
                }
                else // RecordedLap
                {
                    // Recorded lap mode: get interpolated throttle from playback service
                    // The recorded values are already scaled by the power level that was in
                    // effect during recording, so we use them directly.
                    if (_ghostPlaybackService.IsPlaying(i + 1))
                    {
                        slot.PowerMultiplier = _ghostPlaybackService.GetCurrentThrottleValue(i + 1);
                    }
                    else
                    {
                        // Playback not started - car stopped
                        slot.PowerMultiplier = 0;
                    }
                }
            }
            else
            {
                // Normal mode OR recording mode: use PowerLevel as multiplier so controller works
                slot.PowerMultiplier = (byte)(UsePerSlotPower ? controller.PowerLevel : PowerLevel);
                slot.GhostMode = false;
            }
        }

        return builder.Build();
    }

    /// <summary>
    /// Disables track power.
    /// </summary>
    public void DisablePower()
    {
        if (!IsGattConnected) return;

        // Stop the heartbeat first
        _powerHeartbeatService.Stop();

        RunFireAndForget(DisablePowerAsync, "DisablePower");
    }

    private async Task DisablePowerAsync()
    {
        StatusText = "Sending power off command...";

        // Stop all playback
        for (int i = 1; i <= 6; i++)
        {
            _ghostPlaybackService.StopPlayback(i);
        }

        await _powerHeartbeatService.SendPowerOffSequenceAsync();

        IsPowerEnabled = false;
        StatusText = "Power disabled";
    }

    /// <summary>
    /// Toggles track power on/off.
    /// </summary>
    [RelayCommand]
    private void TogglePower()
    {
        if (IsPowerEnabled)
            DisablePower();
        else
            EnablePower();
    }

    /// <summary>
    /// Writes throttle profiles to all 6 slots sequentially with delays.
    /// Each slot uses its configured profile type (Linear, Exponential, or Stepped).
    /// Each slot requires 6 blocks of 17 bytes (block index + 16 throttle values).
    /// </summary>
    private async Task<bool> WriteThrottleProfilesAsync()
    {
        if (!IsGattConnected) return false;

        for (int slot = 1; slot <= 6; slot++)
        {
            var controller = Controllers[slot - 1];
            var profileType = controller.ThrottleProfile;

            // Get the throttle curve blocks for this slot's profile type
            var blocks = ThrottleProfileHelper.CreateBlocks(profileType);
            var uuid = ScalextricProtocol.Characteristics.GetThrottleProfileForSlot(slot);

            // Write all 6 blocks for this slot
            for (int blockIndex = 0; blockIndex < ScalextricProtocol.ThrottleProfile.BlockCount; blockIndex++)
            {
                StatusText = $"Writing throttle profile slot {slot} ({profileType}), block {blockIndex + 1}/6...";

                var success = await _bleMonitorService.WriteCharacteristicAwaitAsync(uuid, blocks[blockIndex]);

                if (!success)
                {
                    StatusText = $"Failed to write throttle profile for slot {slot}, block {blockIndex}";
                    return false;
                }

                // Delay between writes to avoid flooding the BLE connection
                await Task.Delay(BleWriteDelayMs);
            }
        }

        StatusText = "Throttle profiles written successfully";
        return true;
    }

    /// <summary>
    /// Safely runs an async task without awaiting, handling any exceptions.
    /// This replaces the fire-and-forget pattern `_ = AsyncMethod()` to ensure errors are not silently swallowed.
    /// </summary>
    private void RunFireAndForget(Func<Task> asyncAction, string operationName)
    {
        Task.Run(async () =>
        {
            try
            {
                await asyncAction();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in {OperationName}", operationName);
                Dispatcher.UIThread.Post(() => StatusText = $"Error: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Indicates whether the notification window is currently open.
    /// </summary>
    [ObservableProperty]
    private bool _isNotificationWindowOpen;

    /// <summary>
    /// Indicates whether the GATT services window is currently open.
    /// </summary>
    [ObservableProperty]
    private bool _isGattServicesWindowOpen;

    /// <summary>
    /// Indicates whether the ghost control window is currently open.
    /// </summary>
    [ObservableProperty]
    private bool _isGhostControlWindowOpen;

    /// <summary>
    /// Returns true if any controller is currently in ghost mode.
    /// Used by Ghost Control window to show placeholder when no ghost slots.
    /// </summary>
    public bool HasAnyGhostMode => Controllers.Any(c => c.IsGhostMode);

    /// <summary>
    /// Sets the window service for managing child windows.
    /// </summary>
    /// <param name="windowService">The window service instance.</param>
    public void SetWindowService(IWindowService windowService)
    {
        _windowService = windowService;
        _windowService.GattServicesWindowClosed += (_, _) => IsGattServicesWindowOpen = false;
        _windowService.NotificationWindowClosed += (_, _) => IsNotificationWindowOpen = false;
        _windowService.GhostControlWindowClosed += (_, _) => IsGhostControlWindowOpen = false;
    }

    /// <summary>
    /// Shows the GATT Services window.
    /// </summary>
    [RelayCommand]
    private void ShowGattServices()
    {
        if (_windowService == null) return;
        IsGattServicesWindowOpen = true;
        _windowService.ShowGattServicesWindow();
    }

    /// <summary>
    /// Shows the Live Notifications window.
    /// </summary>
    [RelayCommand]
    private void ShowNotifications()
    {
        if (_windowService == null) return;
        IsNotificationWindowOpen = true;
        _windowService.ShowNotificationWindow();
    }

    /// <summary>
    /// Shows the Ghost Control window.
    /// </summary>
    [RelayCommand]
    private void ShowGhostControl()
    {
        if (_windowService == null) return;
        IsGhostControlWindowOpen = true;
        _windowService.ShowGhostControlWindow();
    }

    /// <summary>
    /// Clears the notification log.
    /// </summary>
    [RelayCommand]
    private void ClearNotificationLog()
    {
        NotificationLog.Clear();
        FilteredNotificationLog.Clear();
    }
}
