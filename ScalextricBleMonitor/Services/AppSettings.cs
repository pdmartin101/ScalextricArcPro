using System;
using System.IO;
using System.Text.Json;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Application settings that persist between sessions.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Power level for track power (0-63).
    /// </summary>
    public int PowerLevel { get; set; } = 63;

    /// <summary>
    /// Gets the path to the settings file.
    /// </summary>
    private static string SettingsFilePath
    {
        get
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "ScalextricBleMonitor");
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
                    return settings;
                }
            }
        }
        catch
        {
            // If loading fails, return defaults
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
        }
        catch
        {
            // Silently fail if we can't save settings
        }
    }
}
