using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Scalextric;
using ScalextricBleMonitor.Models;
using ScalextricBleMonitor.Services;

namespace ScalextricBleMonitor.ViewModels;

/// <summary>
/// Manages BLE connection state, GATT service discovery, and characteristic operations.
/// </summary>
public partial class BleConnectionViewModel : ObservableObject, IDisposable
{
    private readonly Scalextric.IBleService _bleService;
    private readonly IDispatcherService _dispatcher;
    private bool _disposed;

    /// <summary>
    /// Indicates whether the Scalextric device is currently detected via advertisement.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentConnectionState))]
    [NotifyPropertyChangedFor(nameof(ConnectionStatusText))]
    private bool _isConnected;

    /// <summary>
    /// Indicates whether we have an active GATT connection.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentConnectionState))]
    [NotifyPropertyChangedFor(nameof(ConnectionStatusText))]
    private bool _isGattConnected;

    /// <summary>
    /// The device name when detected.
    /// </summary>
    [ObservableProperty]
    private string _deviceName = string.Empty;

    /// <summary>
    /// Additional status information.
    /// </summary>
    [ObservableProperty]
    private string _statusText = "Initializing...";

    /// <summary>
    /// Discovered GATT services and characteristics.
    /// </summary>
    public ObservableCollection<ServiceViewModel> Services { get; } = [];

    /// <summary>
    /// Current BLE connection state for binding to UI.
    /// </summary>
    public ConnectionState CurrentConnectionState =>
        IsGattConnected ? ConnectionState.GattConnected :
        IsConnected ? ConnectionState.Advertising :
        ConnectionState.Disconnected;

    /// <summary>
    /// Text showing connection state.
    /// </summary>
    public string ConnectionStatusText =>
        IsGattConnected ? "GATT Connected" :
        IsConnected ? "Detected" :
        "Disconnected";

    /// <summary>
    /// Event raised when GATT connection is first established.
    /// </summary>
    public event EventHandler? GattConnected;

    /// <summary>
    /// Event raised when GATT connection is lost.
    /// </summary>
    public event EventHandler? GattDisconnected;

    /// <summary>
    /// Event raised when device is disconnected (no longer detected).
    /// </summary>
    public event EventHandler? DeviceDisconnected;

    /// <summary>
    /// Event raised when a notification is received.
    /// </summary>
    public event EventHandler<BleNotificationEventArgs>? NotificationReceived;

    /// <summary>
    /// Initializes a new instance of the BleConnectionViewModel.
    /// </summary>
    public BleConnectionViewModel(Scalextric.IBleService bleService, IDispatcherService dispatcher)
    {
        _bleService = bleService;
        _dispatcher = dispatcher;

        _bleService.ConnectionStateChanged += OnConnectionStateChanged;
        _bleService.StatusMessageChanged += OnStatusMessageChanged;
        _bleService.ServicesDiscovered += OnServicesDiscovered;
        _bleService.NotificationReceived += OnNotificationReceived;
        _bleService.CharacteristicValueRead += OnCharacteristicValueRead;
    }

    /// <summary>
    /// Starts BLE scanning.
    /// </summary>
    public void StartMonitoring()
    {
        _bleService.StartScanning();
    }

    /// <summary>
    /// Stops BLE scanning.
    /// </summary>
    public void StopMonitoring()
    {
        _bleService.StopScanning();
    }

    /// <summary>
    /// Requests a read of the specified characteristic.
    /// </summary>
    public void ReadCharacteristic(Guid serviceUuid, Guid characteristicUuid)
    {
        _bleService.ReadCharacteristic(serviceUuid, characteristicUuid);
    }

    private void OnConnectionStateChanged(object? sender, BleConnectionStateEventArgs e)
    {
        _dispatcher.Post(() =>
        {
            var wasGattConnected = IsGattConnected;
            IsConnected = e.IsDeviceDetected;
            IsGattConnected = e.IsGattConnected;
            DeviceName = e.DeviceName ?? string.Empty;

            // When GATT connection is first established
            if (e.IsGattConnected && !wasGattConnected)
            {
                GattConnected?.Invoke(this, EventArgs.Empty);
            }

            if (!e.IsGattConnected && wasGattConnected)
            {
                GattDisconnected?.Invoke(this, EventArgs.Empty);
            }

            if (!e.IsDeviceDetected)
            {
                Services.Clear();
                DeviceDisconnected?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    private void OnStatusMessageChanged(object? sender, string message)
    {
        _dispatcher.Post(() =>
        {
            StatusText = message;
        });
    }

    private void OnServicesDiscovered(object? sender, BleServicesDiscoveredEventArgs e)
    {
        _dispatcher.Post(() =>
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
            _bleService.SubscribeToAllNotifications();
        });
    }

    private void OnNotificationReceived(object? sender, BleNotificationEventArgs e)
    {
        NotificationReceived?.Invoke(this, e);
    }

    private void OnCharacteristicValueRead(object? sender, BleCharacteristicReadEventArgs e)
    {
        _dispatcher.Post(() =>
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
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _bleService.ConnectionStateChanged -= OnConnectionStateChanged;
        _bleService.StatusMessageChanged -= OnStatusMessageChanged;
        _bleService.ServicesDiscovered -= OnServicesDiscovered;
        _bleService.NotificationReceived -= OnNotificationReceived;
        _bleService.CharacteristicValueRead -= OnCharacteristicValueRead;
        _bleService.Dispose();

        GC.SuppressFinalize(this);
    }
}
