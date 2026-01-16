namespace ScalextricRace.Services;

/// <summary>
/// Event args for BLE connection state changes.
/// </summary>
public class BleConnectionStateEventArgs : EventArgs
{
    /// <summary>
    /// Whether a device has been detected via BLE advertisement.
    /// </summary>
    public bool IsDeviceDetected { get; init; }

    /// <summary>
    /// Whether an active GATT connection exists.
    /// </summary>
    public bool IsGattConnected { get; init; }

    /// <summary>
    /// The name of the detected device (if any).
    /// </summary>
    public string? DeviceName { get; init; }
}

/// <summary>
/// Event args for BLE notification data.
/// </summary>
public class BleNotificationEventArgs : EventArgs
{
    /// <summary>
    /// The GATT service UUID.
    /// </summary>
    public Guid ServiceUuid { get; init; }

    /// <summary>
    /// The GATT characteristic UUID.
    /// </summary>
    public Guid CharacteristicUuid { get; init; }

    /// <summary>
    /// The notification data bytes.
    /// </summary>
    public required byte[] Data { get; init; }

    /// <summary>
    /// Timestamp when the notification was received.
    /// </summary>
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Interface for BLE operations with Scalextric powerbases.
/// </summary>
public interface IBleService : IDisposable
{
    /// <summary>
    /// Raised when the connection state changes (device detected, GATT connected, etc.).
    /// </summary>
    event EventHandler<BleConnectionStateEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Raised when a notification is received from a subscribed characteristic.
    /// </summary>
    event EventHandler<BleNotificationEventArgs>? NotificationReceived;

    /// <summary>
    /// Raised when a status message should be displayed to the user.
    /// </summary>
    event EventHandler<string>? StatusMessageChanged;

    /// <summary>
    /// Gets whether BLE scanning is currently active.
    /// </summary>
    bool IsScanning { get; }

    /// <summary>
    /// Gets whether a device has been detected via BLE advertisement.
    /// </summary>
    bool IsDeviceDetected { get; }

    /// <summary>
    /// Gets whether an active GATT connection exists.
    /// </summary>
    bool IsGattConnected { get; }

    /// <summary>
    /// Gets the name of the connected device.
    /// </summary>
    string? DeviceName { get; }

    /// <summary>
    /// Starts scanning for Scalextric BLE devices.
    /// </summary>
    void StartScanning();

    /// <summary>
    /// Stops BLE scanning.
    /// </summary>
    void StopScanning();

    /// <summary>
    /// Writes data to a characteristic.
    /// </summary>
    /// <param name="characteristicUuid">The characteristic UUID to write to.</param>
    /// <param name="data">The data to write.</param>
    /// <returns>True if the write succeeded.</returns>
    Task<bool> WriteCharacteristicAsync(Guid characteristicUuid, byte[] data);
}
