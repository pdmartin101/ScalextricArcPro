using ScalextricRace.Models;

namespace ScalextricRace.Services;

/// <summary>
/// Interface for car storage, enabling unit test mocking.
/// </summary>
public interface ICarStorage
{
    /// <summary>
    /// Loads all cars from disk.
    /// </summary>
    /// <returns>List of cars, empty if file doesn't exist.</returns>
    List<Car> Load();

    /// <summary>
    /// Saves all cars to disk.
    /// </summary>
    /// <param name="cars">The cars to save.</param>
    void Save(IEnumerable<Car> cars);
}
