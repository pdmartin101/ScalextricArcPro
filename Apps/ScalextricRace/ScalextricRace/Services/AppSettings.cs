using System;
using System.IO;
using System.Text.Json;
using Serilog;

namespace ScalextricRace.Services;

/// <summary>
/// Application settings that persist between sessions.
/// Stored in %LocalAppData%/ScalextricRace/settings.json
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Whether track power should be enabled on startup.
    /// </summary>
    public bool PowerEnabled { get; set; } = false;

    /// <summary>
    /// Global power level for track power (0-63).
    /// </summary>
    public int PowerLevel { get; set; } = 63;

    /// <summary>
    /// The selected throttle profile type name.
    /// Valid values: "Linear", "Exponential", "Stepped"
    /// </summary>
    public string ThrottleProfile { get; set; } = "Linear";

    /// <summary>
    /// Gets the path to the settings file.
    /// </summary>
    private static string SettingsFilePath
    {
        get
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "ScalextricRace");
            return Path.Combine(appFolder, "settings.json");
        }
    }

    /// <summary>
    /// Loads settings from disk, or returns defaults if file doesn't exist.
    /// </summary>
    public static AppSettings Load()
    {
        try
        {
            var filePath = SettingsFilePath;
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    // Validate loaded values
                    settings.PowerLevel = Math.Clamp(settings.PowerLevel, 0, 63);

                    // Validate throttle profile
                    var validProfiles = new[] { "Linear", "Exponential", "Stepped" };
                    if (string.IsNullOrEmpty(settings.ThrottleProfile) ||
                        Array.IndexOf(validProfiles, settings.ThrottleProfile) < 0)
                    {
                        settings.ThrottleProfile = "Linear";
                    }

                    Log.Information("Settings loaded from {FilePath}", filePath);
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load settings, using defaults");
        }

        return new AppSettings();
    }

    /// <summary>
    /// Saves settings to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            var filePath = SettingsFilePath;
            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(filePath, json);

            Log.Debug("Settings saved to {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save settings");
        }
    }
}
