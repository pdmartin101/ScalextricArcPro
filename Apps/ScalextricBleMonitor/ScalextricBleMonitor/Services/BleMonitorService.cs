using System;
using System.Threading.Tasks;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// BLE monitoring service for ScalextricBleMonitor app.
/// Extends the shared BleService with the WriteCharacteristicAwaitAsync method alias.
/// </summary>
public class BleMonitorService : ScalextricBle.BleService, IBleMonitorService
{
    /// <inheritdoc />
    public Task<bool> WriteCharacteristicAwaitAsync(Guid characteristicUuid, byte[] data)
    {
        return WriteCharacteristicAsync(characteristicUuid, data);
    }
}
