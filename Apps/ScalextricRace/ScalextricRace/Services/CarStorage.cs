using System.Text.Json;
using ScalextricRace.Models;
using Serilog;

namespace ScalextricRace.Services;

/// <summary>
/// Handles persistence of car data to JSON file.
/// Stored in %LocalAppData%/ScalextricPdm/ScalextricRace/cars.json
/// </summary>
public class CarStorage : ICarStorage
{
    /// <summary>
    /// Gets the path to the cars file.
    /// </summary>
    private static string CarsFilePath
    {
        get
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "ScalextricPdm", "ScalextricRace");
            return Path.Combine(appFolder, "cars.json");
        }
    }

    /// <summary>
    /// Loads all cars from disk.
    /// Returns empty list if file doesn't exist (default car will be added by caller).
    /// </summary>
    public List<Car> Load()
    {
        try
        {
            var filePath = CarsFilePath;
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var cars = JsonSerializer.Deserialize<List<Car>>(json);
                if (cars != null)
                {
                    // Validate loaded values
                    foreach (var car in cars)
                    {
                        car.DefaultPower = Math.Clamp(car.DefaultPower, 0, 63);
                        car.GhostMaxPower = Math.Clamp(car.GhostMaxPower, 0, 63);
                        car.MinPower = Math.Clamp(car.MinPower, 0, 63);
                    }

                    Log.Information("Loaded {Count} cars from {FilePath}", cars.Count, filePath);
                    return cars;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load cars, returning empty list");
        }

        return [];
    }

    /// <summary>
    /// Saves all cars to disk.
    /// </summary>
    /// <param name="cars">The cars to save.</param>
    public void Save(IEnumerable<Car> cars)
    {
        try
        {
            var filePath = CarsFilePath;
            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(cars.ToList(), options);
            File.WriteAllText(filePath, json);

            Log.Debug("Saved {Count} cars to {FilePath}", cars.Count(), filePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save cars");
        }
    }
}
