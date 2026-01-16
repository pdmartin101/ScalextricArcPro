using CommunityToolkit.Mvvm.ComponentModel;
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
    /// </summary>
    [ObservableProperty]
    private string? _imagePath;

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

    // Sync changes back to model
    partial void OnNameChanged(string value) => _car.Name = value;
    partial void OnImagePathChanged(string? value) => _car.ImagePath = value;
    partial void OnDefaultPowerChanged(int value) => _car.DefaultPower = Math.Clamp(value, 0, 63);
    partial void OnGhostMaxPowerChanged(int value) => _car.GhostMaxPower = Math.Clamp(value, 0, 63);
    partial void OnMinPowerChanged(int value) => _car.MinPower = Math.Clamp(value, 0, 63);
}
