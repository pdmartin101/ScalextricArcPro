using System;
using System.IO;
using System.Text.Json;
using Scalextric;
using Serilog;

namespace ScalextricRace.Services;

/// <summary>
/// Startup settings for a single controller slot.
/// These are ultra-safe values loaded on app launch before any race/car/driver is selected.
/// </summary>
public class StartupSlotSettings
{
    /// <summary>
    /// Startup power level for this slot (0-63).
    /// </summary>
    public int PowerLevel { get; set; } = ScalextricProtocol.MaxPowerLevel;

    /// <summary>
    /// Startup throttle profile type name for this slot.
    /// </summary>
    public string ThrottleProfile { get; set; } = "Linear";
}

/// <summary>
/// Startup power and throttle settings loaded on app launch.
/// These are ultra-safe values used before any race/car/driver is selected.
/// Runtime power is calculated from Car and Driver settings when assigned.
/// </summary>
public class StartupSettings
{
    /// <summary>
    /// Startup global power level for track power (0-63).
    /// Used when UsePerSlotPower is false.
    /// </summary>
    public int PowerLevel { get; set; } = ScalextricProtocol.MaxPowerLevel;

    /// <summary>
    /// Startup throttle profile type name.
    /// Valid values: "Linear", "Exponential", "Stepped"
    /// Used when UsePerSlotPower is false.
    /// </summary>
    public string ThrottleProfile { get; set; } = "Linear";

    /// <summary>
    /// Whether per-slot power mode is enabled on startup.
    /// When true, each slot can have individual power settings.
    /// </summary>
    public bool UsePerSlotPower { get; set; } = false;

    /// <summary>
    /// Startup per-slot settings (indexed 0-5 for slots 1-6).
    /// Used when UsePerSlotPower is true.
    /// </summary>
    public StartupSlotSettings[] SlotSettings { get; set; } = CreateStartupSlotSettings();

    private static StartupSlotSettings[] CreateStartupSlotSettings()
    {
        return new StartupSlotSettings[]
        {
            new() { PowerLevel = ScalextricProtocol.MaxPowerLevel, ThrottleProfile = "Linear" },
            new() { PowerLevel = ScalextricProtocol.MaxPowerLevel, ThrottleProfile = "Linear" },
            new() { PowerLevel = ScalextricProtocol.MaxPowerLevel, ThrottleProfile = "Linear" },
            new() { PowerLevel = ScalextricProtocol.MaxPowerLevel, ThrottleProfile = "Linear" },
            new() { PowerLevel = ScalextricProtocol.MaxPowerLevel, ThrottleProfile = "Linear" },
            new() { PowerLevel = ScalextricProtocol.MaxPowerLevel, ThrottleProfile = "Linear" }
        };
    }
}

/// <summary>
/// Application settings that persist between sessions.
/// Stored in %LocalAppData%/ScalextricPdm/ScalextricRace/settings.json
/// </summary>
public class AppSettings : IAppSettings
{
    /// <summary>
    /// Whether track power should be enabled automatically on startup after connection.
    /// </summary>
    public bool StartWithPowerEnabled { get; set; } = false;

    /// <summary>
    /// Startup power and throttle settings loaded on app launch.
    /// These are ultra-safe values used before any race/car/driver is selected.
    /// </summary>
    public StartupSettings Startup { get; set; } = new();

    /// <summary>
    /// Gets the base application data folder path.
    /// </summary>
    public static string AppDataFolder
    {
        get
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appDataPath, "ScalextricPdm", "ScalextricRace");
        }
    }

    /// <summary>
    /// Gets the path to the Images folder for storing car images.
    /// </summary>
    public static string ImagesFolder => Path.Combine(AppDataFolder, "Images");

    /// <summary>
    /// Gets the path to the settings file.
    /// </summary>
    private static string SettingsFilePath => Path.Combine(AppDataFolder, "settings.json");

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
                    // Ensure Startup object exists
                    settings.Startup ??= new StartupSettings();

                    // Validate loaded values
                    settings.Startup.PowerLevel = Math.Clamp(settings.Startup.PowerLevel, ScalextricProtocol.MinPowerLevel, ScalextricProtocol.MaxPowerLevel);

                    // Validate throttle profile
                    var validProfiles = new[] { "Linear", "Exponential", "Stepped" };
                    if (string.IsNullOrEmpty(settings.Startup.ThrottleProfile) ||
                        Array.IndexOf(validProfiles, settings.Startup.ThrottleProfile) < 0)
                    {
                        settings.Startup.ThrottleProfile = "Linear";
                    }

                    // Ensure SlotSettings array has 6 elements
                    if (settings.Startup.SlotSettings == null || settings.Startup.SlotSettings.Length != ScalextricProtocol.SlotCount)
                    {
                        settings.Startup.SlotSettings = new StartupSlotSettings[]
                        {
                            new() { PowerLevel = ScalextricProtocol.MaxPowerLevel, ThrottleProfile = "Linear" },
                            new() { PowerLevel = ScalextricProtocol.MaxPowerLevel, ThrottleProfile = "Linear" },
                            new() { PowerLevel = ScalextricProtocol.MaxPowerLevel, ThrottleProfile = "Linear" },
                            new() { PowerLevel = ScalextricProtocol.MaxPowerLevel, ThrottleProfile = "Linear" },
                            new() { PowerLevel = ScalextricProtocol.MaxPowerLevel, ThrottleProfile = "Linear" },
                            new() { PowerLevel = ScalextricProtocol.MaxPowerLevel, ThrottleProfile = "Linear" }
                        };
                    }
                    else
                    {
                        // Validate each slot's settings
                        foreach (var slot in settings.Startup.SlotSettings)
                        {
                            slot.PowerLevel = Math.Clamp(slot.PowerLevel, ScalextricProtocol.MinPowerLevel, ScalextricProtocol.MaxPowerLevel);
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
