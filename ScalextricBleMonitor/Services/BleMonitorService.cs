using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

#if WINDOWS
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
#endif

namespace ScalextricBleMonitor.Services;

/// <summary>
/// BLE monitoring service using Windows.Devices.Bluetooth APIs.
/// Scans for Scalextric ARC Pro powerbase advertisements and connects via GATT.
/// </summary>
public class BleMonitorService : IBleMonitorService
{
    // How long without seeing advertisements before we consider device lost (only when NOT GATT connected)
    private static readonly TimeSpan DeviceTimeoutDuration = TimeSpan.FromSeconds(10);

    // Timer interval to check for device timeout
    private static readonly TimeSpan TimeoutCheckInterval = TimeSpan.FromSeconds(2);

    public event EventHandler<BleConnectionStateEventArgs>? ConnectionStateChanged;
    public event EventHandler<string>? StatusMessageChanged;
    public event EventHandler<BleServicesDiscoveredEventArgs>? ServicesDiscovered;
    public event EventHandler<BleNotificationEventArgs>? NotificationReceived;
    public event EventHandler<BleCharacteristicReadEventArgs>? CharacteristicValueRead;
    public event EventHandler<BleCharacteristicWriteEventArgs>? CharacteristicWriteCompleted;

    public bool IsScanning { get; private set; }
    public bool IsGattConnected { get; private set; }

    private bool _isDeviceDetected;
    private string? _lastDeviceName;
    private ulong? _lastBluetoothAddress;
    private DateTime? _lastSeenTime;
    private Timer? _timeoutTimer;
    private bool _disposed;
    private bool _isConnecting;
    private readonly object _connectionLock = new();
    private int _connectionAttempts;
    private const int MaxConnectionAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

#if WINDOWS
    private BluetoothLEAdvertisementWatcher? _watcher;
    private BluetoothLEDevice? _connectedDevice;
    private readonly List<GattDeviceService> _gattServices = [];
    private readonly List<(Guid ServiceUuid, GattCharacteristic Characteristic)> _subscribedCharacteristics = [];
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

    public void StartScanning()
    {
        ThrowIfDisposed();
        if (IsScanning) return;

#if WINDOWS
        try
        {
            // Create and configure the advertisement watcher
            _watcher = new BluetoothLEAdvertisementWatcher
            {
                // Active scanning requests scan response data (more info about device)
                ScanningMode = BluetoothLEScanningMode.Active
            };

            // Subscribe to events
            _watcher.Received += OnAdvertisementReceived;
            _watcher.Stopped += OnWatcherStopped;

            // Start the watcher
            _watcher.Start();
            IsScanning = true;

            // Start the timeout checker timer
            _timeoutTimer = new Timer(CheckDeviceTimeout, null, TimeoutCheckInterval, TimeoutCheckInterval);

            RaiseStatusMessage("Scanning for Scalextric ARC Pro...");
        }
        catch (Exception ex)
        {
            RaiseStatusMessage($"Failed to start scanning: {ex.Message}");
        }
#else
        // TODO: macOS/Linux support using InTheHand.BluetoothLE or SimpleBLE
        RaiseStatusMessage("BLE scanning not implemented for this platform.");
#endif
    }

    public void StopScanning()
    {
        ThrowIfDisposed();
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

    public void ConnectAndDiscoverServices()
    {
        ThrowIfDisposed();
#if WINDOWS
        if (_lastBluetoothAddress.HasValue && !_isConnecting && !IsGattConnected)
        {
            RunFireAndForget(
                () => ConnectAndDiscoverServicesAsync(_lastBluetoothAddress.Value),
                "ConnectAndDiscoverServices");
        }
#endif
    }

    public void Disconnect()
    {
        ThrowIfDisposed();
#if WINDOWS
        DisconnectInternal(raiseEvents: true);
        RaiseStatusMessage("Disconnected from device.");
#endif
    }

    public void SubscribeToAllNotifications()
    {
        ThrowIfDisposed();
#if WINDOWS
        if (!IsGattConnected)
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
        if (!IsGattConnected)
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
        if (!IsGattConnected)
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

    public async Task<bool> WriteCharacteristicAwaitAsync(Guid characteristicUuid, byte[] data)
    {
        ThrowIfDisposed();
#if WINDOWS
        if (!IsGattConnected)
        {
            return false;
        }
        return await WriteCharacteristicInternalAsync(characteristicUuid, data);
#else
        await Task.CompletedTask;
        return false;
#endif
    }

#if WINDOWS
    private async Task<bool> WriteCharacteristicInternalAsync(Guid characteristicUuid, byte[] data)
    {
        try
        {
            // Find the characteristic across all services
            GattCharacteristic? targetCharacteristic = null;
            foreach (var service in _gattServices)
            {
                var charsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Cached);
                if (charsResult.Status == GattCommunicationStatus.Success)
                {
                    targetCharacteristic = charsResult.Characteristics.FirstOrDefault(c => c.Uuid == characteristicUuid);
                    if (targetCharacteristic != null) break;
                }
            }

            if (targetCharacteristic == null)
            {
                CharacteristicWriteCompleted?.Invoke(this, new BleCharacteristicWriteEventArgs
                {
                    CharacteristicUuid = characteristicUuid,
                    Success = false,
                    ErrorMessage = "Characteristic not found"
                });
                return false;
            }

            // Check if writable
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

            // Create buffer and write
            using var writer = new Windows.Storage.Streams.DataWriter();
            writer.WriteBytes(data);
            var buffer = writer.DetachBuffer();

            var writeOption = props.HasFlag(GattCharacteristicProperties.Write)
                ? GattWriteOption.WriteWithResponse
                : GattWriteOption.WriteWithoutResponse;

            var status = await targetCharacteristic.WriteValueAsync(buffer, writeOption);

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

    private async Task ReadCharacteristicAsync(Guid serviceUuid, Guid characteristicUuid)
    {
        try
        {
            // Find the service
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

            // Get characteristics
            var charsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Cached);
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

            // Find the characteristic
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

            // Check if readable
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

            // Read the value
            var readResult = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
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

            // Extract the data
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

    private async Task SubscribeToAllNotificationsAsync()
    {
        int subscribed = 0;
        int failed = 0;

        foreach (var service in _gattServices)
        {
            try
            {
                var charsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Cached);
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
                        // Determine which descriptor value to use
                        var cccdValue = canNotify
                            ? GattClientCharacteristicConfigurationDescriptorValue.Notify
                            : GattClientCharacteristicConfigurationDescriptorValue.Indicate;

                        var status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue);

                        if (status == GattCommunicationStatus.Success)
                        {
                            characteristic.ValueChanged += OnCharacteristicValueChanged;
                            _subscribedCharacteristics.Add((service.Uuid, characteristic));
                            subscribed++;

                            var charName = GetCharacteristicName(characteristic.Uuid);
                            System.Diagnostics.Debug.WriteLine($"Subscribed to {charName} ({characteristic.Uuid})");
                        }
                        else
                        {
                            failed++;
                            System.Diagnostics.Debug.WriteLine($"Failed to subscribe to {characteristic.Uuid}: {status}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        System.Diagnostics.Debug.WriteLine($"Exception subscribing to {characteristic.Uuid}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting characteristics for service {service.Uuid}: {ex.Message}");
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
            // Read the data from the buffer
            using var reader = Windows.Storage.Streams.DataReader.FromBuffer(args.CharacteristicValue);
            var data = new byte[args.CharacteristicValue.Length];
            reader.ReadBytes(data);

            // Find the service UUID for this characteristic
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
            System.Diagnostics.Debug.WriteLine($"Error processing notification: {ex.Message}");
        }
    }

    private void OnAdvertisementReceived(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementReceivedEventArgs args)
    {
        try
        {
            // Check if this is a Scalextric device by local name
            var localName = args.Advertisement.LocalName;

            // Match on "Scalextric" in name (case insensitive)
            if (!string.IsNullOrEmpty(localName) &&
                localName.Contains("Scalextric", StringComparison.OrdinalIgnoreCase))
            {
                // Update last seen info
                _lastSeenTime = DateTime.Now;
                _lastDeviceName = localName;
                _lastBluetoothAddress = args.BluetoothAddress;

                if (!_isDeviceDetected)
                {
                    _isDeviceDetected = true;
                    RaiseConnectionStateChanged();
                    RaiseStatusMessage($"Found {localName}. Connecting...");

                    // Automatically attempt GATT connection when device is found
                    RunFireAndForget(
                        () => ConnectAndDiscoverServicesAsync(args.BluetoothAddress),
                        "AutoConnectOnAdvertisement");
                }
                // Don't spam UI updates for every advertisement when already connected
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error processing advertisement: {ex.Message}");
        }
    }

    private async Task ConnectAndDiscoverServicesAsync(ulong bluetoothAddress)
    {
        // Use lock to prevent multiple simultaneous connection attempts
        lock (_connectionLock)
        {
            if (_isConnecting || IsGattConnected) return;
            _isConnecting = true;
            _connectionAttempts = 0;
        }

        while (_connectionAttempts < MaxConnectionAttempts && !IsGattConnected && !_disposed)
        {
            _connectionAttempts++;

            try
            {
                if (_connectionAttempts > 1)
                {
                    RaiseStatusMessage($"Retrying connection (attempt {_connectionAttempts}/{MaxConnectionAttempts})...");
                    await Task.Delay(RetryDelay);
                }
                else
                {
                    RaiseStatusMessage("Connecting to device...");
                    // Small delay on first attempt to let BLE stack settle
                    await Task.Delay(100);
                }

                // Get the device
                _connectedDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress);
                if (_connectedDevice == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Attempt {_connectionAttempts}: Device not found");
                    continue;
                }

                // Subscribe to connection status changes
                _connectedDevice.ConnectionStatusChanged += OnConnectionStatusChanged;

                // Get GATT services - this establishes the actual connection
                RaiseStatusMessage("Discovering GATT services...");
                var gattResult = await _connectedDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);

                if (gattResult.Status != GattCommunicationStatus.Success)
                {
                    System.Diagnostics.Debug.WriteLine($"Attempt {_connectionAttempts}: GetGattServicesAsync failed: {gattResult.Status}");
                    DisconnectInternal(raiseEvents: false);
                    continue;
                }

                // Store services and discover characteristics
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

                    // Get characteristics for this service
                    var charsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
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
                        }
                    }

                    discoveredServices.Add(serviceInfo);
                }

                // Success!
                IsGattConnected = true;
                _isConnecting = false;
                _connectionAttempts = 0;

                RaiseConnectionStateChanged();
                RaiseStatusMessage($"Connected! Found {discoveredServices.Count} services.");

                // Raise services discovered event
                ServicesDiscovered?.Invoke(this, new BleServicesDiscoveredEventArgs
                {
                    Services = discoveredServices
                });

                return; // Exit the retry loop on success
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Attempt {_connectionAttempts}: Exception: {ex.Message}");
                DisconnectInternal(raiseEvents: false);
            }
        }

        // All attempts failed
        _isConnecting = false;
        if (!IsGattConnected)
        {
            RaiseStatusMessage($"Failed to connect after {MaxConnectionAttempts} attempts. Will retry on next advertisement...");
            // Reset device detected so next advertisement triggers a new connection attempt
            _isDeviceDetected = false;
            RaiseConnectionStateChanged();
        }
    }

    private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
        {
            // Only handle disconnect if we were actually GATT connected
            if (IsGattConnected)
            {
                DisconnectInternal(raiseEvents: true);
                RaiseStatusMessage("GATT disconnected. Will reconnect on next advertisement...");
            }
        }
    }

    private void DisconnectInternal(bool raiseEvents)
    {
        var wasConnected = IsGattConnected;
        IsGattConnected = false;

        // Unsubscribe from all characteristic notifications
        foreach (var (_, characteristic) in _subscribedCharacteristics)
        {
            try
            {
                characteristic.ValueChanged -= OnCharacteristicValueChanged;
            }
            catch { /* ignore */ }
        }
        _subscribedCharacteristics.Clear();

        // Dispose all GATT services
        foreach (var service in _gattServices)
        {
            try { service.Dispose(); } catch { /* ignore */ }
        }
        _gattServices.Clear();

        // Dispose the device
        if (_connectedDevice != null)
        {
            try
            {
                _connectedDevice.ConnectionStatusChanged -= OnConnectionStatusChanged;
                _connectedDevice.Dispose();
            }
            catch { /* ignore */ }
            _connectedDevice = null;
        }

        if (raiseEvents && wasConnected)
        {
            RaiseConnectionStateChanged();
        }
    }

    private void OnWatcherStopped(BluetoothLEAdvertisementWatcher sender,
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

    private void CheckDeviceTimeout(object? state)
    {
        // Don't timeout if we have an active GATT connection - rely on GATT disconnect events instead
        if (IsGattConnected) return;

        if (_isDeviceDetected && _lastSeenTime.HasValue)
        {
            var timeSinceLastSeen = DateTime.Now - _lastSeenTime.Value;
            if (timeSinceLastSeen > DeviceTimeoutDuration)
            {
                _isDeviceDetected = false;
#if WINDOWS
                DisconnectInternal(raiseEvents: false);
#endif
                RaiseConnectionStateChanged();
                RaiseStatusMessage($"Device lost. Last seen: {_lastSeenTime.Value:HH:mm:ss}");
            }
        }
    }

    private void RaiseConnectionStateChanged()
    {
        ConnectionStateChanged?.Invoke(this, new BleConnectionStateEventArgs
        {
            IsConnected = _isDeviceDetected,
            IsGattConnected = IsGattConnected,
            DeviceName = _lastDeviceName,
            BluetoothAddress = _lastBluetoothAddress,
            LastSeen = _lastSeenTime
        });
    }

    private void RaiseStatusMessage(string message)
    {
        StatusMessageChanged?.Invoke(this, message);
    }

    /// <summary>
    /// Throws ObjectDisposedException if the service has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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
                RaiseStatusMessage($"Error: {ex.Message}");
            }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopScanning();
#if WINDOWS
        DisconnectInternal(raiseEvents: false);
#endif
        GC.SuppressFinalize(this);
    }
}
