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
    private readonly AppSettings _settings;
    private IWindowService? _windowService;
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

    /// <summary>
    /// Creates a MainViewModel with default services. Used for design-time and simple instantiation.
    /// </summary>
    public MainViewModel() : this(new BleMonitorService(), AppSettings.Load())
    {
    }

    /// <summary>
    /// Creates a MainViewModel with injected dependencies.
    /// </summary>
    /// <param name="bleMonitorService">The BLE monitoring service.</param>
    /// <param name="settings">The application settings.</param>
    public MainViewModel(IBleMonitorService bleMonitorService, AppSettings settings)
    {
        _bleMonitorService = bleMonitorService;
        _settings = settings;

        _bleMonitorService.ConnectionStateChanged += OnConnectionStateChanged;
        _bleMonitorService.StatusMessageChanged += OnStatusMessageChanged;
        _bleMonitorService.ServicesDiscovered += OnServicesDiscovered;
        _bleMonitorService.NotificationReceived += OnNotificationReceived;
        _bleMonitorService.CharacteristicValueRead += OnCharacteristicValueRead;

        // Initialize from persisted settings
        _powerLevel = _settings.PowerLevel;
        _usePerSlotPower = _settings.UsePerSlotPower;

        InitializeControllers();

        // Start notification batch timer to reduce UI dispatcher load
        _notificationBatchTimer = new Timer(
            FlushNotificationBatch,
            null,
            NotificationBatchIntervalMs,
            NotificationBatchIntervalMs);
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

        await SendPowerOffSequenceAsync();
    }

    /// <summary>
    /// Sends the power-off sequence: clear ghost commands followed by power-off commands.
    /// This shared method is used by both initial power-off and disable power operations.
    /// </summary>
    private async Task SendPowerOffSequenceAsync()
    {
        // First, send PowerOnRacing commands with all slots at power 0 and ghost mode OFF
        // This clears any latched ghost mode state from a previous session
        var clearGhostCommand = BuildClearGhostCommand();
        for (int i = 0; i < 3; i++)
        {
            await _bleMonitorService.WriteCharacteristicAwaitAsync(ScalextricProtocol.Characteristics.Command, clearGhostCommand);
            await Task.Delay(BleWriteDelayMs);
        }

        // Now send the actual power-off commands
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
        for (int i = 1; i < data.Length && (i - 1) < Controllers.Count; i++)
        {
            Controllers[i - 1].UpdateFromByte(data[i]);
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

            // Valid slot IDs are 1-6
            if (slotId >= 1 && slotId <= MaxControllers)
            {
                // Update the controller with both lane timestamps
                Controllers[slotId - 1].UpdateFinishLineTimestamps(lane1Timestamp, lane2Timestamp);
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
        }
        _settings.Save();

        // Stop power heartbeat
        _powerHeartbeatCts?.Cancel();
        _powerHeartbeatCts?.Dispose();
        _powerHeartbeatCts = null;

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

        await SendPowerOffSequenceAsync();

        IsPowerEnabled = false;
        StatusText = "Power disabled";
    }

    /// <summary>
    /// Builds a command that keeps power on but clears ghost mode on all slots with power 0.
    /// This is used to transition out of ghost mode before cutting power.
    /// </summary>
    private static byte[] BuildClearGhostCommand()
    {
        return BuildCommandWithAllSlotsZeroed(ScalextricProtocol.CommandType.PowerOnRacing);
    }

    /// <summary>
    /// Builds a power-off command that clears ghost mode on all slots.
    /// </summary>
    private static byte[] BuildPowerOffCommand()
    {
        return BuildCommandWithAllSlotsZeroed(ScalextricProtocol.CommandType.NoPowerTimerStopped);
    }

    /// <summary>
    /// Helper method to build a command with all slots set to power 0 and ghost mode disabled.
    /// Reduces duplication between BuildClearGhostCommand and BuildPowerOffCommand.
    /// </summary>
    private static byte[] BuildCommandWithAllSlotsZeroed(ScalextricProtocol.CommandType commandType)
    {
        var builder = new ScalextricProtocol.CommandBuilder
        {
            Type = commandType
        };

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
    [RelayCommand]
    private void TogglePower()
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
    /// Sets the window service for managing child windows.
    /// </summary>
    /// <param name="windowService">The window service instance.</param>
    public void SetWindowService(IWindowService windowService)
    {
        _windowService = windowService;
        _windowService.GattServicesWindowClosed += (_, _) => IsGattServicesWindowOpen = false;
        _windowService.NotificationWindowClosed += (_, _) => IsNotificationWindowOpen = false;
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
    /// Clears the notification log.
    /// </summary>
    [RelayCommand]
    private void ClearNotificationLog()
    {
        NotificationLog.Clear();
        FilteredNotificationLog.Clear();
    }
}
