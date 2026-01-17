using ScalextricRace.Models;

namespace ScalextricRace.Services;

/// <summary>
/// Interface for skill level configuration service.
/// Enables unit test mocking and separates persistence from the model.
/// </summary>
public interface ISkillLevelConfigService
{
    /// <summary>
    /// Gets the current skill level configuration.
    /// </summary>
    SkillLevelConfig Config { get; }

    /// <summary>
    /// Loads or reloads the skill level configuration from disk.
    /// </summary>
    /// <returns>The loaded configuration.</returns>
    SkillLevelConfig Load();

    /// <summary>
    /// Saves the current skill level configuration to disk.
    /// </summary>
    void Save();

    /// <summary>
    /// Gets the skill level name for a given power limit value.
    /// </summary>
    /// <param name="powerLimit">The power limit value.</param>
    /// <returns>The skill level name.</returns>
    string GetLevelName(int? powerLimit);
}
