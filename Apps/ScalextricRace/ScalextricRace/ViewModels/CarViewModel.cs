using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScalextricRace.Models;

namespace ScalextricRace.ViewModels;

/// <summary>
/// View model for car editing and display.
/// Wraps a Car model and provides observable properties for data binding.
/// </summary>
public partial class CarViewModel : ObservableObject
{
    private readonly Car _car;

    /// <summary>
    /// Callback invoked when deletion is requested for this car.
    /// </summary>
    public Action<CarViewModel>? OnDeleteRequested { get; set; }

    /// <summary>
    /// Callback invoked when any car property changes (for auto-save).
    /// </summary>
    public Action<CarViewModel>? OnChanged { get; set; }

    /// <summary>
    /// Callback invoked when tuning is requested for this car.
    /// </summary>
    public Action<CarViewModel>? OnTuneRequested { get; set; }

    /// <summary>
    /// Callback invoked when image selection is requested for this car.
    /// </summary>
    public Action<CarViewModel>? OnImageChangeRequested { get; set; }

    /// <summary>
    /// Gets whether this is the default car (cannot be deleted).
    /// </summary>
    public bool IsDefault { get; }

    /// <summary>
    /// Gets whether this car can be deleted (not the default car).
    /// </summary>
    public bool CanDelete => !IsDefault;

    /// <summary>
    /// Gets the unique identifier for the car.
    /// </summary>
    public Guid Id => _car.Id;

    /// <summary>
    /// Display name for the car.
    /// </summary>
    [ObservableProperty]
    private string _name;

    /// <summary>
    /// Optional path to car image for UI display.
    /// Use ImagePathToBitmapConverter in XAML to convert to Bitmap.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private string? _imagePath;

    /// <summary>
    /// Gets whether this car has an image set.
    /// </summary>
    public bool HasImage => !string.IsNullOrEmpty(ImagePath);

    /// <summary>
    /// Default power level for normal driving (0-63).
    /// </summary>
    [ObservableProperty]
    private int _defaultPower;

    /// <summary>
    /// Maximum ghost mode power without crashing (0-63).
    /// </summary>
    [ObservableProperty]
    private int _ghostMaxPower;

    /// <summary>
    /// Minimum power to keep the car moving (0-63).
    /// </summary>
    [ObservableProperty]
    private int _minPower;

    /// <summary>
    /// Creates a new CarViewModel wrapping the specified car.
    /// </summary>
    /// <param name="car">The car model to wrap.</param>
    /// <param name="isDefault">Whether this is the default car (non-deletable).</param>
    public CarViewModel(Car car, bool isDefault = false)
    {
        _car = car;
        IsDefault = isDefault;

        // Initialize from model
        _name = car.Name;
        _imagePath = car.ImagePath;
        _defaultPower = car.DefaultPower;
        _ghostMaxPower = car.GhostMaxPower;
        _minPower = car.MinPower;
    }

    /// <summary>
    /// Gets the underlying Car model.
    /// </summary>
    public Car GetModel() => _car;

    // Sync changes back to model and notify via callback
    partial void OnNameChanged(string value)
    {
        _car.Name = value;
        OnChanged?.Invoke(this);
    }

    partial void OnImagePathChanged(string? value)
    {
        _car.ImagePath = value;
        OnChanged?.Invoke(this);
    }

    partial void OnDefaultPowerChanged(int value)
    {
        _car.DefaultPower = Math.Clamp(value, 0, 63);
        OnChanged?.Invoke(this);
    }

    partial void OnGhostMaxPowerChanged(int value)
    {
        _car.GhostMaxPower = Math.Clamp(value, 0, 63);
        OnChanged?.Invoke(this);
    }

    partial void OnMinPowerChanged(int value)
    {
        _car.MinPower = Math.Clamp(value, 0, 63);
        OnChanged?.Invoke(this);
    }

    /// <summary>
    /// Requests deletion of this car.
    /// </summary>
    [RelayCommand]
    private void RequestDelete()
    {
        if (!IsDefault)
        {
            OnDeleteRequested?.Invoke(this);
        }
    }

    /// <summary>
    /// Requests tuning for this car.
    /// </summary>
    [RelayCommand]
    private void RequestTune()
    {
        OnTuneRequested?.Invoke(this);
    }

    /// <summary>
    /// Requests image change for this car.
    /// </summary>
    [RelayCommand]
    private void RequestImageChange()
    {
        OnImageChangeRequested?.Invoke(this);
    }
}
