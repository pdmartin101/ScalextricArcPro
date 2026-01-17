using ScalextricRace.Models;

namespace ScalextricRace.Services;

/// <summary>
/// Handles persistence of driver data to JSON file.
/// Stored in %LocalAppData%/ScalextricPdm/ScalextricRace/drivers.json
/// </summary>
public class DriverStorage : JsonStorageBase<Driver>, IDriverStorage
{
    /// <inheritdoc />
    protected override string FileName => "drivers.json";

    /// <inheritdoc />
    protected override string EntityName => "drivers";

    /// <inheritdoc />
    protected override void ValidateItems(List<Driver> items)
    {
        foreach (var driver in items)
        {
            if (driver.PowerPercentage.HasValue)
            {
                driver.PowerPercentage = Math.Clamp(driver.PowerPercentage.Value, 50, 100);
            }
        }
    }
}
