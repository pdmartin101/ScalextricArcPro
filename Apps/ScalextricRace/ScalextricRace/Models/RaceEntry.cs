namespace ScalextricRace.Models;

/// <summary>
/// Represents a car/driver pairing for a race entry.
/// Each race can have up to 6 entries (one per slot).
/// </summary>
public class RaceEntry
{
    /// <summary>
    /// The slot number (1-6) for this entry.
    /// </summary>
    public int SlotNumber { get; set; }

    /// <summary>
    /// Whether this slot is enabled/participating in the race.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// The ID of the selected car, or null if none selected.
    /// </summary>
    public Guid? CarId { get; set; }

    /// <summary>
    /// The ID of the selected driver, or null if none selected.
    /// </summary>
    public Guid? DriverId { get; set; }
}
