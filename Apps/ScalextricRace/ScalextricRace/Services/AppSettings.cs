using System;
using System.IO;
using System.Text.Json;
using Serilog;

namespace ScalextricRace.Services;

/// <summary>
/// Default settings for a single controller slot.
/// These values are used as initial defaults for new races/sessions.
/// </summary>
public class DefaultSlotSettings
{
    /// <summary>
    /// Default power level for this slot (0-63).
    /// </summary>
    public int PowerLevel { get; set; } = 63;

    /// <summary>
    /// Default throttle profile type name for this slot.
    /// </summary>
    public string ThrottleProfile { get; set; } = "Linear";
}

/// <summary>
/// Default power and throttle settings used as initial values for new races/sessions.
/// These can be overridden by race-specific or session-specific settings.
/// </summary>
public class DefaultSettings
{
    /// <summary>
    /// Default global power level for track power (0-63).
    /// Used when IsPerSlotPowerMode is false.
    /// </summary>
    public int PowerLevel { get; set; } = 63;

    /// <summary>
    /// Default throttle profile type name.
    /// Valid values: "Linear", "Exponential", "Stepped"
    /// Used when IsPerSlotPowerMode is false.
    /// </summary>
    public string ThrottleProfile { get; set; } = "Linear";

    /// <summary>
    /// Whether per-slot power mode is enabled by default.
    /// When true, each slot can have individual power and throttle settings.
    /// </summary>
    public bool IsPerSlotPowerMode { get; set; } = false;

    /// <summary>
    /// Default per-slot settings (indexed 0-5 for slots 1-6).
    /// Used when IsPerSlotPowerMode is true.
    /// </summary>
    public DefaultSlotSettings[] SlotSettings { get; set; } = CreateDefaultSlotSettings();

    private static DefaultSlotSettings[] CreateDefaultSlotSettings()
    {
        return new DefaultSlotSettings[]
        {
            new() { PowerLevel = 63, ThrottleProfile = "Linear" },
            new() { PowerLevel = 63, ThrottleProfile = "Linear" },
            new() { PowerLevel = 63, ThrottleProfile = "Linear" },
            new() { PowerLevel = 63, ThrottleProfile = "Linear" },
            new() { PowerLevel = 63, ThrottleProfile = "Linear" },
            new() { PowerLevel = 63, ThrottleProfile = "Linear" }
        };
    }
}

/// <summary>
/// Application settings that persist between sessions.
/// Stored in %LocalAppData%/ScalextricPdm/ScalextricRace/settings.json
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Whether track power should be enabled automatically on startup after connection.
    /// </summary>
    public bool StartWithPowerEnabled { get; set; } = false;

    /// <summary>
    /// Default power and throttle settings used as initial values.
    /// </summary>
    public DefaultSettings Defaults { get; set; } = new();

    /// <summary>
    /// Gets the path to the settings file.
    /// </summary>
    private static string SettingsFilePath
    {
        get
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "ScalextricPdm", "ScalextricRace");
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
                    // Ensure Defaults object exists
                    settings.Defaults ??= new DefaultSettings();

                    // Validate loaded values
                    settings.Defaults.PowerLevel = Math.Clamp(settings.Defaults.PowerLevel, 0, 63);

                    // Validate throttle profile
                    var validProfiles = new[] { "Linear", "Exponential", "Stepped" };
                    if (string.IsNullOrEmpty(settings.Defaults.ThrottleProfile) ||
                        Array.IndexOf(validProfiles, settings.Defaults.ThrottleProfile) < 0)
                    {
                        settings.Defaults.ThrottleProfile = "Linear";
                    }

                    // Ensure SlotSettings array has 6 elements
                    if (settings.Defaults.SlotSettings == null || settings.Defaults.SlotSettings.Length != 6)
                    {
                        settings.Defaults.SlotSettings = new DefaultSlotSettings[]
                        {
                            new() { PowerLevel = 63, ThrottleProfile = "Linear" },
                            new() { PowerLevel = 63, ThrottleProfile = "Linear" },
                            new() { PowerLevel = 63, ThrottleProfile = "Linear" },
                            new() { PowerLevel = 63, ThrottleProfile = "Linear" },
                            new() { PowerLevel = 63, ThrottleProfile = "Linear" },
                            new() { PowerLevel = 63, ThrottleProfile = "Linear" }
                        };
                    }
                    else
                    {
                        // Validate each slot's settings
                        foreach (var slot in settings.Defaults.SlotSettings)
                        {
                            slot.PowerLevel = Math.Clamp(slot.PowerLevel, 0, 63);
                            if (string.IsNullOrEmpty(slot.ThrottleProfile) ||
                                Array.IndexOf(validProfiles, slot.ThrottleProfile) < 0)
                            {
                                slot.ThrottleProfile = "Linear";
                            }
                        }
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
