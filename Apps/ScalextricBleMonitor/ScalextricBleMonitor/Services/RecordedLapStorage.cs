using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ScalextricBleMonitor.Models;
using Serilog;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Handles persistence of recorded laps to JSON files.
/// Stores all recorded laps in a single file in the user's local app data folder.
/// </summary>
public static class RecordedLapStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Gets the path to the recorded laps file.
    /// </summary>
    private static string RecordedLapsFilePath
    {
        get
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "ScalextricPdm", "ScalextricBleMonitor");
            return Path.Combine(appFolder, "recorded_laps.json");
        }
    }

    /// <summary>
    /// Loads all recorded laps from disk.
    /// </summary>
    /// <returns>List of recorded laps, or empty list if file doesn't exist or is invalid.</returns>
    public static List<RecordedLap> Load()
    {
        try
        {
            var filePath = RecordedLapsFilePath;
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var laps = JsonSerializer.Deserialize<List<RecordedLap>>(json, JsonOptions);
                if (laps != null)
                {
                    Log.Information("Loaded {Count} recorded laps from {Path}", laps.Count, filePath);
                    return laps;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load recorded laps from disk");
        }

        return [];
    }

    /// <summary>
    /// Saves all recorded laps to disk.
    /// </summary>
    /// <param name="laps">The list of recorded laps to save.</param>
    public static void Save(IEnumerable<RecordedLap> laps)
    {
        try
        {
            var filePath = RecordedLapsFilePath;
            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var lapList = new List<RecordedLap>(laps);
            var json = JsonSerializer.Serialize(lapList, JsonOptions);
            File.WriteAllText(filePath, json);

            Log.Debug("Saved {Count} recorded laps to {Path}", lapList.Count, filePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save recorded laps to disk");
        }
    }
}
