using System;
using System.IO;
using System.Text.Json;
using Scalextric;
using Serilog;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Application settings that persist between sessions.
/// </summary>
public class AppSettings : IAppSettings
{
    /// <summary>
    /// Global power level for track power (0-63). Legacy, kept for backwards compatibility.
    /// </summary>
    public int PowerLevel { get; set; } = ScalextricProtocol.MaxPowerLevel;

    /// <summary>
    /// Per-slot power levels (0-63). Array index 0 = slot 1, etc.
    /// </summary>
    public int[] SlotPowerLevels { get; set; } = [ScalextricProtocol.MaxPowerLevel, ScalextricProtocol.MaxPowerLevel, ScalextricProtocol.MaxPowerLevel, ScalextricProtocol.MaxPowerLevel, ScalextricProtocol.MaxPowerLevel, ScalextricProtocol.MaxPowerLevel];

    /// <summary>
    /// When true, use individual per-slot power levels. When false, use global PowerLevel for all slots.
    /// </summary>
    public bool UsePerSlotPower { get; set; } = true;

    /// <summary>
    /// Per-slot ghost mode settings. Array index 0 = slot 1, etc.
    /// When true, the slot operates in ghost mode (power level becomes direct throttle index).
    /// </summary>
    public bool[] SlotGhostModes { get; set; } = [false, false, false, false, false, false];

    /// <summary>
    /// Per-slot ghost throttle levels (0-63). Array index 0 = slot 1, etc.
    /// When ghost mode is enabled, this value is sent as the direct throttle index.
    /// Defaults to 0 (stopped) for safety.
    /// </summary>
    public int[] SlotGhostThrottleLevels { get; set; } = [0, 0, 0, 0, 0, 0];

    /// <summary>
    /// Per-slot throttle profile type names. Array index 0 = slot 1, etc.
    /// Valid values: "Linear", "Exponential", "Stepped"
    /// </summary>
    public string[] SlotThrottleProfiles { get; set; } = ["Linear", "Linear", "Linear", "Linear", "Linear", "Linear"];

    /// <summary>
    /// Per-slot ghost source type names. Array index 0 = slot 1, etc.
    /// Valid values: "FixedSpeed", "RecordedLap"
    /// </summary>
    public string[] SlotGhostSources { get; set; } = ["FixedSpeed", "FixedSpeed", "FixedSpeed", "FixedSpeed", "FixedSpeed", "FixedSpeed"];

    /// <summary>
    /// Gets the path to the settings file.
    /// </summary>
    private static string SettingsFilePath
    {
        get
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "ScalextricPdm", "ScalextricBleMonitor");
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
                    settings.PowerLevel = Math.Clamp(settings.PowerLevel, ScalextricProtocol.MinPowerLevel, ScalextricProtocol.MaxPowerLevel);

                    // Validate per-slot power levels
                    if (settings.SlotPowerLevels == null || settings.SlotPowerLevels.Length != ScalextricProtocol.SlotCount)
                    {
                        settings.SlotPowerLevels = [ScalextricProtocol.MaxPowerLevel, ScalextricProtocol.MaxPowerLevel, ScalextricProtocol.MaxPowerLevel, ScalextricProtocol.MaxPowerLevel, ScalextricProtocol.MaxPowerLevel, ScalextricProtocol.MaxPowerLevel];
                    }
                    else
                    {
                        for (int i = 0; i < ScalextricProtocol.SlotCount; i++)
                        {
                            settings.SlotPowerLevels[i] = Math.Clamp(settings.SlotPowerLevels[i], ScalextricProtocol.MinPowerLevel, ScalextricProtocol.MaxPowerLevel);
                        }
                    }

                    // Validate per-slot ghost modes
                    if (settings.SlotGhostModes == null || settings.SlotGhostModes.Length != ScalextricProtocol.SlotCount)
                    {
                        settings.SlotGhostModes = [false, false, false, false, false, false];
                    }

                    // Validate per-slot ghost throttle levels
                    if (settings.SlotGhostThrottleLevels == null || settings.SlotGhostThrottleLevels.Length != ScalextricProtocol.SlotCount)
                    {
                        settings.SlotGhostThrottleLevels = [0, 0, 0, 0, 0, 0];
                    }
                    else
                    {
                        for (int i = 0; i < ScalextricProtocol.SlotCount; i++)
                        {
                            settings.SlotGhostThrottleLevels[i] = Math.Clamp(settings.SlotGhostThrottleLevels[i], ScalextricProtocol.MinPowerLevel, ScalextricProtocol.MaxPowerLevel);
                        }
                    }

                    // Validate per-slot throttle profiles
                    if (settings.SlotThrottleProfiles == null || settings.SlotThrottleProfiles.Length != ScalextricProtocol.SlotCount)
                    {
                        settings.SlotThrottleProfiles = ["Linear", "Linear", "Linear", "Linear", "Linear", "Linear"];
                    }
                    else
                    {
                        // Ensure each value is a valid profile name
                        var validProfiles = new[] { "Linear", "Exponential", "Stepped" };
                        for (int i = 0; i < ScalextricProtocol.SlotCount; i++)
                        {
                            if (string.IsNullOrEmpty(settings.SlotThrottleProfiles[i]) ||
                                Array.IndexOf(validProfiles, settings.SlotThrottleProfiles[i]) < 0)
                            {
                                settings.SlotThrottleProfiles[i] = "Linear";
                            }
                        }
                    }

                    // Validate per-slot ghost sources
                    if (settings.SlotGhostSources == null || settings.SlotGhostSources.Length != ScalextricProtocol.SlotCount)
                    {
                        settings.SlotGhostSources = ["FixedSpeed", "FixedSpeed", "FixedSpeed", "FixedSpeed", "FixedSpeed", "FixedSpeed"];
                    }
                    else
                    {
                        // Ensure each value is a valid ghost source name
                        var validSources = new[] { "FixedSpeed", "RecordedLap" };
                        for (int i = 0; i < ScalextricProtocol.SlotCount; i++)
                        {
                            if (string.IsNullOrEmpty(settings.SlotGhostSources[i]) ||
                                Array.IndexOf(validSources, settings.SlotGhostSources[i]) < 0)
                            {
                                settings.SlotGhostSources[i] = "FixedSpeed";
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
