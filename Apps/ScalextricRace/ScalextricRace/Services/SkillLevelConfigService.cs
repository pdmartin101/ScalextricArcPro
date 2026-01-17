using ScalextricRace.Models;

namespace ScalextricRace.Services;

/// <summary>
/// Service for managing skill level configuration.
/// Wraps SkillLevelConfig and provides persistence operations.
/// </summary>
public class SkillLevelConfigService : ISkillLevelConfigService
{
    private SkillLevelConfig _config;

    /// <summary>
    /// Creates a new instance and loads the configuration.
    /// </summary>
    public SkillLevelConfigService()
    {
        _config = SkillLevelConfig.Load();
    }

    /// <inheritdoc />
    public SkillLevelConfig Config => _config;

    /// <inheritdoc />
    public SkillLevelConfig Load()
    {
        _config = SkillLevelConfig.Load();
        return _config;
    }

    /// <inheritdoc />
    public void Save()
    {
        _config.Save();
    }

    /// <inheritdoc />
    public string GetLevelName(int? powerLimit)
    {
        return _config.GetLevelName(powerLimit);
    }
}
