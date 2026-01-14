using System;
using System.Collections.Generic;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Represents a discovered GATT service.
/// </summary>
public class BleServiceInfo
{
    public Guid Uuid { get; init; }
    public string? Name { get; init; }
    public List<BleCharacteristicInfo> Characteristics { get; init; } = [];
}

/// <summary>
/// Represents a discovered GATT characteristic.
/// </summary>
public class BleCharacteristicInfo
{
    public Guid Uuid { get; init; }
    public string? Name { get; init; }
    public string Properties { get; init; } = string.Empty;
}

/// <summary>
/// Event args for BLE connection state changes.
/// </summary>
public class BleConnectionStateEventArgs : EventArgs
{
    public bool IsConnected { get; init; }
    public bool IsGattConnected { get; init; }
    public string? DeviceName { get; init; }
    public ulong? BluetoothAddress { get; init; }
    public DateTime? LastSeen { get; init; }
}

/// <summary>
/// Event args for GATT services discovered.
/// </summary>
public class BleServicesDiscoveredEventArgs : EventArgs
{
    public List<BleServiceInfo> Services { get; init; } = [];
}

/// <summary>
/// Event args for characteristic notification data received.
/// </summary>
public class BleNotificationEventArgs : EventArgs
{
    public Guid ServiceUuid { get; init; }
    public Guid CharacteristicUuid { get; init; }
    public string? CharacteristicName { get; init; }
    public byte[] Data { get; init; } = [];
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Interface for BLE monitoring service.
/// Abstracts platform-specific BLE scanning to allow future cross-platform support.
/// </summary>
public interface IBleMonitorService : IDisposable
{
    /// <summary>
    /// Raised when the connection state changes (device detected/lost).
    /// </summary>
    event EventHandler<BleConnectionStateEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Raised when there's a status message to display (info, errors, etc.).
    /// </summary>
    event EventHandler<string>? StatusMessageChanged;

    /// <summary>
    /// Raised when GATT services have been discovered.
    /// </summary>
    event EventHandler<BleServicesDiscoveredEventArgs>? ServicesDiscovered;

    /// <summary>
    /// Raised when notification data is received from a characteristic.
    /// </summary>
    event EventHandler<BleNotificationEventArgs>? NotificationReceived;

    /// <summary>
    /// Whether the service is currently scanning.
    /// </summary>
    bool IsScanning { get; }

    /// <summary>
    /// Whether we have an active GATT connection.
    /// </summary>
    bool IsGattConnected { get; }

    /// <summary>
    /// Starts BLE advertisement scanning.
    /// </summary>
    void StartScanning();

    /// <summary>
    /// Stops BLE advertisement scanning.
    /// </summary>
    void StopScanning();

    /// <summary>
    /// Attempts to connect via GATT and discover services.
    /// </summary>
    void ConnectAndDiscoverServices();

    /// <summary>
    /// Disconnects the GATT connection.
    /// </summary>
    void Disconnect();

    /// <summary>
    /// Subscribes to notifications on all characteristics that support Notify or Indicate.
    /// </summary>
    void SubscribeToAllNotifications();
}
