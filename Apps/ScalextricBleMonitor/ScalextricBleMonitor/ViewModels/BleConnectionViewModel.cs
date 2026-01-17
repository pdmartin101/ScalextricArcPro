using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Scalextric;
using ScalextricBleMonitor.Services;

namespace ScalextricBleMonitor.ViewModels;

/// <summary>
/// Manages BLE connection state, GATT service discovery, and characteristic operations.
/// </summary>
public partial class BleConnectionViewModel : ObservableObject, IDisposable
{
    private readonly IBleMonitorService _bleMonitorService;
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
    /// Brush for the status indicator circle.
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
    public BleConnectionViewModel(IBleMonitorService bleMonitorService)
    {
        _bleMonitorService = bleMonitorService;

        _bleMonitorService.ConnectionStateChanged += OnConnectionStateChanged;
        _bleMonitorService.StatusMessageChanged += OnStatusMessageChanged;
        _bleMonitorService.ServicesDiscovered += OnServicesDiscovered;
        _bleMonitorService.NotificationReceived += OnNotificationReceived;
        _bleMonitorService.CharacteristicValueRead += OnCharacteristicValueRead;
    }

    /// <summary>
    /// Starts BLE scanning.
    /// </summary>
    public void StartMonitoring()
    {
        _bleMonitorService.StartScanning();
    }

    /// <summary>
    /// Stops BLE scanning.
    /// </summary>
    public void StopMonitoring()
    {
        _bleMonitorService.StopScanning();
    }

    /// <summary>
    /// Requests a read of the specified characteristic.
    /// </summary>
    public void ReadCharacteristic(Guid serviceUuid, Guid characteristicUuid)
    {
        _bleMonitorService.ReadCharacteristic(serviceUuid, characteristicUuid);
    }

    private void OnConnectionStateChanged(object? sender, BleConnectionStateEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
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
        NotificationReceived?.Invoke(this, e);
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
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _bleMonitorService.ConnectionStateChanged -= OnConnectionStateChanged;
        _bleMonitorService.StatusMessageChanged -= OnStatusMessageChanged;
        _bleMonitorService.ServicesDiscovered -= OnServicesDiscovered;
        _bleMonitorService.NotificationReceived -= OnNotificationReceived;
        _bleMonitorService.CharacteristicValueRead -= OnCharacteristicValueRead;
        _bleMonitorService.Dispose();

        GC.SuppressFinalize(this);
    }
}
