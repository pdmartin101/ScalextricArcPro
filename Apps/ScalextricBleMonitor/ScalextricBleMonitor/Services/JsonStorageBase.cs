using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Serilog;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Base class for JSON file storage services.
/// Provides common Load/Save functionality with proper error handling and logging.
/// </summary>
/// <typeparam name="T">The type of entity to store.</typeparam>
public abstract class JsonStorageBase<T>
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// Gets the file name (e.g., "recorded_laps.json").
    /// </summary>
    protected abstract string FileName { get; }

    /// <summary>
    /// Gets the entity name for logging (e.g., "recorded laps").
    /// </summary>
    protected abstract string EntityName { get; }

    /// <summary>
    /// Gets the base application data folder path.
    /// </summary>
    protected static string AppDataFolder
    {
        get
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appDataPath, "ScalextricPdm", "ScalextricBleMonitor");
        }
    }

    /// <summary>
    /// Gets the full path to the storage file.
    /// </summary>
    protected string FilePath => Path.Combine(AppDataFolder, FileName);

    /// <summary>
    /// Validates loaded entities. Override to add entity-specific validation.
    /// </summary>
    /// <param name="items">The items to validate.</param>
    protected virtual void ValidateItems(List<T> items) { }

    /// <summary>
    /// Loads all entities from disk.
    /// Returns empty list if file doesn't exist or is invalid.
    /// </summary>
    public List<T> Load()
    {
        try
        {
            var filePath = FilePath;
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var items = JsonSerializer.Deserialize<List<T>>(json, WriteOptions);
                if (items != null)
                {
                    ValidateItems(items);
                    Log.Information("Loaded {Count} {EntityName} from {FilePath}", items.Count, EntityName, filePath);
                    return items;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load {EntityName}, returning empty list", EntityName);
        }

        return [];
    }

    /// <summary>
    /// Saves all entities to disk.
    /// </summary>
    /// <param name="items">The items to save.</param>
    public void Save(IEnumerable<T> items)
    {
        try
        {
            var filePath = FilePath;
            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var itemList = items.ToList();
            var json = JsonSerializer.Serialize(itemList, WriteOptions);
            File.WriteAllText(filePath, json);

            Log.Debug("Saved {Count} {EntityName} to {FilePath}", itemList.Count, EntityName, filePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save {EntityName}", EntityName);
        }
    }
}
