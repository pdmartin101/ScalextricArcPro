using System;

namespace ScalextricBleMonitor.Models;

/// <summary>
/// Represents a Bluetooth GATT characteristic discovered on a service.
/// This is a pure domain model with no UI or framework dependencies.
/// </summary>
public class GattCharacteristic
{
    /// <summary>
    /// The UUID of the characteristic.
    /// </summary>
    public Guid Uuid { get; set; }

    /// <summary>
    /// The UUID of the parent service.
    /// </summary>
    public Guid ServiceUuid { get; set; }

    /// <summary>
    /// The display name of the characteristic (either well-known name or UUID string).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable string of characteristic properties (e.g., "R, W, N").
    /// </summary>
    public string Properties { get; set; } = string.Empty;

    /// <summary>
    /// The last value read from this characteristic, if any.
    /// </summary>
    public byte[]? LastValue { get; set; }

    /// <summary>
    /// Human-readable representation of the last value.
    /// </summary>
    public string? LastValueDisplay { get; set; }
}
