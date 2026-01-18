using CommunityToolkit.Mvvm.ComponentModel;
using Scalextric;

namespace ScalextricRace.ViewModels;

/// <summary>
/// View model for individual controller/slot power settings.
/// Used in per-slot power mode for individual car control.
/// </summary>
public partial class ControllerViewModel : ObservableObject
{
    /// <summary>
    /// Gets the slot number (1-6).
    /// </summary>
    [ObservableProperty]
    private int _slotNumber;

    /// <summary>
    /// Power level for this slot (0-63).
    /// </summary>
    [ObservableProperty]
    private int _powerLevel = ScalextricProtocol.MaxPowerLevel;

    /// <summary>
    /// Throttle profile type for this slot.
    /// </summary>
    [ObservableProperty]
    private ThrottleProfileType _throttleProfile = ThrottleProfileType.Linear;

    /// <summary>
    /// Available throttle profile types for selection.
    /// </summary>
    public static ThrottleProfileType[] AvailableProfiles { get; } =
        Enum.GetValues<ThrottleProfileType>();

    /// <summary>
    /// Display label for this slot.
    /// </summary>
    public string SlotLabel => $"Slot {SlotNumber}";

    /// <summary>
    /// Creates a controller view model for the specified slot.
    /// </summary>
    /// <param name="slotNumber">The slot number (1-6).</param>
    public ControllerViewModel(int slotNumber)
    {
        SlotNumber = slotNumber;
    }
}
