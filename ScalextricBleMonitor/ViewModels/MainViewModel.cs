using System;
using System.Collections.ObjectModel;
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

    // Track previous lane change state to detect edges (button press events)
    private readonly bool[] _previousLaneChangeState = new bool[6]; // Up to 6 car slots

    /// <summary>
    /// Lane change press count for each car slot (1-6).
    /// </summary>
    [ObservableProperty]
    private int _laneChangeCount1;

    [ObservableProperty]
    private int _laneChangeCount2;

    [ObservableProperty]
    private int _laneChangeCount3;

    [ObservableProperty]
    private int _laneChangeCount4;

    [ObservableProperty]
    private int _laneChangeCount5;

    [ObservableProperty]
    private int _laneChangeCount6;

    /// <summary>
    /// Total lane change presses across all slots.
    /// </summary>
    [ObservableProperty]
    private int _totalLaneChangeCount;

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

            if (!e.IsConnected)
            {
                Services.Clear();
                ResetLaneChangeCounters();
            }
        });
    }

    private void ResetLaneChangeCounters()
    {
        LaneChangeCount1 = 0;
        LaneChangeCount2 = 0;
        LaneChangeCount3 = 0;
        LaneChangeCount4 = 0;
        LaneChangeCount5 = 0;
        LaneChangeCount6 = 0;
        TotalLaneChangeCount = 0;
        Array.Clear(_previousLaneChangeState);
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
            // Detect lane change button presses (rising edge)
            DetectLaneChangePresses(e.Data);

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

    private void DetectLaneChangePresses(byte[] data)
    {
        for (int i = 0; i < data.Length && i < 6; i++)
        {
            bool currentLaneChange = (data[i] & 0x80) != 0;
            bool previousLaneChange = _previousLaneChangeState[i];

            // Detect rising edge (button was just pressed)
            if (currentLaneChange && !previousLaneChange)
            {
                TotalLaneChangeCount++;

                // Increment the specific slot counter
                switch (i)
                {
                    case 0: LaneChangeCount1++; break;
                    case 1: LaneChangeCount2++; break;
                    case 2: LaneChangeCount3++; break;
                    case 3: LaneChangeCount4++; break;
                    case 4: LaneChangeCount5++; break;
                    case 5: LaneChangeCount6++; break;
                }
            }

            _previousLaneChangeState[i] = currentLaneChange;
        }
    }

    private static string DecodeScalextricData(Guid characteristicUuid, byte[] data)
    {
        if (data.Length == 0) return "(empty)";

        // Try to decode based on known Scalextric data formats
        // Throttle data: Bits 0-5 = throttle (0-63), Bit 6 = brake, Bit 7 = lane change
        if (data.Length >= 1)
        {
            var parts = new System.Collections.Generic.List<string>();

            foreach (var b in data)
            {
                int throttle = b & 0x3F;
                bool brake = (b & 0x40) != 0;
                bool laneChange = (b & 0x80) != 0;

                var decoded = $"T:{throttle}";
                if (brake) decoded += " BRK";
                if (laneChange) decoded += " LC";
                parts.Add(decoded);
            }

            return string.Join(" | ", parts);
        }

        return "(raw)";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _bleMonitorService.ConnectionStateChanged -= OnConnectionStateChanged;
        _bleMonitorService.StatusMessageChanged -= OnStatusMessageChanged;
        _bleMonitorService.ServicesDiscovered -= OnServicesDiscovered;
        _bleMonitorService.NotificationReceived -= OnNotificationReceived;
        _bleMonitorService.Dispose();

        GC.SuppressFinalize(this);
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
    private string _name = string.Empty;

    [ObservableProperty]
    private string _properties = string.Empty;

    public string DisplayText => $"{Name} [{Properties}]";
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
