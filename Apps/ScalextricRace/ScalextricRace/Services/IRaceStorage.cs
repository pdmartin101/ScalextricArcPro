using ScalextricRace.Models;

namespace ScalextricRace.Services;

/// <summary>
/// Interface for race storage, enabling unit test mocking.
/// </summary>
public interface IRaceStorage
{
    /// <summary>
    /// Loads all races from disk.
    /// </summary>
    /// <returns>List of races, empty if file doesn't exist.</returns>
    List<Race> Load();

    /// <summary>
    /// Saves all races to disk.
    /// </summary>
    /// <param name="races">The races to save.</param>
    void Save(IEnumerable<Race> races);
}
