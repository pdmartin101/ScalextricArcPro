using Avalonia.Media.Imaging;
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
    private readonly SkillLevelConfig _skillLevels;

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
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    [NotifyPropertyChangedFor(nameof(ImageBitmap))]
    private string? _imagePath;

    /// <summary>
    /// Gets whether this driver has an image set.
    /// </summary>
    public bool HasImage => !string.IsNullOrEmpty(ImagePath);

    /// <summary>
    /// Gets the driver image as a bitmap for display.
    /// </summary>
    public Bitmap? ImageBitmap
    {
        get
        {
            if (string.IsNullOrEmpty(ImagePath) || !System.IO.File.Exists(ImagePath))
                return null;

            try
            {
                return new Bitmap(ImagePath);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Maximum power level this driver can use (0-63).
    /// Null means no limit - driver can use full car power.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPowerLimit))]
    [NotifyPropertyChangedFor(nameof(PowerLimitDisplay))]
    [NotifyPropertyChangedFor(nameof(SelectedSkillLevel))]
    private int? _powerLimit;

    /// <summary>
    /// Gets whether this driver has a power limit set (less than 63).
    /// </summary>
    public bool HasPowerLimit => PowerLimit.HasValue && PowerLimit.Value < 63;

    /// <summary>
    /// Gets the power limit display string using skill level names.
    /// </summary>
    public string PowerLimitDisplay => _skillLevels.GetLevelName(PowerLimit);

    /// <summary>
    /// Gets the available skill levels for the UI.
    /// </summary>
    public List<SkillLevel> AvailableSkillLevels => _skillLevels.Levels;

    /// <summary>
    /// Gets or sets the selected skill level for the dropdown.
    /// </summary>
    public SkillLevel? SelectedSkillLevel
    {
        get => GetCurrentSkillLevel();
        set
        {
            if (value != null)
            {
                SetSkillLevel(value);
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the current skill level based on power limit.
    /// </summary>
    private SkillLevel? GetCurrentSkillLevel()
    {
        // Find matching skill level by power limit
        if (!PowerLimit.HasValue || PowerLimit.Value >= 63)
        {
            return _skillLevels.Levels.FirstOrDefault(l => l.IsNoLimit);
        }

        // Try exact match first
        var exactMatch = _skillLevels.Levels.FirstOrDefault(l => l.PowerLimit == PowerLimit.Value);
        if (exactMatch != null)
        {
            return exactMatch;
        }

        // No exact match - return closest lower level
        return _skillLevels.Levels
            .Where(l => l.PowerLimit <= PowerLimit.Value)
            .OrderByDescending(l => l.PowerLimit)
            .FirstOrDefault();
    }

    /// <summary>
    /// Creates a new DriverViewModel wrapping the specified driver.
    /// </summary>
    /// <param name="driver">The driver model to wrap.</param>
    /// <param name="isDefault">Whether this is the default driver (non-deletable).</param>
    /// <param name="skillLevels">The skill level configuration (optional, loads default if null).</param>
    public DriverViewModel(Driver driver, bool isDefault = false, SkillLevelConfig? skillLevels = null)
    {
        _driver = driver;
        _skillLevels = skillLevels ?? SkillLevelConfig.Load();
        IsDefault = isDefault;

        // Initialize from model
        _name = driver.Name;
        _imagePath = driver.ImagePath;
        _powerLimit = driver.PowerLimit;
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

    partial void OnPowerLimitChanged(int? value)
    {
        _driver.PowerLimit = value.HasValue ? Math.Clamp(value.Value, 0, 63) : null;
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
    /// Sets the power limit to the specified skill level.
    /// </summary>
    /// <param name="skillLevel">The skill level to apply.</param>
    private void SetSkillLevel(SkillLevel skillLevel)
    {
        if (skillLevel.IsNoLimit)
        {
            PowerLimit = null;
        }
        else
        {
            PowerLimit = skillLevel.PowerLimit;
        }
    }
}
