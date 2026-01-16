using System;
using System.Threading.Tasks;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Interface for BLE monitoring service.
/// Extends the shared IBleService with additional methods for the monitor app.
/// </summary>
public interface IBleMonitorService : ScalextricBle.IBleService
{
    /// <summary>
    /// Writes a value to a characteristic asynchronously and waits for completion.
    /// This is an alias for WriteCharacteristicAsync with a different name for compatibility.
    /// </summary>
    /// <param name="characteristicUuid">The characteristic UUID to write to.</param>
    /// <param name="data">The data to write.</param>
    /// <returns>True if the write succeeded, false otherwise.</returns>
    Task<bool> WriteCharacteristicAwaitAsync(Guid characteristicUuid, byte[] data);
}
