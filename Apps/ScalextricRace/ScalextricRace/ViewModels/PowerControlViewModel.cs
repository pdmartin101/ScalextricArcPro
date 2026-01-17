using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Scalextric;
using ScalextricRace.Services;
using Serilog;

namespace ScalextricRace.ViewModels;

/// <summary>
/// Manages power control settings for the track.
/// Handles global and per-slot power levels, throttle profiles, and controller state.
/// </summary>
public partial class PowerControlViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private bool _isInitializing = true;

    /// <summary>
    /// The global power level (0-63) applied to all slots.
    /// </summary>
    [ObservableProperty]
    private int _powerLevel = 63;

    /// <summary>
    /// The selected throttle profile type for all cars.
    /// </summary>
    [ObservableProperty]
    private ThrottleProfileType _selectedThrottleProfile = ThrottleProfileType.Linear;

    /// <summary>
    /// Available throttle profile types for selection.
    /// </summary>
    public static ThrottleProfileType[] AvailableThrottleProfiles { get; } =
        Enum.GetValues<ThrottleProfileType>();

    /// <summary>
    /// Whether per-slot power mode is enabled.
    /// When true, each controller has individual power settings.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PerSlotToggleText))]
    private bool _isPerSlotPowerMode;

    /// <summary>
    /// Gets the text for the per-slot power mode toggle button.
    /// </summary>
    public string PerSlotToggleText => IsPerSlotPowerMode ? "Global Mode" : "Per-Slot Mode";

    /// <summary>
    /// Collection of controller view models for per-slot power settings.
    /// </summary>
    public ObservableCollection<ControllerViewModel> Controllers { get; } = [];

    /// <summary>
    /// Initializes a new instance of the PowerControlViewModel.
    /// </summary>
    /// <param name="settings">The application settings.</param>
    public PowerControlViewModel(AppSettings settings)
    {
        _settings = settings;

        // Load startup global settings (ultra-safe values)
        PowerLevel = _settings.Startup.PowerLevel;
        SelectedThrottleProfile = Enum.TryParse<ThrottleProfileType>(_settings.Startup.ThrottleProfile, out var profile)
            ? profile
            : ThrottleProfileType.Linear;
        IsPerSlotPowerMode = _settings.Startup.IsPerSlotPowerMode;

        // Create 6 controller ViewModels (one per slot)
        for (int i = 0; i < 6; i++)
        {
            var slotStartup = _settings.Startup.SlotSettings[i];

            var controller = new ControllerViewModel(i + 1);
            controller.PowerLevel = slotStartup.PowerLevel;
            controller.ThrottleProfile = Enum.TryParse<ThrottleProfileType>(slotStartup.ThrottleProfile, out var slotProfile)
                ? slotProfile
                : ThrottleProfileType.Linear;

            // Subscribe to changes for auto-save
            controller.PowerLevelChanged += OnControllerPowerLevelChanged;
            controller.ThrottleProfileChanged += OnControllerThrottleProfileChanged;

            Controllers.Add(controller);
        }

        _isInitializing = false;
        Log.Information("PowerControlViewModel initialized. PowerLevel={PowerLevel}, ThrottleProfile={ThrottleProfile}, PerSlotMode={PerSlotMode}",
            PowerLevel, SelectedThrottleProfile, IsPerSlotPowerMode);
    }

    /// <summary>
    /// Builds a power command based on current settings and race entry configuration.
    /// </summary>
    /// <param name="commandType">The type of power command to build.</param>
    /// <param name="raceEntries">Optional race entries for applying car/driver power settings.</param>
    /// <returns>A byte array containing the power command.</returns>
    public byte[] BuildPowerCommand(ScalextricProtocol.CommandType commandType, IEnumerable<RaceEntryViewModel>? raceEntries = null)
    {
        var builder = new ScalextricProtocol.CommandBuilder { Type = commandType };

        if (IsPerSlotPowerMode && raceEntries != null)
        {
            // Apply race entry configuration (car + driver power settings)
            for (int i = 0; i < Controllers.Count; i++)
            {
                var entry = raceEntries.ElementAtOrDefault(i);
                byte power = entry != null ? (byte)entry.MaxThrottle : (byte)Controllers[i].PowerLevel;
                builder.SetSlotPower(i + 1, power);
            }
        }
        else if (IsPerSlotPowerMode)
        {
            // Use controller power levels directly
            for (int i = 0; i < Controllers.Count; i++)
            {
                builder.SetSlotPower(i + 1, (byte)Controllers[i].PowerLevel);
            }
        }
        else
        {
            // Global power mode
            builder.SetAllPower((byte)PowerLevel);
        }

        return builder.Build();
    }

    /// <summary>
    /// Saves current power control settings to persistent storage.
    /// </summary>
    public void SaveSettings()
    {
        if (_isInitializing) return;

        _settings.Startup.PowerLevel = PowerLevel;
        _settings.Startup.ThrottleProfile = SelectedThrottleProfile.ToString();
        _settings.Startup.IsPerSlotPowerMode = IsPerSlotPowerMode;

        // Save per-slot settings
        for (int i = 0; i < Controllers.Count && i < _settings.Startup.SlotSettings.Length; i++)
        {
            var controller = Controllers[i];
            _settings.Startup.SlotSettings[i].PowerLevel = controller.PowerLevel;
            _settings.Startup.SlotSettings[i].ThrottleProfile = controller.ThrottleProfile.ToString();
        }

        _settings.Save();
        Log.Debug("Saved power control settings");
    }

    // Partial method implementations for property changes
    partial void OnPowerLevelChanged(int value)
    {
        if (_isInitializing) return;

        // Sync all controllers to global value if not in per-slot mode
        if (!IsPerSlotPowerMode)
        {
            foreach (var controller in Controllers)
            {
                controller.PowerLevel = value;
            }
        }

        SaveSettings();
    }

    partial void OnSelectedThrottleProfileChanged(ThrottleProfileType value)
    {
        if (_isInitializing) return;

        // Sync all controllers to global value if not in per-slot mode
        if (!IsPerSlotPowerMode)
        {
            foreach (var controller in Controllers)
            {
                controller.ThrottleProfile = value;
            }
        }

        SaveSettings();
    }

    partial void OnIsPerSlotPowerModeChanged(bool value)
    {
        if (_isInitializing) return;

        if (!value)
        {
            // Switching to global mode - sync all controllers to global settings
            foreach (var controller in Controllers)
            {
                controller.PowerLevel = PowerLevel;
                controller.ThrottleProfile = SelectedThrottleProfile;
            }
        }

        SaveSettings();
    }

    /// <summary>
    /// Handles power level changes from individual controller ViewModels.
    /// </summary>
    private void OnControllerPowerLevelChanged(object? sender, int value)
    {
        if (_isInitializing) return;

        // If in global mode, update global power level
        if (!IsPerSlotPowerMode && sender is ControllerViewModel)
        {
            PowerLevel = value;
        }

        SaveSettings();
    }

    /// <summary>
    /// Handles throttle profile changes from individual controller ViewModels.
    /// </summary>
    private void OnControllerThrottleProfileChanged(object? sender, ThrottleProfileType value)
    {
        if (_isInitializing) return;

        // If in global mode, update global throttle profile
        if (!IsPerSlotPowerMode && sender is ControllerViewModel)
        {
            SelectedThrottleProfile = value;
        }

        SaveSettings();
    }
}
