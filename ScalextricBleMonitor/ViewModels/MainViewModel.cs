using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ScalextricBleMonitor.Services;

namespace ScalextricBleMonitor.ViewModels;

/// <summary>
/// Main view model for the BLE monitor window.
/// Observes BLE connection state and exposes bindable properties for the UI.
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IBleMonitorService _bleMonitorService;
    private bool _disposed;
    private CancellationTokenSource? _powerHeartbeatCts;

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
    /// Power level for all slots (0-63).
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

    private const int MaxNotificationLogEntries = 100;
    private const int MaxControllers = 6;

    /// <summary>
    /// Controller status for each slot (1-6).
    /// </summary>
    public ObservableCollection<ControllerViewModel> Controllers { get; } = [];

    private void InitializeControllers()
    {
        Controllers.Clear();
        for (int i = 0; i < MaxControllers; i++)
        {
            Controllers.Add(new ControllerViewModel { SlotNumber = i + 1 });
        }
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

    public MainViewModel() : this(new BleMonitorService())
    {
    }

    public MainViewModel(IBleMonitorService bleMonitorService)
    {
        _bleMonitorService = bleMonitorService;
        _bleMonitorService.ConnectionStateChanged += OnConnectionStateChanged;
        _bleMonitorService.StatusMessageChanged += OnStatusMessageChanged;
        _bleMonitorService.ServicesDiscovered += OnServicesDiscovered;
        _bleMonitorService.NotificationReceived += OnNotificationReceived;
        _bleMonitorService.CharacteristicValueRead += OnCharacteristicValueRead;

        InitializeControllers();
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
            IsConnected = e.IsConnected;
            IsGattConnected = e.IsGattConnected;
            DeviceName = e.DeviceName ?? string.Empty;

            if (!e.IsGattConnected)
            {
                // Stop power heartbeat when GATT connection is lost
                StopPowerHeartbeat();
            }

            if (!e.IsConnected)
            {
                Services.Clear();
                ResetControllers();
            }
        });
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
                        Properties = characteristic.Properties
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
        Dispatcher.UIThread.Post(() =>
        {
            // Only update controller states from the Throttle characteristic (0x3b09)
            // Other characteristics like Track (0x3b0c) contain sensor/timing data, not controller input
            if (e.CharacteristicUuid == ScalextricProtocol.Characteristics.Throttle)
            {
                UpdateControllerStates(e.Data);
            }

            // Add new entry at the top
            NotificationLog.Insert(0, new NotificationDataViewModel
            {
                Timestamp = e.Timestamp,
                CharacteristicName = e.CharacteristicName ?? e.CharacteristicUuid.ToString(),
                CharacteristicUuid = e.CharacteristicUuid,
                RawData = e.Data,
                HexData = BitConverter.ToString(e.Data).Replace("-", " "),
                DecodedData = DecodeScalextricData(e.CharacteristicUuid, e.Data)
            });

            // Keep the log from growing too large
            while (NotificationLog.Count > MaxNotificationLogEntries)
            {
                NotificationLog.RemoveAt(NotificationLog.Count - 1);
            }
        });
    }

    private void UpdateControllerStates(byte[] data)
    {
        // First byte appears to be a header/status byte, controller data starts at index 1
        // data[0] = header/status
        // data[1] = Controller 1
        // data[2] = Controller 2
        // etc.
        for (int i = 1; i < data.Length && (i - 1) < Controllers.Count; i++)
        {
            Controllers[i - 1].UpdateFromByte(data[i]);
        }
    }

    private static string DecodeScalextricData(Guid characteristicUuid, byte[] data)
    {
        if (data.Length == 0) return "(empty)";

        // Try to decode based on known Scalextric data formats
        // First byte is header/status, then controller data follows
        // Controller data: Bits 0-5 = throttle (0-63), Bit 6 = brake, Bit 7 = lane change
        if (data.Length >= 2)
        {
            var parts = new System.Collections.Generic.List<string>();

            // First byte is header
            parts.Add($"H:{data[0]:X2}");

            // Remaining bytes are controller data
            for (int i = 1; i < data.Length; i++)
            {
                var b = data[i];
                int throttle = b & 0x3F;
                bool brake = (b & 0x40) != 0;
                bool laneChange = (b & 0x80) != 0;

                var decoded = $"C{i}:T{throttle}";
                if (brake) decoded += "+B";
                if (laneChange) decoded += "+L";
                parts.Add(decoded);
            }

            return string.Join(" | ", parts);
        }
        else if (data.Length == 1)
        {
            return $"H:{data[0]:X2}";
        }

        return "(raw)";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Stop power heartbeat
        _powerHeartbeatCts?.Cancel();
        _powerHeartbeatCts?.Dispose();
        _powerHeartbeatCts = null;

        _bleMonitorService.ConnectionStateChanged -= OnConnectionStateChanged;
        _bleMonitorService.StatusMessageChanged -= OnStatusMessageChanged;
        _bleMonitorService.ServicesDiscovered -= OnServicesDiscovered;
        _bleMonitorService.NotificationReceived -= OnNotificationReceived;
        _bleMonitorService.CharacteristicValueRead -= OnCharacteristicValueRead;
        _bleMonitorService.Dispose();

        GC.SuppressFinalize(this);
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

    // Interval for sending power heartbeat commands (ms)
    private const int PowerHeartbeatIntervalMs = 200;

    /// <summary>
    /// Enables track power with the current power level.
    /// </summary>
    public void EnablePower()
    {
        if (!IsGattConnected) return;

        // Run the async enable operation without blocking
        _ = EnablePowerAsync();
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

        // Start the power heartbeat
        IsPowerEnabled = true;
        StatusText = $"Power enabled at level {PowerLevel}";

        // Cancel any existing heartbeat
        _powerHeartbeatCts?.Cancel();
        _powerHeartbeatCts = new CancellationTokenSource();

        // Start continuous power command sending
        _ = PowerHeartbeatLoopAsync(_powerHeartbeatCts.Token);
    }

    /// <summary>
    /// Continuously sends power commands to keep the track powered.
    /// The powerbase requires periodic commands to maintain power.
    /// </summary>
    private async Task PowerHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && IsGattConnected && IsPowerEnabled)
            {
                var command = ScalextricProtocol.CommandBuilder.CreatePowerOnCommand((byte)PowerLevel);
                var success = await _bleMonitorService.WriteCharacteristicAwaitAsync(
                    ScalextricProtocol.Characteristics.Command, command);

                if (!success)
                {
                    // Write failed - connection may be lost
                    Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = "Power command failed - connection lost?";
                    });
                    break;
                }

                await Task.Delay(PowerHeartbeatIntervalMs, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation, ignore
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                StatusText = $"Power heartbeat error: {ex.Message}";
            });
        }
    }

    /// <summary>
    /// Disables track power.
    /// </summary>
    public void DisablePower()
    {
        if (!IsGattConnected) return;

        // Stop the heartbeat first
        _powerHeartbeatCts?.Cancel();
        _powerHeartbeatCts = null;

        _ = DisablePowerAsync();
    }

    private async Task DisablePowerAsync()
    {
        StatusText = "Sending power off command...";
        var command = ScalextricProtocol.CommandBuilder.CreatePowerOffCommand();
        var success = await _bleMonitorService.WriteCharacteristicAwaitAsync(ScalextricProtocol.Characteristics.Command, command);

        if (success)
        {
            IsPowerEnabled = false;
            StatusText = "Power disabled";
        }
        else
        {
            IsPowerEnabled = false; // Still mark as disabled
            StatusText = "Failed to send power off command";
        }
    }

    /// <summary>
    /// Toggles track power on/off.
    /// </summary>
    public void TogglePower()
    {
        if (IsPowerEnabled)
            DisablePower();
        else
            EnablePower();
    }

    /// <summary>
    /// Writes linear throttle profiles to all 6 slots sequentially with delays.
    /// Each slot requires 6 blocks of 17 bytes (block index + 16 throttle values).
    /// </summary>
    private async Task<bool> WriteThrottleProfilesAsync()
    {
        if (!IsGattConnected) return false;

        // Get the throttle curve blocks (6 blocks of 17 bytes each)
        var blocks = ScalextricProtocol.ThrottleProfile.CreateLinearBlocks();

        for (int slot = 1; slot <= 6; slot++)
        {
            var uuid = ScalextricProtocol.Characteristics.GetThrottleProfileForSlot(slot);

            // Write all 6 blocks for this slot
            for (int blockIndex = 0; blockIndex < ScalextricProtocol.ThrottleProfile.BlockCount; blockIndex++)
            {
                StatusText = $"Writing throttle profile slot {slot}, block {blockIndex + 1}/6...";

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
    /// Stops the power heartbeat when connection is lost.
    /// </summary>
    private void StopPowerHeartbeat()
    {
        _powerHeartbeatCts?.Cancel();
        _powerHeartbeatCts = null;
        IsPowerEnabled = false;
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
    /// Clears the notification log.
    /// </summary>
    public void ClearNotificationLog()
    {
        NotificationLog.Clear();
    }

    /// <summary>
    /// Called when the notification window is closed.
    /// </summary>
    public void OnNotificationWindowClosed()
    {
        IsNotificationWindowOpen = false;
    }

    /// <summary>
    /// Called when the GATT services window is closed.
    /// </summary>
    public void OnGattServicesWindowClosed()
    {
        IsGattServicesWindowOpen = false;
    }
}

/// <summary>
/// View model for a GATT service.
/// </summary>
public partial class ServiceViewModel : ObservableObject
{
    [ObservableProperty]
    private Guid _uuid;

    [ObservableProperty]
    private string _name = string.Empty;

    public ObservableCollection<CharacteristicViewModel> Characteristics { get; } = [];
}

/// <summary>
/// View model for a GATT characteristic.
/// </summary>
public partial class CharacteristicViewModel : ObservableObject
{
    [ObservableProperty]
    private Guid _uuid;

    [ObservableProperty]
    private Guid _serviceUuid;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _properties = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReadValue))]
    [NotifyPropertyChangedFor(nameof(ReadResultDisplay))]
    private byte[]? _lastReadValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReadValue))]
    [NotifyPropertyChangedFor(nameof(ReadResultDisplay))]
    private string? _lastReadHex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReadResultDisplay))]
    private string? _lastReadText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReadValue))]
    [NotifyPropertyChangedFor(nameof(ReadResultDisplay))]
    private string? _lastReadError;

    public string DisplayText => $"{Name} [{Properties}]";

    public bool IsReadable => Properties.Contains("R");

    public bool HasReadValue => LastReadHex != null || LastReadError != null;

    public string ReadResultDisplay
    {
        get
        {
            if (LastReadError != null) return $"Error: {LastReadError}";
            if (LastReadHex != null)
            {
                return LastReadText != null ? $"{LastReadHex} \"{LastReadText}\"" : LastReadHex;
            }
            return string.Empty;
        }
    }
}

/// <summary>
/// View model for notification data received from a characteristic.
/// </summary>
public partial class NotificationDataViewModel : ObservableObject
{
    [ObservableProperty]
    private DateTime _timestamp;

    [ObservableProperty]
    private string _characteristicName = string.Empty;

    [ObservableProperty]
    private Guid _characteristicUuid;

    [ObservableProperty]
    private byte[] _rawData = [];

    [ObservableProperty]
    private string _hexData = string.Empty;

    [ObservableProperty]
    private string _decodedData = string.Empty;

    public string TimestampText => Timestamp.ToString("HH:mm:ss.fff");

    public string DisplayText => $"[{TimestampText}] {CharacteristicName}: {HexData}";
}

/// <summary>
/// View model for individual controller/slot status.
/// </summary>
public partial class ControllerViewModel : ObservableObject
{
    private bool _previousBrakeState;
    private bool _previousLaneChangeState;

    [ObservableProperty]
    private int _slotNumber;

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

    public void Reset()
    {
        Throttle = 0;
        IsBrakePressed = false;
        IsLaneChangePressed = false;
        BrakeCount = 0;
        LaneChangeCount = 0;
        _previousBrakeState = false;
        _previousLaneChangeState = false;
    }
}

/// <summary>
/// Converts throttle value (0-63) to a scale factor (0.0-1.0) for ScaleTransform.
/// </summary>
public class ThrottleToScaleConverter : IValueConverter
{
    public static readonly ThrottleToScaleConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int throttle)
        {
            // Scale 0-63 to 0.0-1.0
            return throttle / 63.0;
        }
        return 0.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts power enabled state to button text.
/// </summary>
public class PowerButtonTextConverter : IValueConverter
{
    public static readonly PowerButtonTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isEnabled)
        {
            return isEnabled ? "POWER OFF" : "POWER ON";
        }
        return "POWER ON";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts bool to brush for button indicators.
/// </summary>
public class BoolToBrushConverter : IValueConverter
{
    private static readonly ISolidColorBrush BrakeActiveColor = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
    private static readonly ISolidColorBrush BrakeInactiveColor = new SolidColorBrush(Color.FromRgb(183, 28, 28)); // Dark red
    private static readonly ISolidColorBrush LaneChangeActiveColor = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Blue
    private static readonly ISolidColorBrush LaneChangeInactiveColor = new SolidColorBrush(Color.FromRgb(21, 101, 192)); // Dark blue

    public static readonly BoolToBrushConverter BrakeInstance = new(true);
    public static readonly BoolToBrushConverter LaneChangeInstance = new(false);

    private readonly bool _isBrake;

    public BoolToBrushConverter(bool isBrake = true)
    {
        _isBrake = isBrake;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isPressed)
        {
            if (_isBrake)
            {
                return isPressed ? BrakeActiveColor : BrakeInactiveColor;
            }
            else
            {
                return isPressed ? LaneChangeActiveColor : LaneChangeInactiveColor;
            }
        }
        return _isBrake ? BrakeInactiveColor : LaneChangeInactiveColor;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts power enabled state to indicator color.
/// </summary>
public class PowerIndicatorColorConverter : IValueConverter
{
    public static readonly PowerIndicatorColorConverter Instance = new();

    private static readonly Color PowerOnColor = Color.FromRgb(76, 175, 80);   // Green
    private static readonly Color PowerOffColor = Color.FromRgb(158, 158, 158); // Gray

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isPowerOn)
        {
            return isPowerOn ? PowerOnColor : PowerOffColor;
        }
        return PowerOffColor;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
