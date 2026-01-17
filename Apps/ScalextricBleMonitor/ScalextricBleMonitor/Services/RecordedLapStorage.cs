using System.Collections.Generic;
using ScalextricBleMonitor.Models;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Handles persistence of recorded laps to JSON files.
/// Stores all recorded laps in a single file in the user's local app data folder.
/// Uses internal instance for base class functionality while maintaining static API.
/// </summary>
public class RecordedLapStorage : JsonStorageBase<RecordedLap>
{
    /// <summary>
    /// Singleton instance for static access pattern.
    /// </summary>
    private static readonly RecordedLapStorage Instance = new();

    /// <inheritdoc />
    protected override string FileName => "recorded_laps.json";

    /// <inheritdoc />
    protected override string EntityName => "recorded laps";

    /// <summary>
    /// Loads all recorded laps from disk.
    /// </summary>
    /// <returns>List of recorded laps, or empty list if file doesn't exist or is invalid.</returns>
    public new static List<RecordedLap> Load() => Instance.LoadInternal();

    /// <summary>
    /// Saves all recorded laps to disk.
    /// </summary>
    /// <param name="laps">The list of recorded laps to save.</param>
    public new static void Save(IEnumerable<RecordedLap> laps) => Instance.SaveInternal(laps);

    /// <summary>
    /// Internal load implementation using base class.
    /// </summary>
    private List<RecordedLap> LoadInternal() => base.Load();

    /// <summary>
    /// Internal save implementation using base class.
    /// </summary>
    private void SaveInternal(IEnumerable<RecordedLap> laps) => base.Save(laps);
}
