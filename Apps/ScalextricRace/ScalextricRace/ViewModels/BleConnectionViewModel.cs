using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Scalextric;
using ScalextricRace.Models;
using ScalextricRace.Services;
using Serilog;

namespace ScalextricRace.ViewModels;

/// <summary>
/// Manages BLE connection state, scanning, and event handling for the Scalextric powerbase.
/// </summary>
public partial class BleConnectionViewModel : ObservableObject, IDisposable
{
    private readonly Services.IBleService? _bleService;
    private readonly SynchronizationContext? _syncContext;

    /// <summary>
    /// Indicates whether BLE scanning is active.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionStatusText))]
    [NotifyPropertyChangedFor(nameof(CurrentConnectionState))]
    private bool _isScanning;

    /// <summary>
    /// Indicates whether a Scalextric device has been detected via BLE advertisement.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionStatusText))]
    [NotifyPropertyChangedFor(nameof(CurrentConnectionState))]
    private bool _isDeviceDetected;

    /// <summary>
    /// Indicates whether an active GATT connection exists to the powerbase.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionStatusText))]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    [NotifyPropertyChangedFor(nameof(CurrentConnectionState))]
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
    /// Gets the current connection state for UI display.
    /// Used with a converter to determine status indicator color.
    /// </summary>
    public ConnectionState CurrentConnectionState => (IsScanning, IsDeviceDetected, IsGattConnected) switch
    {
        (_, _, true) => ConnectionState.Connected,
        (_, true, false) => ConnectionState.Connecting,
        (true, false, _) => ConnectionState.Connecting,
        _ => ConnectionState.Disconnected
    };

    /// <summary>
    /// Event raised when GATT connection is established.
    /// </summary>
    public event EventHandler? GattConnected;

    /// <summary>
    /// Event raised when notification data is received.
    /// </summary>
    public event EventHandler<BleNotificationEventArgs>? NotificationReceived;

    /// <summary>
    /// Initializes a new instance of the BleConnectionViewModel.
    /// </summary>
    /// <param name="bleService">The BLE service for device communication.</param>
    public BleConnectionViewModel(Services.IBleService? bleService = null)
    {
        _bleService = bleService;
        _syncContext = SynchronizationContext.Current;

        // Subscribe to BLE service events
        if (_bleService != null)
        {
            _bleService.ConnectionStateChanged += OnConnectionStateChanged;
            _bleService.StatusMessageChanged += OnStatusMessageChanged;
            _bleService.NotificationReceived += OnNotificationReceived;
        }
    }

    /// <summary>
    /// Starts monitoring for Scalextric devices.
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
    /// Stops monitoring for Scalextric devices.
    /// </summary>
    public void StopMonitoring()
    {
        if (_bleService == null) return;

        Log.Information("Stopping BLE monitoring");
        _bleService.StopScanning();
        IsScanning = false;
    }

    /// <summary>
    /// Handles BLE connection state changes.
    /// </summary>
    private void OnConnectionStateChanged(object? sender, BleConnectionStateEventArgs e)
    {
        PostToUIThread(() =>
        {
            var wasConnected = IsGattConnected;

            IsDeviceDetected = e.IsDeviceDetected;
            IsGattConnected = e.IsGattConnected;

            // Raise event if we just connected
            if (!wasConnected && IsGattConnected)
            {
                GattConnected?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    /// <summary>
    /// Handles BLE status message changes.
    /// </summary>
    private void OnStatusMessageChanged(object? sender, string message)
    {
        PostToUIThread(() =>
        {
            StatusMessage = message;
        });
    }

    /// <summary>
    /// Handles BLE notification data.
    /// </summary>
    private void OnNotificationReceived(object? sender, BleNotificationEventArgs e)
    {
        // Forward to subscribers
        NotificationReceived?.Invoke(this, e);
    }

    /// <summary>
    /// Posts an action to the UI thread using SynchronizationContext.
    /// Falls back to direct execution if no context is available.
    /// </summary>
    private void PostToUIThread(Action action)
    {
        if (_syncContext != null)
        {
            _syncContext.Post(_ => action(), null);
        }
        else
        {
            action();
        }
    }

    /// <summary>
    /// Disposes resources and unsubscribes from events.
    /// </summary>
    public void Dispose()
    {
        if (_bleService != null)
        {
            _bleService.ConnectionStateChanged -= OnConnectionStateChanged;
            _bleService.StatusMessageChanged -= OnStatusMessageChanged;
            _bleService.NotificationReceived -= OnNotificationReceived;
        }
    }
}
