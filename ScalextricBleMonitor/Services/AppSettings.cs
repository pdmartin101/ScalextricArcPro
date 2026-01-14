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
    /// Global power level for track power (0-63). Legacy, kept for backwards compatibility.
    /// </summary>
    public int PowerLevel { get; set; } = 63;

    /// <summary>
    /// Per-slot power levels (0-63). Array index 0 = slot 1, etc.
    /// </summary>
    public int[] SlotPowerLevels { get; set; } = [63, 63, 63, 63, 63, 63];

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

                    // Validate per-slot power levels
                    if (settings.SlotPowerLevels == null || settings.SlotPowerLevels.Length != 6)
                    {
                        settings.SlotPowerLevels = [63, 63, 63, 63, 63, 63];
                    }
                    else
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            settings.SlotPowerLevels[i] = Math.Clamp(settings.SlotPowerLevels[i], 0, 63);
                        }
                    }

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
