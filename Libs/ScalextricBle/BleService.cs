using System.Diagnostics;
using Serilog;

#if WINDOWS
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
#endif

namespace ScalextricBle;

/// <summary>
/// Windows BLE service implementation for Scalextric powerbase communication.
/// Handles scanning, GATT connection, characteristic reads/writes, and notifications.
/// </summary>
public class BleService : IBleService
{
    #region Constants

    private const int MaxConnectionAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan DeviceTimeoutDuration = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TimeoutCheckInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan BleOperationTimeout = TimeSpan.FromSeconds(10);

    #endregion

    #region Events

    public event EventHandler<BleConnectionStateEventArgs>? ConnectionStateChanged;
    public event EventHandler<string>? StatusMessageChanged;
    public event EventHandler<BleServicesDiscoveredEventArgs>? ServicesDiscovered;
    public event EventHandler<BleNotificationEventArgs>? NotificationReceived;
    public event EventHandler<BleCharacteristicReadEventArgs>? CharacteristicValueRead;
    public event EventHandler<BleCharacteristicWriteEventArgs>? CharacteristicWriteCompleted;

    #endregion

    #region Properties

    public bool IsScanning { get; private set; }
    public bool IsDeviceDetected => _isDeviceDetected;
    public bool IsGattConnected => _isGattConnected;
    public string? DeviceName => _deviceName;

    #endregion

    #region Fields

    private bool _isDeviceDetected;
    private bool _isGattConnected;
    private string? _deviceName;
    private ulong? _lastBluetoothAddress;
    private readonly Stopwatch _lastSeenStopwatch = new();
    private DateTime? _lastSeenTime;
    private Timer? _timeoutTimer;
    private bool _disposed;
    private bool _isConnecting;
    private readonly object _connectionLock = new();
    private int _connectionAttempts;

#if WINDOWS
    private BluetoothLEAdvertisementWatcher? _watcher;
    private BluetoothLEDevice? _connectedDevice;
    private readonly List<GattDeviceService> _gattServices = [];
    private readonly List<(Guid ServiceUuid, GattCharacteristic Characteristic)> _subscribedCharacteristics = [];
    private readonly Dictionary<Guid, GattCharacteristic> _characteristicCache = [];
#endif

    // Well-known GATT service names
    private static readonly Dictionary<Guid, string> KnownServiceNames = new()
    {
        { Guid.Parse("00001800-0000-1000-8000-00805f9b34fb"), "Generic Access" },
        { Guid.Parse("00001801-0000-1000-8000-00805f9b34fb"), "Generic Attribute" },
        { Guid.Parse("0000180a-0000-1000-8000-00805f9b34fb"), "Device Information" },
        { Guid.Parse("0000180f-0000-1000-8000-00805f9b34fb"), "Battery Service" },
        { Guid.Parse("00001812-0000-1000-8000-00805f9b34fb"), "Human Interface Device" },
    };

    // Well-known GATT characteristic names
    private static readonly Dictionary<Guid, string> KnownCharacteristicNames = new()
    {
        { Guid.Parse("00002a00-0000-1000-8000-00805f9b34fb"), "Device Name" },
        { Guid.Parse("00002a01-0000-1000-8000-00805f9b34fb"), "Appearance" },
        { Guid.Parse("00002a04-0000-1000-8000-00805f9b34fb"), "Peripheral Preferred Connection Parameters" },
        { Guid.Parse("00002a05-0000-1000-8000-00805f9b34fb"), "Service Changed" },
        { Guid.Parse("00002a19-0000-1000-8000-00805f9b34fb"), "Battery Level" },
        { Guid.Parse("00002a29-0000-1000-8000-00805f9b34fb"), "Manufacturer Name" },
        { Guid.Parse("00002a24-0000-1000-8000-00805f9b34fb"), "Model Number" },
        { Guid.Parse("00002a25-0000-1000-8000-00805f9b34fb"), "Serial Number" },
        { Guid.Parse("00002a26-0000-1000-8000-00805f9b34fb"), "Firmware Revision" },
        { Guid.Parse("00002a27-0000-1000-8000-00805f9b34fb"), "Hardware Revision" },
        { Guid.Parse("00002a28-0000-1000-8000-00805f9b34fb"), "Software Revision" },
    };

    #endregion

    #region Public Methods

    public void StartScanning()
    {
        ThrowIfDisposed();
        if (IsScanning) return;

#if WINDOWS
        try
        {
            _watcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };

            _watcher.Received += OnAdvertisementReceived;
            _watcher.Stopped += OnWatcherStopped;

            _watcher.Start();
            IsScanning = true;

            _timeoutTimer = new Timer(CheckDeviceTimeout, null, TimeoutCheckInterval, TimeoutCheckInterval);

            RaiseStatusMessage("Scanning for Scalextric ARC Pro...");
        }
        catch (Exception ex)
        {
            RaiseStatusMessage($"Failed to start scanning: {ex.Message}");
        }
#else
        RaiseStatusMessage("BLE scanning not implemented for this platform.");
#endif
    }

    public void StopScanning()
    {
        ThrowIfDisposed();
        StopScanningInternal();
    }

    public void SubscribeToAllNotifications()
    {
        ThrowIfDisposed();
#if WINDOWS
        if (!_isGattConnected)
        {
            RaiseStatusMessage("Cannot subscribe: not connected.");
            return;
        }
        RunFireAndForget(SubscribeToAllNotificationsAsync, "SubscribeToAllNotifications");
#endif
    }

    public void ReadCharacteristic(Guid serviceUuid, Guid characteristicUuid)
    {
        ThrowIfDisposed();
#if WINDOWS
        if (!_isGattConnected)
        {
            CharacteristicValueRead?.Invoke(this, new BleCharacteristicReadEventArgs
            {
                ServiceUuid = serviceUuid,
                CharacteristicUuid = characteristicUuid,
                Success = false,
                ErrorMessage = "Not connected"
            });
            return;
        }
        RunFireAndForget(
            () => ReadCharacteristicAsync(serviceUuid, characteristicUuid),
            "ReadCharacteristic");
#endif
    }

    public void WriteCharacteristic(Guid characteristicUuid, byte[] data)
    {
        ThrowIfDisposed();
#if WINDOWS
        if (!_isGattConnected)
        {
            CharacteristicWriteCompleted?.Invoke(this, new BleCharacteristicWriteEventArgs
            {
                CharacteristicUuid = characteristicUuid,
                Success = false,
                ErrorMessage = "Not connected"
            });
            return;
        }
        RunFireAndForget(
            () => WriteCharacteristicInternalAsync(characteristicUuid, data),
            "WriteCharacteristic");
#endif
    }

    public async Task<bool> WriteCharacteristicAsync(Guid characteristicUuid, byte[] data)
    {
        ThrowIfDisposed();
#if WINDOWS
        if (!_isGattConnected)
        {
            return false;
        }
        return await WriteCharacteristicInternalAsync(characteristicUuid, data).ConfigureAwait(false);
#else
        await Task.CompletedTask.ConfigureAwait(false);
        return false;
#endif
    }

    #endregion

    #region Private Methods - Scanning

    private void StopScanningInternal()
    {
        if (!IsScanning) return;

#if WINDOWS
        try
        {
            _timeoutTimer?.Dispose();
            _timeoutTimer = null;

            if (_watcher != null)
            {
                _watcher.Received -= OnAdvertisementReceived;
                _watcher.Stopped -= OnWatcherStopped;
                _watcher.Stop();
                _watcher = null;
            }

            IsScanning = false;
            RaiseStatusMessage("Scanning stopped.");
        }
        catch (Exception ex)
        {
            RaiseStatusMessage($"Error stopping scan: {ex.Message}");
        }
#endif
    }

#if WINDOWS
    private void OnAdvertisementReceived(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementReceivedEventArgs args)
    {
        try
        {
            var localName = args.Advertisement.LocalName;

            if (!string.IsNullOrEmpty(localName) &&
                localName.Contains("Scalextric", StringComparison.OrdinalIgnoreCase))
            {
                _lastSeenStopwatch.Restart();
                _lastSeenTime = DateTime.Now;
                _deviceName = localName;
                _lastBluetoothAddress = args.BluetoothAddress;

                if (!_isDeviceDetected)
                {
                    _isDeviceDetected = true;
                    RaiseConnectionStateChanged();
                    RaiseStatusMessage($"Found {localName}. Connecting...");

                    RunFireAndForget(
                        () => ConnectAndDiscoverServicesAsync(args.BluetoothAddress),
                        "AutoConnectOnAdvertisement");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing advertisement");
        }
    }

    private void OnWatcherStopped(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        IsScanning = false;

        if (args.Error != BluetoothError.Success)
        {
            var errorMessage = args.Error switch
            {
                BluetoothError.RadioNotAvailable => "Bluetooth radio not available. Please enable Bluetooth.",
                BluetoothError.ResourceInUse => "Bluetooth resource in use by another application.",
                BluetoothError.DeviceNotConnected => "Bluetooth adapter disconnected.",
                BluetoothError.DisabledByPolicy => "Bluetooth disabled by system policy.",
                BluetoothError.NotSupported => "Bluetooth LE not supported on this device.",
                BluetoothError.DisabledByUser => "Bluetooth disabled by user.",
                BluetoothError.ConsentRequired => "Bluetooth permission required.",
                BluetoothError.TransportNotSupported => "Bluetooth transport not supported.",
                _ => $"Bluetooth error: {args.Error}"
            };

            RaiseStatusMessage(errorMessage);

            if (_isDeviceDetected)
            {
                _isDeviceDetected = false;
                DisconnectInternal(raiseEvents: true);
            }
        }
    }
#endif

    private void CheckDeviceTimeout(object? state)
    {
        if (_isGattConnected) return;

        if (_isDeviceDetected && _lastSeenStopwatch.IsRunning)
        {
            if (_lastSeenStopwatch.Elapsed > DeviceTimeoutDuration)
            {
                _isDeviceDetected = false;
#if WINDOWS
                DisconnectInternal(raiseEvents: false);
#endif
                RaiseConnectionStateChanged();
                RaiseStatusMessage($"Device lost. Last seen: {_lastSeenTime?.ToString("HH:mm:ss") ?? "unknown"}");
            }
        }
    }

    #endregion

    #region Private Methods - Connection

#if WINDOWS
    private async Task ConnectAndDiscoverServicesAsync(ulong bluetoothAddress)
    {
        lock (_connectionLock)
        {
            if (_isConnecting || _isGattConnected) return;
            _isConnecting = true;
            Interlocked.Exchange(ref _connectionAttempts, 0);
        }

        while (Interlocked.CompareExchange(ref _connectionAttempts, 0, 0) < MaxConnectionAttempts && !_isGattConnected && !_disposed)
        {
            var attempt = Interlocked.Increment(ref _connectionAttempts);

            try
            {
                if (attempt > 1)
                {
                    RaiseStatusMessage($"Retrying connection (attempt {attempt}/{MaxConnectionAttempts})...");
                    await Task.Delay(RetryDelay).ConfigureAwait(false);
                }
                else
                {
                    RaiseStatusMessage("Connecting to device...");
                    await Task.Delay(100).ConfigureAwait(false);
                }

                _connectedDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress).AsTask().ConfigureAwait(false);
                if (_connectedDevice == null)
                {
                    Log.Warning("Connection attempt {Attempt}: Device not found", attempt);
                    continue;
                }

                _connectedDevice.ConnectionStatusChanged += OnConnectionStatusChanged;

                RaiseStatusMessage("Discovering GATT services...");
                var gattResult = await WithTimeoutAsync(
                    _connectedDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached).AsTask(),
                    "GetGattServices").ConfigureAwait(false);

                if (gattResult.Status != GattCommunicationStatus.Success)
                {
                    Log.Warning("Connection attempt {Attempt}: GetGattServicesAsync failed: {Status}", attempt, gattResult.Status);
                    DisconnectInternal(raiseEvents: false);
                    continue;
                }

                var discoveredServices = new List<BleServiceInfo>();
                foreach (var service in gattResult.Services)
                {
                    _gattServices.Add(service);

                    var serviceInfo = new BleServiceInfo
                    {
                        Uuid = service.Uuid,
                        Name = GetServiceName(service.Uuid),
                        Characteristics = []
                    };

                    var charsResult = await WithTimeoutAsync(
                        service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask(),
                        "GetCharacteristics").ConfigureAwait(false);
                    if (charsResult.Status == GattCommunicationStatus.Success)
                    {
                        foreach (var characteristic in charsResult.Characteristics)
                        {
                            var charInfo = new BleCharacteristicInfo
                            {
                                Uuid = characteristic.Uuid,
                                Name = GetCharacteristicName(characteristic.Uuid),
                                Properties = FormatCharacteristicProperties(characteristic.CharacteristicProperties)
                            };
                            serviceInfo.Characteristics.Add(charInfo);
                            _characteristicCache[characteristic.Uuid] = characteristic;
                        }
                    }

                    discoveredServices.Add(serviceInfo);
                }

                _isGattConnected = true;
                _isConnecting = false;
                Interlocked.Exchange(ref _connectionAttempts, 0);

                RaiseConnectionStateChanged();
                RaiseStatusMessage($"Connected! Found {discoveredServices.Count} services.");

                ServicesDiscovered?.Invoke(this, new BleServicesDiscoveredEventArgs
                {
                    Services = discoveredServices
                });

                // Auto-subscribe to notifications after connection
                await SubscribeToAllNotificationsAsync().ConfigureAwait(false);

                return;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Connection attempt {Attempt}: Exception", attempt);
                DisconnectInternal(raiseEvents: false);
            }
        }

        _isConnecting = false;
        if (!_isGattConnected)
        {
            RaiseStatusMessage($"Failed to connect after {MaxConnectionAttempts} attempts. Will retry on next advertisement...");
            _isDeviceDetected = false;
            RaiseConnectionStateChanged();
        }
    }

    private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
        {
            if (_isGattConnected)
            {
                DisconnectInternal(raiseEvents: true);
                RaiseStatusMessage("GATT disconnected. Will reconnect on next advertisement...");
            }
        }
    }

    private void DisconnectInternal(bool raiseEvents)
    {
        var wasConnected = _isGattConnected;
        _isGattConnected = false;

        foreach (var (_, characteristic) in _subscribedCharacteristics)
        {
            try
            {
                characteristic.ValueChanged -= OnCharacteristicValueChanged;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error unsubscribing from characteristic");
            }
        }
        _subscribedCharacteristics.Clear();

        _characteristicCache.Clear();

        foreach (var service in _gattServices)
        {
            try
            {
                service.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error disposing GATT service");
            }
        }
        _gattServices.Clear();

        if (_connectedDevice != null)
        {
            try
            {
                _connectedDevice.ConnectionStatusChanged -= OnConnectionStatusChanged;
                _connectedDevice.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error disposing BLE device");
            }
            _connectedDevice = null;
        }

        if (raiseEvents && wasConnected)
        {
            RaiseConnectionStateChanged();
        }
    }
#endif

    #endregion

    #region Private Methods - Characteristics

#if WINDOWS
    private async Task SubscribeToAllNotificationsAsync()
    {
        int subscribed = 0;
        int failed = 0;

        foreach (var service in _gattServices)
        {
            try
            {
                var charsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Cached).AsTask().ConfigureAwait(false);
                if (charsResult.Status != GattCommunicationStatus.Success)
                    continue;

                foreach (var characteristic in charsResult.Characteristics)
                {
                    var props = characteristic.CharacteristicProperties;
                    bool canNotify = props.HasFlag(GattCharacteristicProperties.Notify);
                    bool canIndicate = props.HasFlag(GattCharacteristicProperties.Indicate);

                    if (!canNotify && !canIndicate)
                        continue;

                    try
                    {
                        var cccdValue = canNotify
                            ? GattClientCharacteristicConfigurationDescriptorValue.Notify
                            : GattClientCharacteristicConfigurationDescriptorValue.Indicate;

                        var status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue).AsTask().ConfigureAwait(false);

                        if (status == GattCommunicationStatus.Success)
                        {
                            characteristic.ValueChanged += OnCharacteristicValueChanged;
                            _subscribedCharacteristics.Add((service.Uuid, characteristic));
                            subscribed++;

                            var charName = GetCharacteristicName(characteristic.Uuid);
                            Log.Debug("Subscribed to {CharacteristicName} ({CharacteristicUuid})", charName, characteristic.Uuid);
                        }
                        else
                        {
                            failed++;
                            Log.Warning("Failed to subscribe to {CharacteristicUuid}: {Status}", characteristic.Uuid, status);
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Log.Warning(ex, "Exception subscribing to {CharacteristicUuid}", characteristic.Uuid);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error getting characteristics for service {ServiceUuid}", service.Uuid);
            }
        }

        if (subscribed > 0)
        {
            RaiseStatusMessage($"Subscribed to {subscribed} notification(s)." + (failed > 0 ? $" ({failed} failed)" : ""));
        }
        else if (failed > 0)
        {
            RaiseStatusMessage($"Failed to subscribe to any notifications ({failed} failed).");
        }
        else
        {
            RaiseStatusMessage("No notifiable characteristics found.");
        }
    }

    private void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        try
        {
            using var reader = Windows.Storage.Streams.DataReader.FromBuffer(args.CharacteristicValue);
            var data = new byte[args.CharacteristicValue.Length];
            reader.ReadBytes(data);

            var serviceUuid = _subscribedCharacteristics
                .FirstOrDefault(x => x.Characteristic.Uuid == sender.Uuid)
                .ServiceUuid;

            NotificationReceived?.Invoke(this, new BleNotificationEventArgs
            {
                ServiceUuid = serviceUuid,
                CharacteristicUuid = sender.Uuid,
                CharacteristicName = GetCharacteristicName(sender.Uuid),
                Data = data,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing notification");
        }
    }

    private async Task ReadCharacteristicAsync(Guid serviceUuid, Guid characteristicUuid)
    {
        try
        {
            var service = _gattServices.FirstOrDefault(s => s.Uuid == serviceUuid);
            if (service == null)
            {
                CharacteristicValueRead?.Invoke(this, new BleCharacteristicReadEventArgs
                {
                    ServiceUuid = serviceUuid,
                    CharacteristicUuid = characteristicUuid,
                    Success = false,
                    ErrorMessage = "Service not found"
                });
                return;
            }

            var charsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Cached).AsTask().ConfigureAwait(false);
            if (charsResult.Status != GattCommunicationStatus.Success)
            {
                CharacteristicValueRead?.Invoke(this, new BleCharacteristicReadEventArgs
                {
                    ServiceUuid = serviceUuid,
                    CharacteristicUuid = characteristicUuid,
                    Success = false,
                    ErrorMessage = $"Failed to get characteristics: {charsResult.Status}"
                });
                return;
            }

            var characteristic = charsResult.Characteristics.FirstOrDefault(c => c.Uuid == characteristicUuid);
            if (characteristic == null)
            {
                CharacteristicValueRead?.Invoke(this, new BleCharacteristicReadEventArgs
                {
                    ServiceUuid = serviceUuid,
                    CharacteristicUuid = characteristicUuid,
                    Success = false,
                    ErrorMessage = "Characteristic not found"
                });
                return;
            }

            if (!characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read))
            {
                CharacteristicValueRead?.Invoke(this, new BleCharacteristicReadEventArgs
                {
                    ServiceUuid = serviceUuid,
                    CharacteristicUuid = characteristicUuid,
                    Success = false,
                    ErrorMessage = "Characteristic is not readable"
                });
                return;
            }

            var readResult = await WithTimeoutAsync(
                characteristic.ReadValueAsync(BluetoothCacheMode.Uncached).AsTask(),
                "ReadValue").ConfigureAwait(false);
            if (readResult.Status != GattCommunicationStatus.Success)
            {
                CharacteristicValueRead?.Invoke(this, new BleCharacteristicReadEventArgs
                {
                    ServiceUuid = serviceUuid,
                    CharacteristicUuid = characteristicUuid,
                    Success = false,
                    ErrorMessage = $"Read failed: {readResult.Status}"
                });
                return;
            }

            using var reader = Windows.Storage.Streams.DataReader.FromBuffer(readResult.Value);
            var data = new byte[readResult.Value.Length];
            reader.ReadBytes(data);

            CharacteristicValueRead?.Invoke(this, new BleCharacteristicReadEventArgs
            {
                ServiceUuid = serviceUuid,
                CharacteristicUuid = characteristicUuid,
                CharacteristicName = GetCharacteristicName(characteristicUuid),
                Data = data,
                Success = true
            });
        }
        catch (Exception ex)
        {
            CharacteristicValueRead?.Invoke(this, new BleCharacteristicReadEventArgs
            {
                ServiceUuid = serviceUuid,
                CharacteristicUuid = characteristicUuid,
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    private async Task<bool> WriteCharacteristicInternalAsync(Guid characteristicUuid, byte[] data)
    {
        try
        {
            if (!_characteristicCache.TryGetValue(characteristicUuid, out var targetCharacteristic))
            {
                CharacteristicWriteCompleted?.Invoke(this, new BleCharacteristicWriteEventArgs
                {
                    CharacteristicUuid = characteristicUuid,
                    Success = false,
                    ErrorMessage = "Characteristic not found"
                });
                return false;
            }

            var props = targetCharacteristic.CharacteristicProperties;
            bool canWrite = props.HasFlag(GattCharacteristicProperties.Write) ||
                           props.HasFlag(GattCharacteristicProperties.WriteWithoutResponse);

            if (!canWrite)
            {
                CharacteristicWriteCompleted?.Invoke(this, new BleCharacteristicWriteEventArgs
                {
                    CharacteristicUuid = characteristicUuid,
                    Success = false,
                    ErrorMessage = "Characteristic is not writable"
                });
                return false;
            }

            using var writer = new Windows.Storage.Streams.DataWriter();
            writer.WriteBytes(data);
            var buffer = writer.DetachBuffer();

            var writeOption = props.HasFlag(GattCharacteristicProperties.Write)
                ? GattWriteOption.WriteWithResponse
                : GattWriteOption.WriteWithoutResponse;

            var status = await WithTimeoutAsync(
                targetCharacteristic.WriteValueAsync(buffer, writeOption).AsTask(),
                "WriteValue").ConfigureAwait(false);

            var success = status == GattCommunicationStatus.Success;
            CharacteristicWriteCompleted?.Invoke(this, new BleCharacteristicWriteEventArgs
            {
                CharacteristicUuid = characteristicUuid,
                Success = success,
                ErrorMessage = success ? null : $"Write failed: {status}"
            });
            return success;
        }
        catch (Exception ex)
        {
            CharacteristicWriteCompleted?.Invoke(this, new BleCharacteristicWriteEventArgs
            {
                CharacteristicUuid = characteristicUuid,
                Success = false,
                ErrorMessage = ex.Message
            });
            return false;
        }
    }
#endif

    #endregion

    #region Private Methods - Helpers

    private void RaiseConnectionStateChanged()
    {
        ConnectionStateChanged?.Invoke(this, new BleConnectionStateEventArgs
        {
            IsDeviceDetected = _isDeviceDetected,
            IsGattConnected = _isGattConnected,
            DeviceName = _deviceName,
            BluetoothAddress = _lastBluetoothAddress,
            LastSeen = _lastSeenTime
        });
    }

    private void RaiseStatusMessage(string message)
    {
        StatusMessageChanged?.Invoke(this, message);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

#if WINDOWS
    private static async Task<T> WithTimeoutAsync<T>(Task<T> task, string operationName)
    {
        using var cts = new CancellationTokenSource(BleOperationTimeout);
        try
        {
            return await task.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"BLE operation '{operationName}' timed out after {BleOperationTimeout.TotalSeconds} seconds");
        }
    }

    private static string GetServiceName(Guid uuid)
    {
        return KnownServiceNames.TryGetValue(uuid, out var name) ? name : uuid.ToString();
    }

    private static string GetCharacteristicName(Guid uuid)
    {
        return KnownCharacteristicNames.TryGetValue(uuid, out var name) ? name : uuid.ToString();
    }

    private static string FormatCharacteristicProperties(GattCharacteristicProperties props)
    {
        var parts = new List<string>();
        if (props.HasFlag(GattCharacteristicProperties.Read)) parts.Add("R");
        if (props.HasFlag(GattCharacteristicProperties.Write)) parts.Add("W");
        if (props.HasFlag(GattCharacteristicProperties.WriteWithoutResponse)) parts.Add("WnR");
        if (props.HasFlag(GattCharacteristicProperties.Notify)) parts.Add("N");
        if (props.HasFlag(GattCharacteristicProperties.Indicate)) parts.Add("I");
        return string.Join(", ", parts);
    }
#endif

    private void RunFireAndForget(Func<Task> asyncAction, string operationName)
    {
        Task.Run(async () =>
        {
            try
            {
                await asyncAction().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in {OperationName}", operationName);
                RaiseStatusMessage($"Error: {ex.Message}");
            }
        });
    }

    #endregion

    #region IDisposable

    ~BleService()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        StopScanningInternal();

        _disposed = true;

        if (disposing)
        {
#if WINDOWS
            DisconnectInternal(raiseEvents: false);
#endif
        }
    }

    #endregion
}
