using System.Text.Json;
using ScalextricRace.Models;
using Serilog;

namespace ScalextricRace.Services;

/// <summary>
/// Handles persistence of driver data to JSON file.
/// Stored in %LocalAppData%/ScalextricPdm/ScalextricRace/drivers.json
/// </summary>
public class DriverStorage : IDriverStorage
{
    /// <summary>
    /// Gets the path to the drivers file.
    /// </summary>
    private static string DriversFilePath => Path.Combine(AppSettings.AppDataFolder, "drivers.json");

    /// <summary>
    /// Loads all drivers from disk.
    /// Returns empty list if file doesn't exist (default driver will be added by caller).
    /// </summary>
    public List<Driver> Load()
    {
        try
        {
            var filePath = DriversFilePath;
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var drivers = JsonSerializer.Deserialize<List<Driver>>(json);
                if (drivers != null)
                {
                    // Validate loaded values
                    foreach (var driver in drivers)
                    {
                        if (driver.PowerPercentage.HasValue)
                        {
                            driver.PowerPercentage = Math.Clamp(driver.PowerPercentage.Value, 50, 100);
                        }
                    }

                    Log.Information("Loaded {Count} drivers from {FilePath}", drivers.Count, filePath);
                    return drivers;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load drivers, returning empty list");
        }

        return [];
    }

    /// <summary>
    /// Saves all drivers to disk.
    /// </summary>
    /// <param name="drivers">The drivers to save.</param>
    public void Save(IEnumerable<Driver> drivers)
    {
        try
        {
            var filePath = DriversFilePath;
            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(drivers.ToList(), options);
            File.WriteAllText(filePath, json);

            Log.Debug("Saved {Count} drivers to {FilePath}", drivers.Count(), filePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save drivers");
        }
    }
}
