using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScalextricRace.Models;

namespace ScalextricRace.ViewModels;

/// <summary>
/// View model for driver editing and display.
/// Wraps a Driver model and provides observable properties for data binding.
/// </summary>
public partial class DriverViewModel : ObservableObject
{
    private readonly Driver _driver;

    /// <summary>
    /// Event raised when deletion is requested for this driver.
    /// </summary>
    public event EventHandler? DeleteRequested;

    /// <summary>
    /// Event raised when any driver property changes (for auto-save).
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Event raised when image selection is requested for this driver.
    /// </summary>
    public event EventHandler? ImageChangeRequested;

    /// <summary>
    /// Gets whether this is the default driver (cannot be deleted).
    /// </summary>
    public bool IsDefault { get; }

    /// <summary>
    /// Gets whether this driver can be deleted (not the default driver).
    /// </summary>
    public bool CanDelete => !IsDefault;

    /// <summary>
    /// Gets the unique identifier for the driver.
    /// </summary>
    public Guid Id => _driver.Id;

    /// <summary>
    /// Display name for the driver.
    /// </summary>
    [ObservableProperty]
    private string _name;

    /// <summary>
    /// Optional path to driver image/avatar for UI display.
    /// Use ImagePathToBitmapConverter in XAML to convert to Bitmap.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private string? _imagePath;

    /// <summary>
    /// Gets whether this driver has an image set.
    /// </summary>
    public bool HasImage => !string.IsNullOrEmpty(ImagePath);

    /// <summary>
    /// Power percentage for this driver (50-100).
    /// Null means 100% - driver can use full car power.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPowerRestriction))]
    [NotifyPropertyChangedFor(nameof(PowerPercentageDisplay))]
    [NotifyPropertyChangedFor(nameof(PowerPercentageSliderValue))]
    private int? _powerPercentage;

    /// <summary>
    /// Gets or sets the power percentage for slider binding (never null, treats null as 100).
    /// </summary>
    public int PowerPercentageSliderValue
    {
        get => PowerPercentage ?? 100;
        set => PowerPercentage = value >= 100 ? null : value;
    }

    /// <summary>
    /// Gets whether this driver has a power restriction (less than 100%).
    /// </summary>
    public bool HasPowerRestriction => PowerPercentage.HasValue && PowerPercentage.Value < 100;

    /// <summary>
    /// Gets the power percentage as a display string (e.g., "75%").
    /// </summary>
    public string PowerPercentageDisplay => $"{PowerPercentageSliderValue}%";

    /// <summary>
    /// Creates a new DriverViewModel wrapping the specified driver.
    /// </summary>
    /// <param name="driver">The driver model to wrap.</param>
    /// <param name="isDefault">Whether this is the default driver (non-deletable).</param>
    public DriverViewModel(Driver driver, bool isDefault = false)
    {
        _driver = driver;
        IsDefault = isDefault;

        // Initialize from model
        _name = driver.Name;
        _imagePath = driver.ImagePath;
        _powerPercentage = driver.PowerPercentage;
    }

    /// <summary>
    /// Gets the underlying Driver model.
    /// </summary>
    public Driver GetModel() => _driver;

    // Sync changes back to model and raise Changed event
    partial void OnNameChanged(string value)
    {
        _driver.Name = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnImagePathChanged(string? value)
    {
        _driver.ImagePath = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnPowerPercentageChanged(int? value)
    {
        _driver.PowerPercentage = value.HasValue ? Math.Clamp(value.Value, 50, 100) : null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests deletion of this driver.
    /// </summary>
    [RelayCommand]
    private void RequestDelete()
    {
        if (!IsDefault)
        {
            DeleteRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Requests image change for this driver.
    /// </summary>
    [RelayCommand]
    private void RequestImageChange()
    {
        ImageChangeRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Increments the power percentage by 1, up to maximum of 100.
    /// If currently null (100%), stays at 100.
    /// </summary>
    [RelayCommand]
    private void IncrementPowerPercentage()
    {
        var current = PowerPercentage ?? 100;
        if (current < 100)
        {
            PowerPercentage = current + 1;
        }
    }

    /// <summary>
    /// Decrements the power percentage by 1, down to minimum of 50.
    /// If currently null (100%), sets to 99.
    /// </summary>
    [RelayCommand]
    private void DecrementPowerPercentage()
    {
        var current = PowerPercentage ?? 100;
        if (current > 50)
        {
            PowerPercentage = current - 1;
        }
    }
}
