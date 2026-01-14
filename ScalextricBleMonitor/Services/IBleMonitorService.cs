using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
/// Event args for characteristic value read results.
/// </summary>
public class BleCharacteristicReadEventArgs : EventArgs
{
    public Guid ServiceUuid { get; init; }
    public Guid CharacteristicUuid { get; init; }
    public string? CharacteristicName { get; init; }
    public byte[] Data { get; init; } = [];
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Event args for characteristic write results.
/// </summary>
public class BleCharacteristicWriteEventArgs : EventArgs
{
    public Guid CharacteristicUuid { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
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
    /// Raised when a characteristic value has been read.
    /// </summary>
    event EventHandler<BleCharacteristicReadEventArgs>? CharacteristicValueRead;

    /// <summary>
    /// Raised when a characteristic write completes.
    /// </summary>
    event EventHandler<BleCharacteristicWriteEventArgs>? CharacteristicWriteCompleted;

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

    /// <summary>
    /// Reads the value from a characteristic.
    /// </summary>
    /// <param name="serviceUuid">The service UUID containing the characteristic.</param>
    /// <param name="characteristicUuid">The characteristic UUID to read from.</param>
    void ReadCharacteristic(Guid serviceUuid, Guid characteristicUuid);

    /// <summary>
    /// Writes a value to a characteristic.
    /// </summary>
    /// <param name="characteristicUuid">The characteristic UUID to write to.</param>
    /// <param name="data">The data to write.</param>
    void WriteCharacteristic(Guid characteristicUuid, byte[] data);

    /// <summary>
    /// Writes a value to a characteristic asynchronously and waits for completion.
    /// </summary>
    /// <param name="characteristicUuid">The characteristic UUID to write to.</param>
    /// <param name="data">The data to write.</param>
    /// <returns>True if the write succeeded, false otherwise.</returns>
    Task<bool> WriteCharacteristicAwaitAsync(Guid characteristicUuid, byte[] data);
}
