using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScalextricRace.Models;

namespace ScalextricRace.ViewModels;

/// <summary>
/// View model for a race entry (car/driver pairing in a controller slot).
/// ARC Pro supports 6 controllers with fixed colors.
/// </summary>
public partial class RaceEntryViewModel : ObservableObject
{
    /// <summary>
    /// Controller colors for slots 1-6 (Red, Green, Blue, Yellow, Orange, White).
    /// </summary>
    private static readonly string[] SlotColors =
    [
        "#E53935", // Red
        "#43A047", // Green
        "#1E88E5", // Blue
        "#FDD835", // Yellow
        "#FB8C00", // Orange
        "#FAFAFA"  // White
    ];

    /// <summary>
    /// Controller color names for display.
    /// </summary>
    private static readonly string[] SlotColorNames =
    [
        "Red",
        "Green",
        "Blue",
        "Yellow",
        "Orange",
        "White"
    ];

    /// <summary>
    /// The slot number (1-6) for this entry.
    /// </summary>
    public int SlotNumber { get; }

    /// <summary>
    /// Display label for the slot (e.g., "Slot 1 - Red").
    /// </summary>
    public string SlotLabel => $"Slot {SlotNumber} - {ColorName}";

    /// <summary>
    /// The color code for this slot's controller.
    /// </summary>
    public string SlotColor => SlotColors[SlotNumber - 1];

    /// <summary>
    /// The color name for this slot's controller.
    /// </summary>
    public string ColorName => SlotColorNames[SlotNumber - 1];

    /// <summary>
    /// Available cars to select from.
    /// </summary>
    public ObservableCollection<CarViewModel> AvailableCars { get; }

    /// <summary>
    /// Available drivers to select from.
    /// </summary>
    public ObservableCollection<DriverViewModel> AvailableDrivers { get; }

    /// <summary>
    /// Whether this slot is enabled/participating in the race.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfigured))]
    private bool _isEnabled;

    /// <summary>
    /// The selected car for this entry.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCar))]
    [NotifyPropertyChangedFor(nameof(IsConfigured))]
    [NotifyPropertyChangedFor(nameof(CarDisplayName))]
    [NotifyPropertyChangedFor(nameof(CarImagePath))]
    [NotifyPropertyChangedFor(nameof(CarPowerDisplay))]
    [NotifyPropertyChangedFor(nameof(MaxThrottle))]
    private CarViewModel? _selectedCar;

    /// <summary>
    /// The selected driver for this entry.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDriver))]
    [NotifyPropertyChangedFor(nameof(IsConfigured))]
    [NotifyPropertyChangedFor(nameof(DriverDisplayName))]
    [NotifyPropertyChangedFor(nameof(DriverImagePath))]
    [NotifyPropertyChangedFor(nameof(DriverPowerDisplay))]
    [NotifyPropertyChangedFor(nameof(MaxThrottle))]
    private DriverViewModel? _selectedDriver;

    /// <summary>
    /// Whether the car selection popup is open.
    /// </summary>
    [ObservableProperty]
    private bool _isCarPopupOpen;

    /// <summary>
    /// Whether the driver selection popup is open.
    /// </summary>
    [ObservableProperty]
    private bool _isDriverPopupOpen;

    /// <summary>
    /// The race's default power level (0-63), used when no car/driver is configured.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaxThrottle))]
    private int _raceDefaultPower = 40;

    /// <summary>
    /// Current lap number (starts at 0).
    /// </summary>
    [ObservableProperty]
    private int _currentLap;

    /// <summary>
    /// Last lap time in seconds (null if no laps completed).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastLapTimeDisplay))]
    private double? _lastLapTime;

    /// <summary>
    /// Best lap time in seconds (null if no laps completed).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BestLapTimeDisplay))]
    private double? _bestLapTime;

    /// <summary>
    /// Current lane (1 or 2, or 0 if not yet crossed).
    /// </summary>
    [ObservableProperty]
    private int _currentLane;

    /// <summary>
    /// Gets whether a car is selected.
    /// </summary>
    public bool HasCar => SelectedCar != null;

    /// <summary>
    /// Gets whether a driver is selected.
    /// </summary>
    public bool HasDriver => SelectedDriver != null;

    /// <summary>
    /// Gets the display name for the selected car.
    /// </summary>
    public string CarDisplayName => SelectedCar?.Name ?? "Select Car";

    /// <summary>
    /// Gets the image path for the selected car.
    /// </summary>
    public string? CarImagePath => SelectedCar?.ImagePath;

    /// <summary>
    /// Gets the power display for the selected car.
    /// </summary>
    public string CarPowerDisplay => SelectedCar != null ? $"Power: {SelectedCar.DefaultPower}" : "";

    /// <summary>
    /// Gets the display name for the selected driver.
    /// </summary>
    public string DriverDisplayName => SelectedDriver?.Name ?? "Select Driver";

    /// <summary>
    /// Gets the image path for the selected driver.
    /// </summary>
    public string? DriverImagePath => SelectedDriver?.ImagePath;

    /// <summary>
    /// Gets the power display for the selected driver.
    /// </summary>
    public string DriverPowerDisplay => SelectedDriver != null ? SelectedDriver.PowerPercentageDisplay : "";

    /// <summary>
    /// Gets the computed max throttle based on car power and driver percentage.
    /// Falls back to race default power if no car is configured.
    /// </summary>
    public int MaxThrottle
    {
        get
        {
            if (SelectedCar == null) return RaceDefaultPower;
            var carPower = SelectedCar.DefaultPower;
            var driverPercentage = SelectedDriver?.PowerPercentageSliderValue ?? 100;
            return (int)Math.Round(carPower * driverPercentage / 100.0);
        }
    }

    /// <summary>
    /// Gets whether this entry is fully configured (enabled with car and driver).
    /// </summary>
    public bool IsConfigured => IsEnabled && HasCar && HasDriver;

    /// <summary>
    /// Formatted last lap time for display.
    /// </summary>
    public string LastLapTimeDisplay => LastLapTime.HasValue
        ? $"{LastLapTime.Value:F2}s"
        : "--";

    /// <summary>
    /// Formatted best lap time for display.
    /// </summary>
    public string BestLapTimeDisplay => BestLapTime.HasValue
        ? $"{BestLapTime.Value:F2}s"
        : "--";

    /// <summary>
    /// Creates a new race entry view model.
    /// </summary>
    /// <param name="slotNumber">The slot number (1-6).</param>
    /// <param name="availableCars">Collection of available cars.</param>
    /// <param name="availableDrivers">Collection of available drivers.</param>
    public RaceEntryViewModel(int slotNumber, ObservableCollection<CarViewModel> availableCars, ObservableCollection<DriverViewModel> availableDrivers)
    {
        SlotNumber = slotNumber;
        AvailableCars = availableCars;
        AvailableDrivers = availableDrivers;
    }

    /// <summary>
    /// Gets the underlying model for this entry.
    /// </summary>
    public RaceEntry GetModel() => new()
    {
        SlotNumber = SlotNumber,
        CarId = SelectedCar?.Id,
        DriverId = SelectedDriver?.Id
    };

    /// <summary>
    /// Resets this entry to default state (disabled, no selections).
    /// </summary>
    public void Reset()
    {
        IsEnabled = false;
        SelectedCar = null;
        SelectedDriver = null;
        IsCarPopupOpen = false;
        IsDriverPopupOpen = false;
    }

    /// <summary>
    /// Opens the car selection popup.
    /// </summary>
    [RelayCommand]
    private void OpenCarPopup()
    {
        IsCarPopupOpen = true;
        IsDriverPopupOpen = false;
    }

    /// <summary>
    /// Opens the driver selection popup.
    /// </summary>
    [RelayCommand]
    private void OpenDriverPopup()
    {
        IsDriverPopupOpen = true;
        IsCarPopupOpen = false;
    }

    /// <summary>
    /// Selects a car and closes the popup.
    /// </summary>
    [RelayCommand]
    private void SelectCar(CarViewModel? car)
    {
        SelectedCar = car;
        IsCarPopupOpen = false;
    }

    /// <summary>
    /// Selects a driver and closes the popup.
    /// </summary>
    [RelayCommand]
    private void SelectDriver(DriverViewModel? driver)
    {
        SelectedDriver = driver;
        IsDriverPopupOpen = false;
    }

    // Close popups when selection changes (for ListBox binding approach)
    partial void OnSelectedCarChanged(CarViewModel? value)
    {
        IsCarPopupOpen = false;
    }

    partial void OnSelectedDriverChanged(DriverViewModel? value)
    {
        IsDriverPopupOpen = false;
    }
}
