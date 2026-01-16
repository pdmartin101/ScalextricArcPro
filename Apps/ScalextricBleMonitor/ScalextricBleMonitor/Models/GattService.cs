using System;
using System.Collections.Generic;

namespace ScalextricBleMonitor.Models;

/// <summary>
/// Represents a Bluetooth GATT service discovered on a device.
/// This is a pure domain model with no UI or framework dependencies.
/// </summary>
public class GattService
{
    /// <summary>
    /// The UUID of the service.
    /// </summary>
    public Guid Uuid { get; set; }

    /// <summary>
    /// The display name of the service (either well-known name or UUID string).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The characteristics belonging to this service.
    /// </summary>
    public List<GattCharacteristic> Characteristics { get; set; } = [];
}
