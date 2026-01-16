namespace ScalextricRace.Models;

/// <summary>
/// Represents the BLE connection state for UI display.
/// </summary>
public enum ConnectionState
{
    /// <summary>
    /// Not scanning and not connected.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Scanning for devices or connecting to a detected device.
    /// </summary>
    Connecting,

    /// <summary>
    /// GATT connection established and ready.
    /// </summary>
    Connected
}
