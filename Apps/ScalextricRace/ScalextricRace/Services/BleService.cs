#if WINDOWS
using System.Diagnostics;
using Serilog;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace ScalextricRace.Services;

/// <summary>
/// Windows implementation of the BLE service for Scalextric powerbases.
/// Handles BLE scanning, GATT connection, and characteristic operations.
/// </summary>
public class BleService : IBleService
{
    #region Constants

    /// <summary>
    /// Maximum number of GATT connection retry attempts.
    /// </summary>
    private const int MaxConnectionAttempts = 3;

    /// <summary>
    /// Delay between connection retry attempts.
    /// </summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Timeout for device detection (device considered lost if no advertisement received).
    /// </summary>
    private static readonly TimeSpan DeviceTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Interval for checking device timeout.
    /// </summary>
    private static readonly TimeSpan TimeoutCheckInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Timeout for BLE async operations to prevent indefinite hangs.
    /// </summary>
    private static readonly TimeSpan BleOperationTimeout = TimeSpan.FromSeconds(10);

    #endregion

    #region Fields

    private BluetoothLEAdvertisementWatcher? _watcher;
    private BluetoothLEDevice? _connectedDevice;
    private readonly List<GattDeviceService> _gattServices = new();
    private readonly Dictionary<Guid, GattCharacteristic> _characteristicCache = new();
    private readonly List<GattCharacteristic> _subscribedCharacteristics = new();

    private readonly Stopwatch _lastSeenStopwatch = new();
    private Timer? _timeoutTimer;
    private ulong _lastBluetoothAddress;

    private bool _isDeviceDetected;
    private bool _isGattConnected;
    private string? _deviceName;

    private readonly object _connectionLock = new();
    private bool _isConnecting;
    private bool _disposed;

    #endregion

    #region Events

    /// <inheritdoc />
    public event EventHandler<BleConnectionStateEventArgs>? ConnectionStateChanged;

    /// <inheritdoc />
    public event EventHandler<BleNotificationEventArgs>? NotificationReceived;

    /// <inheritdoc />
    public event EventHandler<string>? StatusMessageChanged;

    #endregion

    #region Properties

    /// <inheritdoc />
    public bool IsScanning { get; private set; }

    /// <inheritdoc />
    public bool IsDeviceDetected => _isDeviceDetected;

    /// <inheritdoc />
    public bool IsGattConnected => _isGattConnected;

    /// <inheritdoc />
    public string? DeviceName => _deviceName;

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public void StartScanning()
    {
        ThrowIfDisposed();
        if (IsScanning) return;

        try
        {
            // Create and configure the BLE advertisement watcher
            _watcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };

            _watcher.Received += OnAdvertisementReceived;
            _watcher.Stopped += OnWatcherStopped;

            _watcher.Start();
            IsScanning = true;

            // Start timeout checker
            _timeoutTimer = new Timer(
                CheckDeviceTimeout,
                null,
                TimeoutCheckInterval,
                TimeoutCheckInterval);

            RaiseStatusMessage("Scanning for Scalextric devices...");
        }
        catch (Exception ex)
        {
            RaiseStatusMessage($"Failed to start scanning: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public void StopScanning()
    {
        ThrowIfDisposed();
        StopScanningInternal();
    }

    /// <summary>
    /// Internal method to stop scanning without throwing if disposed.
    /// </summary>
    private void StopScanningInternal()
    {
        if (!IsScanning) return;

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
    }

    /// <inheritdoc />
    public async Task<bool> WriteCharacteristicAsync(Guid characteristicUuid, byte[] data)
    {
        ThrowIfDisposed();

        if (!_isGattConnected)
            return false;

        if (!_characteristicCache.TryGetValue(characteristicUuid, out var characteristic))
            return false;

        try
        {
            // Check if writable
            var props = characteristic.CharacteristicProperties;
            bool canWrite = props.HasFlag(GattCharacteristicProperties.Write) ||
                           props.HasFlag(GattCharacteristicProperties.WriteWithoutResponse);

            if (!canWrite)
            {
                Log.Warning("Characteristic {CharacteristicUuid} is not writable", characteristicUuid);
                return false;
            }

            // Create buffer and write
            using var writer = new Windows.Storage.Streams.DataWriter();
            writer.WriteBytes(data);
            var buffer = writer.DetachBuffer();

            var writeOption = props.HasFlag(GattCharacteristicProperties.Write)
                ? GattWriteOption.WriteWithResponse
                : GattWriteOption.WriteWithoutResponse;

            var status = await WithTimeoutAsync(
                characteristic.WriteValueAsync(buffer, writeOption).AsTask(),
                "WriteValue").ConfigureAwait(false);

            return status == GattCommunicationStatus.Success;
        }
        catch (TimeoutException ex)
        {
            Log.Warning(ex, "Write timeout");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Write failed");
            return false;
        }
    }

    /// <summary>
    /// Finalizer to ensure resources are cleaned up if Dispose is not called.
    /// </summary>
    ~BleService()
    {
        Dispose(disposing: false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        // Stop scanning before setting disposed flag to avoid ThrowIfDisposed
        StopScanningInternal();

        _disposed = true;

        if (disposing)
        {
            // Dispose managed resources
            DisconnectAndCleanup();
        }
    }

    #endregion

    #region Private Methods - Scanning

    /// <summary>
    /// Handles received BLE advertisements.
    /// </summary>
    private void OnAdvertisementReceived(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementReceivedEventArgs args)
    {
        try
        {
            var localName = args.Advertisement.LocalName;

            // Check if this is a Scalextric device
            if (string.IsNullOrEmpty(localName) ||
                !localName.Contains("Scalextric", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Update last seen timestamp
            _lastSeenStopwatch.Restart();
            _lastBluetoothAddress = args.BluetoothAddress;

            if (!_isDeviceDetected)
            {
                _isDeviceDetected = true;
                _deviceName = localName;
                RaiseConnectionStateChanged();
                RaiseStatusMessage($"Found {localName}. Connecting...");

                // Auto-connect when device is found using proper fire-and-forget pattern
                RunFireAndForget(
                    () => ConnectAndDiscoverServicesAsync(args.BluetoothAddress),
                    "AutoConnectOnAdvertisement");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing advertisement");
        }
    }

    /// <summary>
    /// Handles watcher stopped event.
    /// </summary>
    private void OnWatcherStopped(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        IsScanning = false;

        if (args.Error != BluetoothError.Success)
        {
            // Provide user-friendly error messages
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

            // On error, clean up any partial connection state
            if (_isDeviceDetected)
            {
                _isDeviceDetected = false;
                DisconnectAndCleanup();
                RaiseConnectionStateChanged();
            }
        }
    }

    /// <summary>
    /// Checks if the device has timed out (no advertisements received).
    /// </summary>
    private void CheckDeviceTimeout(object? state)
    {
        // Don't timeout if we have a GATT connection
        if (_isGattConnected) return;

        if (_isDeviceDetected && _lastSeenStopwatch.Elapsed > DeviceTimeout)
        {
            _isDeviceDetected = false;
            _deviceName = null;
            RaiseConnectionStateChanged();
            RaiseStatusMessage("Device lost. Scanning...");
        }
    }

    #endregion

    #region Private Methods - Connection

    /// <summary>
    /// Connects to the device and discovers GATT services.
    /// </summary>
    private async Task ConnectAndDiscoverServicesAsync(ulong bluetoothAddress)
    {
        lock (_connectionLock)
        {
            if (_isConnecting || _isGattConnected) return;
            _isConnecting = true;
        }

        try
        {
            int attempt = 0;

            while (attempt < MaxConnectionAttempts && !_isGattConnected && !_disposed)
            {
                attempt++;

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
                        // Small delay on first attempt to let BLE stack settle
                        await Task.Delay(100).ConfigureAwait(false);
                    }

                    // Get the BLE device
                    _connectedDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress).AsTask().ConfigureAwait(false);

                    if (_connectedDevice == null)
                    {
                        Log.Warning("Connection attempt {Attempt}: Device not found", attempt);
                        continue;
                    }

                    // Subscribe to connection status changes
                    _connectedDevice.ConnectionStatusChanged += OnConnectionStatusChanged;

                    // Discover GATT services - this establishes the actual connection
                    RaiseStatusMessage("Discovering GATT services...");
                    var gattResult = await WithTimeoutAsync(
                        _connectedDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached).AsTask(),
                        "GetGattServices").ConfigureAwait(false);

                    if (gattResult.Status != GattCommunicationStatus.Success)
                    {
                        Log.Warning("Connection attempt {Attempt}: GetGattServicesAsync failed: {Status}", attempt, gattResult.Status);
                        DisconnectAndCleanup();
                        continue;
                    }

                    // Cache services and characteristics
                    foreach (var service in gattResult.Services)
                    {
                        _gattServices.Add(service);

                        var charsResult = await WithTimeoutAsync(
                            service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask(),
                            "GetCharacteristics").ConfigureAwait(false);
                        if (charsResult.Status == GattCommunicationStatus.Success)
                        {
                            foreach (var characteristic in charsResult.Characteristics)
                            {
                                _characteristicCache[characteristic.Uuid] = characteristic;
                            }
                        }
                    }

                    _isGattConnected = true;
                    RaiseConnectionStateChanged();
                    RaiseStatusMessage($"Connected to {_deviceName}! Found {_gattServices.Count} services.");

                    // Subscribe to notifications
                    await SubscribeToNotificationsAsync().ConfigureAwait(false);
                    return; // Exit the retry loop on success
                }
                catch (TimeoutException ex)
                {
                    Log.Warning(ex, "Connection attempt {Attempt}: Timeout", attempt);
                    DisconnectAndCleanup();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Connection attempt {Attempt}: Exception", attempt);
                    DisconnectAndCleanup();
                }
            }

            if (!_isGattConnected)
            {
                RaiseStatusMessage($"Failed to connect after {MaxConnectionAttempts} attempts. Will retry on next advertisement...");
                // Reset device detected so next advertisement triggers a new connection attempt
                _isDeviceDetected = false;
                RaiseConnectionStateChanged();
            }
        }
        finally
        {
            lock (_connectionLock)
            {
                _isConnecting = false;
            }
        }
    }

    /// <summary>
    /// Subscribes to all notification characteristics.
    /// </summary>
    private async Task SubscribeToNotificationsAsync()
    {
        int subscribed = 0;
        int failed = 0;

        foreach (var kvp in _characteristicCache)
        {
            var characteristic = kvp.Value;
            var props = characteristic.CharacteristicProperties;

            bool canNotify = props.HasFlag(GattCharacteristicProperties.Notify);
            bool canIndicate = props.HasFlag(GattCharacteristicProperties.Indicate);

            if (!canNotify && !canIndicate) continue;

            try
            {
                var cccdValue = canNotify
                    ? GattClientCharacteristicConfigurationDescriptorValue.Notify
                    : GattClientCharacteristicConfigurationDescriptorValue.Indicate;

                var status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue)
                    .AsTask().ConfigureAwait(false);

                if (status == GattCommunicationStatus.Success)
                {
                    characteristic.ValueChanged += OnCharacteristicValueChanged;
                    _subscribedCharacteristics.Add(characteristic);
                    subscribed++;
                    Log.Debug("Subscribed to {CharacteristicUuid}", characteristic.Uuid);
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

    /// <summary>
    /// Handles characteristic value changes (notifications).
    /// </summary>
    private void OnCharacteristicValueChanged(
        GattCharacteristic sender,
        GattValueChangedEventArgs args)
    {
        try
        {
            using var reader = Windows.Storage.Streams.DataReader.FromBuffer(args.CharacteristicValue);
            var data = new byte[args.CharacteristicValue.Length];
            reader.ReadBytes(data);

            // Find the service UUID for this characteristic
            Guid serviceUuid = Guid.Empty;
            foreach (var service in _gattServices)
            {
                if (_characteristicCache.TryGetValue(sender.Uuid, out var cached) &&
                    cached == sender)
                {
                    serviceUuid = service.Uuid;
                    break;
                }
            }

            NotificationReceived?.Invoke(this, new BleNotificationEventArgs
            {
                ServiceUuid = serviceUuid,
                CharacteristicUuid = sender.Uuid,
                Data = data,
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error reading notification");
        }
    }

    /// <summary>
    /// Handles device connection status changes.
    /// </summary>
    private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
        {
            // Only handle disconnect if we were actually GATT connected
            if (_isGattConnected)
            {
                DisconnectAndCleanup();
                RaiseConnectionStateChanged();
                RaiseStatusMessage("GATT disconnected. Will reconnect on next advertisement...");
            }
        }
    }

    /// <summary>
    /// Cleans up connection resources.
    /// </summary>
    private void DisconnectAndCleanup()
    {
        // Unsubscribe from characteristics
        foreach (var characteristic in _subscribedCharacteristics)
        {
            try
            {
                characteristic.ValueChanged -= OnCharacteristicValueChanged;
            }
            catch { }
        }
        _subscribedCharacteristics.Clear();

        // Dispose services
        foreach (var service in _gattServices)
        {
            try
            {
                service.Dispose();
            }
            catch { }
        }
        _gattServices.Clear();
        _characteristicCache.Clear();

        // Dispose device
        if (_connectedDevice != null)
        {
            _connectedDevice.ConnectionStatusChanged -= OnConnectionStatusChanged;
            _connectedDevice.Dispose();
            _connectedDevice = null;
        }

        _isGattConnected = false;
    }

    #endregion

    #region Private Methods - Events

    /// <summary>
    /// Raises the ConnectionStateChanged event.
    /// </summary>
    private void RaiseConnectionStateChanged()
    {
        ConnectionStateChanged?.Invoke(this, new BleConnectionStateEventArgs
        {
            IsDeviceDetected = _isDeviceDetected,
            IsGattConnected = _isGattConnected,
            DeviceName = _deviceName
        });
    }

    /// <summary>
    /// Raises the StatusMessageChanged event.
    /// </summary>
    private void RaiseStatusMessage(string message)
    {
        StatusMessageChanged?.Invoke(this, message);
    }

    #endregion

    #region Private Methods - Helpers

    /// <summary>
    /// Wraps an async operation with a timeout to prevent indefinite hangs.
    /// </summary>
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

    /// <summary>
    /// Safely runs an async task without awaiting, handling any exceptions.
    /// This replaces the fire-and-forget pattern to ensure errors are not silently swallowed.
    /// </summary>
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

    /// <summary>
    /// Throws ObjectDisposedException if the service has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    #endregion
}
#endif
