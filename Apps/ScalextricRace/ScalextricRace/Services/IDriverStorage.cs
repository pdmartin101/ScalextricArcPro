using ScalextricRace.Models;

namespace ScalextricRace.Services;

/// <summary>
/// Interface for driver storage, enabling unit test mocking.
/// </summary>
public interface IDriverStorage
{
    /// <summary>
    /// Loads all drivers from disk.
    /// </summary>
    /// <returns>List of drivers, empty if file doesn't exist.</returns>
    List<Driver> Load();

    /// <summary>
    /// Saves all drivers to disk.
    /// </summary>
    /// <param name="drivers">The drivers to save.</param>
    void Save(IEnumerable<Driver> drivers);
}
