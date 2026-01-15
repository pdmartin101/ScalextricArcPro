namespace ScalextricBleMonitor.Models;

/// <summary>
/// Represents the state of a single slot/controller on the Scalextric track.
/// This is a pure domain model with no UI or framework dependencies.
/// </summary>
public class Controller
{
    /// <summary>
    /// The slot number (1-6).
    /// </summary>
    public int SlotNumber { get; set; }

    /// <summary>
    /// Current throttle position (0-63).
    /// </summary>
    public int Throttle { get; set; }

    /// <summary>
    /// Whether the brake button is currently pressed.
    /// </summary>
    public bool IsBrakePressed { get; set; }

    /// <summary>
    /// Whether the lane change button is currently pressed.
    /// </summary>
    public bool IsLaneChangePressed { get; set; }

    /// <summary>
    /// Count of brake button presses (rising edges detected).
    /// </summary>
    public int BrakeCount { get; set; }

    /// <summary>
    /// Count of lane change button presses (rising edges detected).
    /// </summary>
    public int LaneChangeCount { get; set; }

    /// <summary>
    /// Power level for this controller (0-63). Used as a multiplier for track power.
    /// In ghost mode, this becomes the direct throttle index (0-63).
    /// </summary>
    public int PowerLevel { get; set; } = 63;

    /// <summary>
    /// When true, this slot operates in ghost mode - PowerLevel becomes a direct throttle
    /// index rather than a multiplier, allowing autonomous car control without a physical controller.
    /// </summary>
    public bool IsGhostMode { get; set; }

    /// <summary>
    /// Resets controller input state (throttle, buttons, counts).
    /// Does not reset power settings.
    /// </summary>
    public void ResetInputState()
    {
        Throttle = 0;
        IsBrakePressed = false;
        IsLaneChangePressed = false;
        BrakeCount = 0;
        LaneChangeCount = 0;
    }
}
