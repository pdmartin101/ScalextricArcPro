using System.Text.Json;
using ScalextricRace.Models;
using Serilog;

namespace ScalextricRace.Services;

/// <summary>
/// Handles persistence of race data to JSON file.
/// Stored in %LocalAppData%/ScalextricPdm/ScalextricRace/races.json
/// </summary>
public class RaceStorage : IRaceStorage
{
    /// <summary>
    /// Gets the path to the races file.
    /// </summary>
    private static string RacesFilePath => Path.Combine(AppSettings.AppDataFolder, "races.json");

    /// <summary>
    /// Loads all races from disk.
    /// Returns empty list if file doesn't exist (default race will be added by caller).
    /// </summary>
    public List<Race> Load()
    {
        try
        {
            var filePath = RacesFilePath;
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var races = JsonSerializer.Deserialize<List<Race>>(json);
                if (races != null)
                {
                    // Validate loaded values
                    foreach (var race in races)
                    {
                        ValidateStage(race.FreePractice);
                        ValidateStage(race.Qualifying);
                        ValidateStage(race.RaceStage);
                    }

                    Log.Information("Loaded {Count} races from {FilePath}", races.Count, filePath);
                    return races;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load races, returning empty list");
        }

        return [];
    }

    /// <summary>
    /// Saves all races to disk.
    /// </summary>
    /// <param name="races">The races to save.</param>
    public void Save(IEnumerable<Race> races)
    {
        try
        {
            var filePath = RacesFilePath;
            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(races.ToList(), options);
            File.WriteAllText(filePath, json);

            Log.Debug("Saved {Count} races to {FilePath}", races.Count(), filePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save races");
        }
    }

    private static void ValidateStage(RaceStage stage)
    {
        stage.LapCount = Math.Max(1, stage.LapCount);
        stage.TimeMinutes = Math.Max(1, stage.TimeMinutes);
    }
}
