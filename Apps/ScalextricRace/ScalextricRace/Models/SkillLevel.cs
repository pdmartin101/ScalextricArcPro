using System.Text.Json;
using Serilog;

namespace ScalextricRace.Models;

/// <summary>
/// Represents a driver skill level with a name and power limit.
/// </summary>
public class SkillLevel
{
    /// <summary>
    /// Display name for the skill level (e.g., "Beginner", "Intermediate").
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Power limit for this skill level (0-63).
    /// 63 means no limit (full power available).
    /// </summary>
    public int PowerLimit { get; set; } = 63;

    /// <summary>
    /// Gets whether this skill level has no power restriction.
    /// </summary>
    public bool IsNoLimit => PowerLimit >= 63;

    /// <summary>
    /// Gets the display text showing name and power limit value.
    /// </summary>
    public string DisplayText => IsNoLimit ? Name : $"{Name} ({PowerLimit})";
}

/// <summary>
/// Configuration for driver skill levels.
/// Loaded from skill-levels.json, with defaults if file doesn't exist.
/// </summary>
public class SkillLevelConfig
{
    /// <summary>
    /// The list of available skill levels.
    /// </summary>
    public List<SkillLevel> Levels { get; set; } = [];

    /// <summary>
    /// Gets the path to the skill levels config file.
    /// </summary>
    private static string ConfigFilePath
    {
        get
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "ScalextricPdm", "ScalextricRace");
            return Path.Combine(appFolder, "skill-levels.json");
        }
    }

    /// <summary>
    /// Creates the default skill level configuration.
    /// </summary>
    public static SkillLevelConfig CreateDefault()
    {
        return new SkillLevelConfig
        {
            Levels =
            [
                new SkillLevel { Name = "Beginner", PowerLimit = 25 },
                new SkillLevel { Name = "Intermediate", PowerLimit = 40 },
                new SkillLevel { Name = "Experienced", PowerLimit = 55 },
                new SkillLevel { Name = "No Limit", PowerLimit = 63 }
            ]
        };
    }

    /// <summary>
    /// Loads skill level config from disk, or returns defaults if file doesn't exist.
    /// Also saves defaults if file doesn't exist, so user can customize.
    /// </summary>
    public static SkillLevelConfig Load()
    {
        try
        {
            var filePath = ConfigFilePath;
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var config = JsonSerializer.Deserialize<SkillLevelConfig>(json);
                if (config != null && config.Levels.Count > 0)
                {
                    // Validate loaded values
                    foreach (var level in config.Levels)
                    {
                        level.PowerLimit = Math.Clamp(level.PowerLimit, 0, 63);
                    }

                    Log.Information("Loaded {Count} skill levels from {FilePath}", config.Levels.Count, filePath);
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load skill levels, using defaults");
        }

        // Create and save defaults
        var defaults = CreateDefault();
        defaults.Save();
        return defaults;
    }

    /// <summary>
    /// Saves the skill level config to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            var filePath = ConfigFilePath;
            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(filePath, json);

            Log.Debug("Saved skill levels to {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save skill levels");
        }
    }

    /// <summary>
    /// Gets the skill level name for a given power limit value.
    /// Returns the closest matching level or the exact value if no match.
    /// </summary>
    public string GetLevelName(int? powerLimit)
    {
        if (!powerLimit.HasValue || powerLimit >= 63)
        {
            // Find the "no limit" level name, or use default
            var noLimitLevel = Levels.FirstOrDefault(l => l.IsNoLimit);
            return noLimitLevel?.Name ?? "No Limit";
        }

        // Find exact match first
        var exactMatch = Levels.FirstOrDefault(l => l.PowerLimit == powerLimit.Value);
        if (exactMatch != null)
        {
            return exactMatch.Name;
        }

        // No exact match - show the value
        return powerLimit.Value.ToString();
    }
}
