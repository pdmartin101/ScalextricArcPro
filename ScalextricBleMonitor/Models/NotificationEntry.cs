using System;

namespace ScalextricBleMonitor.Models;

/// <summary>
/// Represents a single BLE notification received from the device.
/// This is a pure domain model with no UI or framework dependencies.
/// </summary>
public class NotificationEntry
{
    /// <summary>
    /// The timestamp when the notification was received.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// The UUID of the service that sent the notification.
    /// </summary>
    public Guid ServiceUuid { get; set; }

    /// <summary>
    /// The UUID of the characteristic that sent the notification.
    /// </summary>
    public Guid CharacteristicUuid { get; set; }

    /// <summary>
    /// The display name of the characteristic.
    /// </summary>
    public string CharacteristicName { get; set; } = string.Empty;

    /// <summary>
    /// The raw notification data bytes.
    /// </summary>
    public byte[] Data { get; set; } = [];

    /// <summary>
    /// Human-readable decoded representation of the data.
    /// </summary>
    public string DecodedValue { get; set; } = string.Empty;
}
