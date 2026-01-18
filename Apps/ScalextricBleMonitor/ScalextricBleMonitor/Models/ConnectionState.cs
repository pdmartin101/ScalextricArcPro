namespace ScalextricBleMonitor.Models;

/// <summary>
/// Represents the BLE connection state of the Scalextric device.
/// </summary>
public enum ConnectionState
{
    /// <summary>
    /// Device is not detected and not connected.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Device detected via BLE advertisement but GATT connection not yet established.
    /// </summary>
    Advertising,

    /// <summary>
    /// Active GATT connection established with service discovery complete.
    /// </summary>
    GattConnected
}
