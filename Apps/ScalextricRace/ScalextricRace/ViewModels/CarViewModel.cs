using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScalextricRace.Models;
using Serilog;

namespace ScalextricRace.ViewModels;

/// <summary>
/// View model for car editing and display.
/// Wraps a Car model and provides observable properties for data binding.
/// </summary>
public partial class CarViewModel : ObservableObject
{
    private readonly Car _car;
    private Bitmap? _cachedBitmap;
    private string? _cachedImagePath;

    /// <summary>
    /// Event raised when deletion is requested for this car.
    /// </summary>
    public event EventHandler? DeleteRequested;

    /// <summary>
    /// Event raised when any car property changes (for auto-save).
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Event raised when tuning is requested for this car.
    /// </summary>
    public event EventHandler? TuneRequested;

    /// <summary>
    /// Event raised when image selection is requested for this car.
    /// </summary>
    public event EventHandler? ImageChangeRequested;

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
    [NotifyPropertyChangedFor(nameof(HasImage))]
    [NotifyPropertyChangedFor(nameof(ImageBitmap))]
    private string? _imagePath;

    /// <summary>
    /// Gets whether this car has an image set.
    /// </summary>
    public bool HasImage => !string.IsNullOrEmpty(ImagePath);

    /// <summary>
    /// Gets the car image as a bitmap for display.
    /// Cached for performance - invalidated when ImagePath changes.
    /// </summary>
    public Bitmap? ImageBitmap
    {
        get
        {
            if (string.IsNullOrEmpty(ImagePath) || !System.IO.File.Exists(ImagePath))
            {
                _cachedBitmap = null;
                _cachedImagePath = null;
                return null;
            }

            // Return cached bitmap if path hasn't changed
            if (_cachedBitmap != null && _cachedImagePath == ImagePath)
                return _cachedBitmap;

            try
            {
                _cachedBitmap = new Bitmap(ImagePath);
                _cachedImagePath = ImagePath;
                return _cachedBitmap;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load image for car {CarId} from {ImagePath}", Id, ImagePath);
                _cachedBitmap = null;
                _cachedImagePath = null;
                return null;
            }
        }
    }

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

    // Sync changes back to model and raise Changed event
    partial void OnNameChanged(string value)
    {
        _car.Name = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnImagePathChanged(string? value)
    {
        _car.ImagePath = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnDefaultPowerChanged(int value)
    {
        _car.DefaultPower = Math.Clamp(value, 0, 63);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnGhostMaxPowerChanged(int value)
    {
        _car.GhostMaxPower = Math.Clamp(value, 0, 63);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnMinPowerChanged(int value)
    {
        _car.MinPower = Math.Clamp(value, 0, 63);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests deletion of this car.
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
    /// Requests tuning for this car.
    /// </summary>
    [RelayCommand]
    private void RequestTune()
    {
        TuneRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests image change for this car.
    /// </summary>
    [RelayCommand]
    private void RequestImageChange()
    {
        ImageChangeRequested?.Invoke(this, EventArgs.Empty);
    }
}
