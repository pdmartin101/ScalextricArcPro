using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scalextric;
using ScalextricBle;
using ScalextricRace.Services;
using Serilog;

namespace ScalextricRace.ViewModels;

/// <summary>
/// Main view model for the ScalextricRace application.
/// Manages connection state, race status, and car/slot information.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    #region Fields

    private readonly IBleService? _bleService;
    private readonly AppSettings _settings;

    /// <summary>
    /// Tracks whether power should be enabled once connected.
    /// Set from saved settings on startup.
    /// </summary>
    private bool _pendingPowerEnable;

    #endregion

    #region Connection State

    /// <summary>
    /// Indicates whether BLE scanning is active.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionStatusText))]
    [NotifyPropertyChangedFor(nameof(StatusIndicatorColor))]
    private bool _isScanning;

    /// <summary>
    /// Indicates whether a Scalextric device has been detected via BLE advertisement.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionStatusText))]
    [NotifyPropertyChangedFor(nameof(StatusIndicatorColor))]
    private bool _isDeviceDetected;

    /// <summary>
    /// Indicates whether an active GATT connection exists to the powerbase.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionStatusText))]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    [NotifyPropertyChangedFor(nameof(StatusIndicatorColor))]
    private bool _isGattConnected;

    /// <summary>
    /// Status message to display to the user.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "Starting...";

    /// <summary>
    /// Gets whether the device is connected and ready for commands.
    /// </summary>
    public bool IsConnected => IsGattConnected;

    /// <summary>
    /// Gets the current connection status as a display string.
    /// </summary>
    public string ConnectionStatusText => (IsScanning, IsDeviceDetected, IsGattConnected) switch
    {
        (_, _, true) => "Connected",
        (_, true, false) => "Connecting...",
        (true, false, _) => "Scanning",
        _ => "Disconnected"
    };

    /// <summary>
    /// Gets the brush color for the connection status indicator.
    /// Red = Disconnected/Not scanning
    /// Blue = Scanning/Connecting
    /// Green = Connected
    /// </summary>
    public IBrush StatusIndicatorColor => (IsScanning, IsDeviceDetected, IsGattConnected) switch
    {
        (_, _, true) => Brushes.Green,      // Connected
        (_, true, false) => Brushes.Blue,   // Device found, connecting
        (true, false, _) => Brushes.Blue,   // Scanning
        _ => Brushes.Red                     // Disconnected
    };

    #endregion

    #region Race State

    /// <summary>
    /// Indicates whether track power is currently enabled.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PowerButtonText))]
    private bool _isPowerEnabled;

    /// <summary>
    /// Gets the text to display on the power toggle button.
    /// </summary>
    public string PowerButtonText => IsPowerEnabled ? "POWER OFF" : "POWER ON";

    /// <summary>
    /// The global power level (0-63) applied to all slots.
    /// </summary>
    [ObservableProperty]
    private int _powerLevel = 63;

    /// <summary>
    /// The selected throttle profile type for all cars.
    /// </summary>
    [ObservableProperty]
    private ThrottleProfileType _selectedThrottleProfile = ThrottleProfileType.Linear;

    /// <summary>
    /// Available throttle profile types for selection.
    /// </summary>
    public static ThrottleProfileType[] AvailableThrottleProfiles { get; } =
        Enum.GetValues<ThrottleProfileType>();

    #endregion

    #region Application Info

    /// <summary>
    /// Gets the application title.
    /// </summary>
    public string Title => "Scalextric Race";

    /// <summary>
    /// Gets the application version.
    /// </summary>
    public string Version => "1.0.0";

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the MainViewModel with dependencies.
    /// </summary>
    /// <param name="bleService">The BLE service for device communication.</param>
    /// <param name="settings">The application settings.</param>
    public MainViewModel(AppSettings settings, IBleService? bleService = null)
    {
        _settings = settings;
        _bleService = bleService;

        // Load settings
        PowerLevel = _settings.PowerLevel;
        SelectedThrottleProfile = Enum.TryParse<ThrottleProfileType>(_settings.ThrottleProfile, out var profile)
            ? profile
            : ThrottleProfileType.Linear;

        // If power was enabled when app closed, enable it once connected
        _pendingPowerEnable = _settings.PowerEnabled;

        Log.Information("MainViewModel initialized. PowerLevel={PowerLevel}, ThrottleProfile={ThrottleProfile}, PendingPower={PendingPower}",
            PowerLevel, SelectedThrottleProfile, _pendingPowerEnable);

        // Subscribe to BLE service events
        if (_bleService != null)
        {
            _bleService.ConnectionStateChanged += OnConnectionStateChanged;
            _bleService.StatusMessageChanged += OnStatusMessageChanged;
            _bleService.NotificationReceived += OnNotificationReceived;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts monitoring for Scalextric devices.
    /// Called automatically when the application starts.
    /// </summary>
    public void StartMonitoring()
    {
        if (_bleService == null)
        {
            Log.Warning("BLE service not available - cannot start monitoring");
            StatusMessage = "BLE service not available";
            return;
        }

        Log.Information("Starting BLE monitoring");
        _bleService.StartScanning();
        IsScanning = _bleService.IsScanning;
    }

    /// <summary>
    /// Stops monitoring and cleans up resources.
    /// Called when the application is closing.
    /// </summary>
    public void StopMonitoring()
    {
        // Save settings before stopping
        SaveSettings();

        if (_bleService == null) return;

        Log.Information("Stopping BLE monitoring");

        // Disable power if enabled
        if (IsPowerEnabled)
        {
            DisablePower();
        }

        _bleService.StopScanning();
        IsScanning = false;
    }

    #endregion

    #region Commands

    /// <summary>
    /// Toggles track power on or off.
    /// </summary>
    [RelayCommand]
    private void TogglePower()
    {
        IsPowerEnabled = !IsPowerEnabled;

        if (IsPowerEnabled)
        {
            EnablePower();
        }
        else
        {
            DisablePower();
        }

        // Save settings when power state changes
        SaveSettings();
    }

    #endregion

    #region Partial Methods (Property Change Handlers)

    /// <summary>
    /// Called when PowerLevel changes. Saves settings.
    /// </summary>
    partial void OnPowerLevelChanged(int value)
    {
        SaveSettings();
    }

    /// <summary>
    /// Called when SelectedThrottleProfile changes. Saves settings.
    /// </summary>
    partial void OnSelectedThrottleProfileChanged(ThrottleProfileType value)
    {
        SaveSettings();
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles BLE connection state changes.
    /// </summary>
    private void OnConnectionStateChanged(object? sender, BleConnectionStateEventArgs e)
    {
        // Marshal to UI thread
        Dispatcher.UIThread.Post(() =>
        {
            var wasConnected = IsGattConnected;
            IsDeviceDetected = e.IsDeviceDetected;
            IsGattConnected = e.IsGattConnected;

            // Check if we just connected and have pending power enable
            if (!wasConnected && IsGattConnected && _pendingPowerEnable)
            {
                Log.Information("Connection established, enabling power from saved settings");
                _pendingPowerEnable = false;
                IsPowerEnabled = true;
                EnablePower();
            }
        });
    }

    /// <summary>
    /// Handles BLE status message changes.
    /// </summary>
    private void OnStatusMessageChanged(object? sender, string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusMessage = message;
        });
    }

    /// <summary>
    /// Handles BLE notification data.
    /// </summary>
    private void OnNotificationReceived(object? sender, BleNotificationEventArgs e)
    {
        // Process notifications (throttle, lap timing, etc.)
        // TODO: Implement notification handling
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Saves current settings to disk.
    /// </summary>
    private void SaveSettings()
    {
        _settings.PowerEnabled = IsPowerEnabled;
        _settings.PowerLevel = PowerLevel;
        _settings.ThrottleProfile = SelectedThrottleProfile.ToString();
        _settings.Save();
    }

    /// <summary>
    /// Enables track power and starts the heartbeat loop.
    /// </summary>
    private async void EnablePower()
    {
        if (_bleService == null) return;

        Log.Information("Enabling track power at level {PowerLevel}", PowerLevel);

        // Build and send power-on command using ScalextricBle library
        var builder = new ScalextricProtocol.CommandBuilder
        {
            Type = ScalextricProtocol.CommandType.PowerOnRacing
        };
        builder.SetAllPower((byte)PowerLevel);

        byte[] command = builder.Build();

        var success = await _bleService.WriteCharacteristicAsync(
            ScalextricProtocol.Characteristics.Command,
            command);

        if (success)
        {
            Log.Information("Power enabled successfully");
            StatusMessage = "Power enabled";
            // TODO: Start heartbeat loop (200ms interval)
        }
        else
        {
            Log.Warning("Failed to enable power");
            StatusMessage = "Failed to enable power";
            IsPowerEnabled = false;
            SaveSettings();
        }
    }

    /// <summary>
    /// Disables track power and stops the heartbeat loop.
    /// </summary>
    private async void DisablePower()
    {
        if (_bleService == null) return;

        Log.Information("Disabling track power");

        // Build and send power-off command
        byte[] command = ScalextricProtocol.CommandBuilder.CreatePowerOffCommand();

        var success = await _bleService.WriteCharacteristicAsync(
            ScalextricProtocol.Characteristics.Command,
            command);

        if (success)
        {
            Log.Information("Power disabled successfully");
            StatusMessage = "Power disabled";
            // TODO: Stop heartbeat loop
        }
        else
        {
            Log.Warning("Failed to disable power");
            StatusMessage = "Failed to disable power";
        }
    }

    #endregion
}
