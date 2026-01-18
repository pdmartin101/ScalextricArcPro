using System;
using System.Collections.ObjectModel;
using System.Linq;
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
/// Coordinates between child ViewModels for connection, power, notifications, and ghost control.
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly BleConnectionViewModel _connection;
    private readonly PowerControlViewModel _power;
    private readonly NotificationLogViewModel _notifications;
    private readonly GhostControlViewModel _ghost;
    private readonly ITimingCalibrationService _timingCalibrationService;
    private readonly AppSettings _settings;
    private IWindowService? _windowService;
    private bool _disposed;

    private const int MaxControllers = 6;

    #region Child ViewModel Delegation

    // Connection state
    public bool IsConnected => _connection.IsConnected;
    public bool IsGattConnected => _connection.IsGattConnected;
    public string DeviceName => _connection.DeviceName;
    public ConnectionState CurrentConnectionState => _connection.CurrentConnectionState;
    public string ConnectionStatusText => _connection.ConnectionStatusText;
    public ObservableCollection<ServiceViewModel> Services => _connection.Services;

    // Power control
    public bool IsPowerEnabled => _power.IsPowerEnabled;
    public bool UsePerSlotPower { get => _power.UsePerSlotPower; set => _power.UsePerSlotPower = value; }
    public int PowerLevel { get => _power.PowerLevel; set => _power.PowerLevel = value; }

    // Notification log
    public ObservableCollection<NotificationDataViewModel> NotificationLog => _notifications.NotificationLog;
    public ObservableCollection<NotificationDataViewModel> FilteredNotificationLog => _notifications.FilteredNotificationLog;
    public int NotificationCharacteristicFilter { get => _notifications.NotificationCharacteristicFilter; set => _notifications.NotificationCharacteristicFilter = value; }
    public bool IsNotificationLogPaused { get => _notifications.IsNotificationLogPaused; set => _notifications.IsNotificationLogPaused = value; }

    // Ghost control
    public ObservableCollection<ControllerViewModel> Controllers => _ghost.Controllers;
    public bool HasAnyGhostMode => _ghost.HasAnyGhostMode;

    #endregion

    /// <summary>
    /// Additional status information.
    /// </summary>
    public string StatusText
    {
        get => _connection.StatusText;
        set
        {
            _connection.StatusText = value;
            _power.StatusText = value;
        }
    }

    /// <summary>
    /// Creates a MainViewModel with default services. Used for design-time and simple instantiation.
    /// </summary>
    public MainViewModel() : this(
        new BleService(),
        new GhostRecordingService(),
        new GhostPlaybackService(),
        new PowerHeartbeatService(new BleService()),
        new TimingCalibrationService(),
        AppSettings.Load())
    {
    }

    /// <summary>
    /// Creates a MainViewModel with injected dependencies.
    /// </summary>
    public MainViewModel(
        Services.IBleService bleService,
        IGhostRecordingService ghostRecordingService,
        IGhostPlaybackService ghostPlaybackService,
        IPowerHeartbeatService powerHeartbeatService,
        ITimingCalibrationService timingCalibrationService,
        AppSettings settings)
    {
        _timingCalibrationService = timingCalibrationService;
        _settings = settings;

        // Create child ViewModels
        _connection = new BleConnectionViewModel(bleService);
        _power = new PowerControlViewModel(bleService, powerHeartbeatService, timingCalibrationService, settings);
        _notifications = new NotificationLogViewModel();
        _ghost = new GhostControlViewModel(ghostRecordingService, ghostPlaybackService, settings);

        // Wire up connection events
        _connection.GattConnected += OnGattConnected;
        _connection.GattDisconnected += OnGattDisconnected;
        _connection.DeviceDisconnected += OnDeviceDisconnected;
        _connection.NotificationReceived += OnNotificationReceived;
        _connection.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);

        // Wire up power events
        _power.PowerEnabled += OnPowerEnabled;
        _power.PowerDisabled += OnPowerDisabled;
        _power.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PowerControlViewModel.IsPowerEnabled))
                OnPropertyChanged(nameof(IsPowerEnabled));
            else if (e.PropertyName == nameof(PowerControlViewModel.StatusText))
                OnPropertyChanged(nameof(StatusText));
        };

        // Wire up notification events for processing
        _notifications.ThrottleNotificationReceived += OnThrottleNotificationReceived;
        _notifications.SlotNotificationReceived += OnSlotNotificationReceived;

        // Wire up ghost control events for settings persistence
        _ghost.ControllerThrottleProfileChanged += OnControllerThrottleProfileChanged;
        _ghost.ControllerGhostSourceChanged += OnControllerGhostSourceChanged;
        _ghost.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(GhostControlViewModel.HasAnyGhostMode))
                OnPropertyChanged(nameof(HasAnyGhostMode));
        };

        // Set up power command builder
        _power.PowerCommandBuilder = BuildPowerCommand;
    }

    #region Event Handlers

    private void OnGattConnected(object? sender, EventArgs e)
    {
        _power.SendInitialPowerOff();
    }

    private void OnGattDisconnected(object? sender, EventArgs e)
    {
        _power.OnGattDisconnected();
    }

    private void OnDeviceDisconnected(object? sender, EventArgs e)
    {
        _ghost.ResetControllers();
    }

    private void OnNotificationReceived(object? sender, BleNotificationEventArgs e)
    {
        _notifications.QueueNotification(e);
    }

    private void OnPowerEnabled(object? sender, EventArgs e)
    {
        _ghost.UpdateAllPlaybackStates(true);
    }

    private void OnPowerDisabled(object? sender, EventArgs e)
    {
        _ghost.StopAllPlayback();
    }

    private void OnThrottleNotificationReceived(object? sender, byte[] data)
    {
        UpdateControllerStates(data);
    }

    private void OnSlotNotificationReceived(object? sender, byte[] data)
    {
        ProcessSlotSensorData(data);
    }

    private void OnControllerThrottleProfileChanged(object? sender, (int SlotIndex, ThrottleProfileType Profile) e)
    {
        if (e.SlotIndex >= 0 && e.SlotIndex < _settings.SlotThrottleProfiles.Length)
        {
            _settings.SlotThrottleProfiles[e.SlotIndex] = e.Profile.ToString();
            _settings.Save();
        }
    }

    private void OnControllerGhostSourceChanged(object? sender, (int SlotIndex, GhostSourceType Source) e)
    {
        if (e.SlotIndex >= 0 && e.SlotIndex < _settings.SlotGhostSources.Length)
        {
            _settings.SlotGhostSources[e.SlotIndex] = e.Source.ToString();
            _settings.Save();
        }
    }

    #endregion

    #region Notification Processing

    private void UpdateControllerStates(byte[] data)
    {
        var timestamp = DateTime.UtcNow;
        for (int i = 1; i < data.Length && (i - 1) < Controllers.Count; i++)
        {
            var controller = Controllers[i - 1];
            controller.UpdateFromByte(data[i]);

            // Feed throttle samples to the recording service if recording is active
            if (_ghost.IsRecording(controller.SlotNumber))
            {
                byte throttleValue = (byte)(data[i] & ScalextricProtocol.ThrottleData.ThrottleMask);
                int powerLevel = UsePerSlotPower ? controller.PowerLevel : PowerLevel;
                _ghost.RecordThrottleSample(controller.SlotNumber, throttleValue, powerLevel, timestamp);
            }
        }
    }

    private void ProcessSlotSensorData(byte[] data)
    {
        if (data.Length >= ScalextricProtocol.SlotData.MinLength)
        {
            int slotId = data[ScalextricProtocol.SlotData.SlotIdOffset];

            uint lane1Timestamp = ScalextricProtocolDecoder.ReadUInt32LittleEndian(data, ScalextricProtocol.SlotData.Lane1EntryOffset);
            uint lane2Timestamp = ScalextricProtocolDecoder.ReadUInt32LittleEndian(data, ScalextricProtocol.SlotData.Lane2EntryOffset);

            var notificationArrivalTime = DateTime.UtcNow;

            // Process timing calibration
            _timingCalibrationService.ProcessSlotNotification(lane1Timestamp, lane2Timestamp, notificationArrivalTime);

            uint maxTimestamp = Math.Max(lane1Timestamp, lane2Timestamp);

            if (slotId >= 1 && slotId <= MaxControllers)
            {
                var controller = Controllers[slotId - 1];
                bool lapCompleted = controller.UpdateFinishLineTimestamps(lane1Timestamp, lane2Timestamp);

                // Notify recording service if a lap was completed while recording
                if (lapCompleted && _ghost.IsRecording(slotId))
                {
                    var trueEventTime = _timingCalibrationService.CalculateTrueEventTime(maxTimestamp);
                    if (trueEventTime.HasValue)
                    {
                        _ghost.NotifyRecordingLapCompleted(slotId, controller.LastLapTimeSeconds, trueEventTime.Value);
                    }
                    else
                    {
                        Log.Warning("TIMING: No calibration available for lap recording, using notification time as fallback");
                        _ghost.NotifyRecordingLapCompleted(slotId, controller.LastLapTimeSeconds, notificationArrivalTime);
                    }
                }

                // Notify playback service if a lap was completed
                if (lapCompleted && _ghost.IsPlaybackActive(slotId))
                {
                    _ghost.NotifyPlaybackLapCompleted(slotId);
                    Log.Debug("Ghost car lap completed for slot {SlotId}, restarting playback", slotId);
                }
            }
        }
    }

    #endregion

    #region Power Command Building

    /// <summary>
    /// Builds a power command using per-controller power levels and ghost mode settings.
    /// </summary>
    private byte[] BuildPowerCommand()
    {
        var builder = new ScalextricProtocol.CommandBuilder
        {
            Type = ScalextricProtocol.CommandType.PowerOnRacing
        };

        for (int i = 0; i < Controllers.Count; i++)
        {
            var controller = Controllers[i];
            var slot = builder.GetSlot(i + 1);

            bool isRecording = controller.IsRecording;

            if (controller.IsGhostMode && !isRecording)
            {
                slot.GhostMode = true;

                if (controller.GhostSource == GhostSourceType.FixedSpeed)
                {
                    slot.PowerMultiplier = (byte)controller.GhostThrottleLevel;
                }
                else // RecordedLap
                {
                    if (_ghost.IsPlaybackActive(i + 1))
                    {
                        slot.PowerMultiplier = _ghost.GetPlaybackThrottleValue(i + 1);
                    }
                    else
                    {
                        slot.PowerMultiplier = 0;
                    }
                }
            }
            else
            {
                slot.PowerMultiplier = (byte)(UsePerSlotPower ? controller.PowerLevel : PowerLevel);
                slot.GhostMode = false;
            }
        }

        return builder.Build();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts BLE scanning.
    /// </summary>
    public void StartMonitoring() => _connection.StartMonitoring();

    /// <summary>
    /// Stops BLE scanning.
    /// </summary>
    public void StopMonitoring() => _connection.StopMonitoring();

    /// <summary>
    /// Enables track power.
    /// </summary>
    public void EnablePower() => _power.EnablePower(IsGattConnected);

    /// <summary>
    /// Disables track power.
    /// </summary>
    public void DisablePower() => _power.DisablePower(IsGattConnected);

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
    /// Requests a read of the specified characteristic.
    /// </summary>
    public void ReadCharacteristic(Guid serviceUuid, Guid characteristicUuid)
    {
        _connection.ReadCharacteristic(serviceUuid, characteristicUuid);
    }

    /// <summary>
    /// Clears the notification log.
    /// </summary>
    [RelayCommand]
    private void ClearNotificationLog() => _notifications.ClearNotificationLogCommand.Execute(null);

    #endregion

    #region Window Management

    [ObservableProperty]
    private bool _isNotificationWindowOpen;

    [ObservableProperty]
    private bool _isGattServicesWindowOpen;

    [ObservableProperty]
    private bool _isGhostControlWindowOpen;

    /// <summary>
    /// Sets the window service for managing child windows.
    /// </summary>
    public void SetWindowService(IWindowService windowService)
    {
        _windowService = windowService;
        _windowService.GattServicesWindowClosed += (_, _) => IsGattServicesWindowOpen = false;
        _windowService.NotificationWindowClosed += (_, _) => IsNotificationWindowOpen = false;
        _windowService.GhostControlWindowClosed += (_, _) => IsGhostControlWindowOpen = false;
    }

    [RelayCommand]
    private void ShowGattServices()
    {
        if (_windowService == null) return;
        IsGattServicesWindowOpen = true;
        _windowService.ShowGattServicesWindow();
    }

    [RelayCommand]
    private void ShowNotifications()
    {
        if (_windowService == null) return;
        IsNotificationWindowOpen = true;
        _windowService.ShowNotificationWindow();
    }

    [RelayCommand]
    private void ShowGhostControl()
    {
        if (_windowService == null) return;
        IsGhostControlWindowOpen = true;
        _windowService.ShowGhostControlWindow();
    }

    #endregion

    #region Disposal

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Save settings before disposing
        _power.SaveSettings();
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

        // Send power-off if connected
        if (IsGattConnected)
        {
            _power.SendShutdownPowerOff();
        }

        // Dispose child ViewModels
        _notifications.Dispose();
        _power.Dispose();
        _connection.Dispose();

        GC.SuppressFinalize(this);
    }

    #endregion
}
