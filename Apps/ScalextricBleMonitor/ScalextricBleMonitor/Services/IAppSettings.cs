namespace ScalextricBleMonitor.Services;

/// <summary>
/// Interface for application settings, enabling unit test mocking.
/// </summary>
public interface IAppSettings
{
    /// <summary>
    /// Global power level for track power (0-63).
    /// </summary>
    int PowerLevel { get; set; }

    /// <summary>
    /// Per-slot power levels (0-63). Array index 0 = slot 1, etc.
    /// </summary>
    int[] SlotPowerLevels { get; set; }

    /// <summary>
    /// When true, use individual per-slot power levels. When false, use global PowerLevel for all slots.
    /// </summary>
    bool UsePerSlotPower { get; set; }

    /// <summary>
    /// Per-slot ghost mode settings. Array index 0 = slot 1, etc.
    /// </summary>
    bool[] SlotGhostModes { get; set; }

    /// <summary>
    /// Per-slot ghost throttle levels (0-63). Array index 0 = slot 1, etc.
    /// </summary>
    int[] SlotGhostThrottleLevels { get; set; }

    /// <summary>
    /// Per-slot throttle profile type names. Array index 0 = slot 1, etc.
    /// </summary>
    string[] SlotThrottleProfiles { get; set; }

    /// <summary>
    /// Per-slot ghost source type names. Array index 0 = slot 1, etc.
    /// </summary>
    string[] SlotGhostSources { get; set; }

    /// <summary>
    /// Saves settings to disk.
    /// </summary>
    void Save();
}
