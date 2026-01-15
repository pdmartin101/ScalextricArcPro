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
    private readonly AppSettings _settings;
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
            Controllers.Add(new ControllerViewModel { SlotNumber = i + 1, PowerLevel = powerLevel, IsGhostMode = isGhostMode });
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

        // Load persisted settings
        _settings = AppSettings.Load();
        _powerLevel = _settings.PowerLevel;
        _usePerSlotPower = _settings.UsePerSlotPower;

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
            var wasGattConnected = IsGattConnected;
            IsConnected = e.IsConnected;
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
                StopPowerHeartbeat();
            }

            if (!e.IsConnected)
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

        // First, send a PowerOnRacing command with all slots at power 0 and ghost mode OFF
        // This clears any latched ghost mode state from a previous session
        var clearGhostCommand = BuildClearGhostCommand();
        for (int i = 0; i < 3; i++)
        {
            await _bleMonitorService.WriteCharacteristicAwaitAsync(ScalextricProtocol.Characteristics.Command, clearGhostCommand);
            await Task.Delay(BleWriteDelayMs);
        }

        // Now send the actual power-off command
        var powerOffCommand = BuildPowerOffCommand();
        for (int i = 0; i < 3; i++)
        {
            await _bleMonitorService.WriteCharacteristicAwaitAsync(ScalextricProtocol.Characteristics.Command, powerOffCommand);
            await Task.Delay(BleWriteDelayMs);
        }
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
            // Process Slot characteristic (0x3b0b) for lap counting
            // Slot notifications are sent when a car passes over the finish line sensor
            else if (e.CharacteristicUuid == ScalextricProtocol.Characteristics.Slot)
            {
                ProcessSlotSensorData(e.Data);
            }

            // Skip adding to log if paused
            if (IsNotificationLogPaused)
                return;

            // Create the notification entry
            var entry = new NotificationDataViewModel
            {
                Timestamp = e.Timestamp,
                CharacteristicName = e.CharacteristicName ?? e.CharacteristicUuid.ToString(),
                CharacteristicUuid = e.CharacteristicUuid,
                RawData = e.Data,
                HexData = BitConverter.ToString(e.Data).Replace("-", " "),
                DecodedData = DecodeScalextricData(e.CharacteristicUuid, e.Data)
            };

            // Add to main log
            NotificationLog.Insert(0, entry);

            // Keep the log from growing too large
            while (NotificationLog.Count > MaxNotificationLogEntries)
            {
                NotificationLog.RemoveAt(NotificationLog.Count - 1);
            }

            // Add to filtered log if it passes the filter
            if (PassesCharacteristicFilter(e.CharacteristicUuid))
            {
                FilteredNotificationLog.Insert(0, entry);
                while (FilteredNotificationLog.Count > MaxNotificationLogEntries)
                {
                    FilteredNotificationLog.RemoveAt(FilteredNotificationLog.Count - 1);
                }
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
        for (int i = 1; i < data.Length && (i - 1) < Controllers.Count; i++)
        {
            Controllers[i - 1].UpdateFromByte(data[i]);
        }
    }

    private void ProcessSlotSensorData(byte[] data)
    {
        // Slot characteristic (0x3b0b) notification format (20 bytes):
        // data[0] = status/counter byte (changes on events)
        // data[1] = slot index (1-6)
        // data[2-5] = t1: Lane 1 entry timestamp (32-bit little-endian, centiseconds)
        // data[6-9] = t2: Lane 2 entry timestamp (32-bit little-endian, centiseconds)
        // data[10-13] = t3: Lane 1 exit timestamp (t3 > t1 by a few tenths)
        // data[14-17] = t4: Lane 2 exit timestamp (t4 > t2 by a few tenths)
        // data[18-19] = additional data
        //
        // t1/t3 are a pair (lane 1 entry/exit), t2/t4 are a pair (lane 2 entry/exit).
        // For lap timing, we use the entry timestamps t1 and t2.
        if (data.Length >= 10)
        {
            int slotId = data[1];

            // Extract Lane 1 entry timestamp from bytes 2-5 (t1)
            uint lane1Timestamp = (uint)(data[2] | (data[3] << 8) | (data[4] << 16) | (data[5] << 24));

            // Extract Lane 2 entry timestamp from bytes 6-9 (t2)
            uint lane2Timestamp = (uint)(data[6] | (data[7] << 8) | (data[8] << 16) | (data[9] << 24));

            // Valid slot IDs are 1-6
            if (slotId >= 1 && slotId <= MaxControllers)
            {
                // Update the controller with both lane timestamps
                Controllers[slotId - 1].UpdateFinishLineTimestamps(lane1Timestamp, lane2Timestamp);
            }
        }
    }

    private static string DecodeScalextricData(Guid characteristicUuid, byte[] data)
    {
        if (data.Length == 0) return "(empty)";

        // Decode based on characteristic type
        if (characteristicUuid == ScalextricProtocol.Characteristics.Slot)
        {
            return DecodeSlotData(data);
        }
        else if (characteristicUuid == ScalextricProtocol.Characteristics.Throttle)
        {
            return DecodeThrottleData(data);
        }
        else if (characteristicUuid == ScalextricProtocol.Characteristics.Track)
        {
            return DecodeTrackData(data);
        }

        // Generic decode for unknown characteristics
        return DecodeGenericData(data);
    }

    private static string DecodeSlotData(byte[] data)
    {
        if (data.Length < 18) return $"(incomplete: {data.Length} bytes)";

        var parts = new System.Collections.Generic.List<string>();

        // Status byte and Slot ID
        parts.Add($"St:{data[0]}");
        int slotId = data[1];
        parts.Add($"Slot:{slotId}");

        // t1: Lane 1 entry timestamp (bytes 2-5, centiseconds)
        uint t1 = (uint)(data[2] | (data[3] << 8) | (data[4] << 16) | (data[5] << 24));
        double t1Seconds = t1 / 100.0;
        parts.Add($"t1:{t1}({t1Seconds:F2}s)");

        // t2: Lane 2 entry timestamp (bytes 6-9, centiseconds)
        uint t2 = (uint)(data[6] | (data[7] << 8) | (data[8] << 16) | (data[9] << 24));
        double t2Seconds = t2 / 100.0;
        parts.Add($"t2:{t2}({t2Seconds:F2}s)");

        // t3: Lane 1 exit timestamp (bytes 10-13, centiseconds) - t3 > t1 by a few tenths
        uint t3 = (uint)(data[10] | (data[11] << 8) | (data[12] << 16) | (data[13] << 24));
        double t3Seconds = t3 / 100.0;
        parts.Add($"t3:{t3}({t3Seconds:F2}s)");

        // t4: Lane 2 exit timestamp (bytes 14-17, centiseconds) - t4 > t2 by a few tenths
        uint t4 = (uint)(data[14] | (data[15] << 8) | (data[16] << 16) | (data[17] << 24));
        double t4Seconds = t4 / 100.0;
        parts.Add($"t4:{t4}({t4Seconds:F2}s)");

        return string.Join(" | ", parts);
    }

    private static string DecodeThrottleData(byte[] data)
    {
        var parts = new System.Collections.Generic.List<string>();

        // First byte is header
        if (data.Length >= 1)
            parts.Add($"H:{data[0]:X2}");

        // Remaining bytes are controller data
        for (int i = 1; i < data.Length && i <= 6; i++)
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

    private static string DecodeTrackData(byte[] data)
    {
        // Track sensor data - show raw byte values for debugging
        var parts = new System.Collections.Generic.List<string>();
        for (int i = 0; i < Math.Min(data.Length, 8); i++)
        {
            parts.Add($"b{i}:{data[i]}");
        }
        if (data.Length > 8)
            parts.Add($"+{data.Length - 8}more");
        return string.Join(" | ", parts);
    }

    private static string DecodeGenericData(byte[] data)
    {
        if (data.Length >= 2)
        {
            var parts = new System.Collections.Generic.List<string>();
            parts.Add($"H:{data[0]:X2}");
            for (int i = 1; i < Math.Min(data.Length, 7); i++)
            {
                parts.Add($"b{i}:{data[i]}");
            }
            if (data.Length > 7)
                parts.Add($"+{data.Length - 7}more");
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
        }
        _settings.Save();

        // Stop power heartbeat
        _powerHeartbeatCts?.Cancel();
        _powerHeartbeatCts?.Dispose();
        _powerHeartbeatCts = null;

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
            // Build commands upfront
            var clearGhostCommand = BuildClearGhostCommand();
            var powerOffCommand = BuildPowerOffCommand();

            // Use a short timeout CTS for each write to avoid blocking
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            // Best-effort: send one clear ghost and one power-off command
            // We don't wait for responses - just fire and let the BLE service handle it
            // This is acceptable during shutdown since we're disposing anyway
            _bleMonitorService.WriteCharacteristicAwaitAsync(
                ScalextricProtocol.Characteristics.Command, clearGhostCommand)
                .Wait(100); // Very short wait, just enough to queue the write

            _bleMonitorService.WriteCharacteristicAwaitAsync(
                ScalextricProtocol.Characteristics.Command, powerOffCommand)
                .Wait(100);
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

    // Interval for sending power heartbeat commands (ms)
    private const int PowerHeartbeatIntervalMs = 200;

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

        // Start the power heartbeat
        IsPowerEnabled = true;
        StatusText = $"Power enabled at level {PowerLevel}";

        // Cancel any existing heartbeat
        _powerHeartbeatCts?.Cancel();
        _powerHeartbeatCts = new CancellationTokenSource();

        // Start continuous power command sending
        var token = _powerHeartbeatCts.Token;
        RunFireAndForget(() => PowerHeartbeatLoopAsync(token), "PowerHeartbeatLoop");
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
                var command = BuildPowerCommand();
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
    /// Builds a power command using per-controller power levels and ghost mode settings.
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

            // Set power level (either per-slot or global)
            slot.PowerMultiplier = (byte)(UsePerSlotPower ? controller.PowerLevel : PowerLevel);

            // Set ghost mode flag - in ghost mode, PowerMultiplier becomes direct throttle index
            slot.GhostMode = controller.IsGhostMode;
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
        _powerHeartbeatCts?.Cancel();
        _powerHeartbeatCts = null;

        RunFireAndForget(DisablePowerAsync, "DisablePower");
    }

    private async Task DisablePowerAsync()
    {
        StatusText = "Sending power off command...";

        // First, send a PowerOnRacing command with all slots at power 0 and ghost mode OFF
        // This clears the ghost mode state in the powerbase before we cut power
        var clearGhostCommand = BuildClearGhostCommand();
        for (int i = 0; i < 3; i++)
        {
            await _bleMonitorService.WriteCharacteristicAwaitAsync(ScalextricProtocol.Characteristics.Command, clearGhostCommand);
            await Task.Delay(BleWriteDelayMs);
        }

        // Now send the actual power-off command
        var powerOffCommand = BuildPowerOffCommand();
        for (int i = 0; i < 3; i++)
        {
            await _bleMonitorService.WriteCharacteristicAwaitAsync(ScalextricProtocol.Characteristics.Command, powerOffCommand);
            await Task.Delay(BleWriteDelayMs);
        }

        IsPowerEnabled = false;
        StatusText = "Power disabled";
    }

    /// <summary>
    /// Builds a command that keeps power on but clears ghost mode on all slots with power 0.
    /// This is used to transition out of ghost mode before cutting power.
    /// </summary>
    private static byte[] BuildClearGhostCommand()
    {
        var builder = new ScalextricProtocol.CommandBuilder
        {
            Type = ScalextricProtocol.CommandType.PowerOnRacing
        };

        // Set all slots to power 0 with ghost mode disabled
        for (int i = 1; i <= 6; i++)
        {
            var slot = builder.GetSlot(i);
            slot.PowerMultiplier = 0;
            slot.GhostMode = false;
        }

        return builder.Build();
    }

    /// <summary>
    /// Builds a power-off command that clears ghost mode on all slots.
    /// </summary>
    private static byte[] BuildPowerOffCommand()
    {
        var builder = new ScalextricProtocol.CommandBuilder
        {
            Type = ScalextricProtocol.CommandType.NoPowerTimerStopped
        };

        // Ensure all slots have power 0 and ghost mode disabled
        for (int i = 1; i <= 6; i++)
        {
            var slot = builder.GetSlot(i);
            slot.PowerMultiplier = 0;
            slot.GhostMode = false;
        }

        return builder.Build();
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
                System.Diagnostics.Debug.WriteLine($"Error in {operationName}: {ex.Message}");
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
    /// Clears the notification log.
    /// </summary>
    public void ClearNotificationLog()
    {
        NotificationLog.Clear();
        FilteredNotificationLog.Clear();
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
