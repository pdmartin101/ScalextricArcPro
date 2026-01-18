using Scalextric;
using ScalextricRace.Models;

namespace ScalextricRace.Services;

/// <summary>
/// Handles persistence of car data to JSON file.
/// Stored in %LocalAppData%/ScalextricPdm/ScalextricRace/cars.json
/// </summary>
public class CarStorage : JsonStorageBase<Car>, ICarStorage
{
    /// <inheritdoc />
    protected override string FileName => "cars.json";

    /// <inheritdoc />
    protected override string EntityName => "cars";

    /// <inheritdoc />
    protected override void ValidateItems(List<Car> items)
    {
        foreach (var car in items)
        {
            car.DefaultPower = Math.Clamp(car.DefaultPower, ScalextricProtocol.MinPowerLevel, ScalextricProtocol.MaxPowerLevel);
            car.GhostMaxPower = Math.Clamp(car.GhostMaxPower, ScalextricProtocol.MinPowerLevel, ScalextricProtocol.MaxPowerLevel);
            car.MinPower = Math.Clamp(car.MinPower, ScalextricProtocol.MinPowerLevel, ScalextricProtocol.MaxPowerLevel);
        }
    }
}
