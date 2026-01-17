using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scalextric;
using ScalextricRace.Models;
using ScalextricRace.Services;
using Serilog;

namespace ScalextricRace.ViewModels;

/// <summary>
/// Main view model for the ScalextricRace application.
/// Manages connection state, race status, and car/slot information.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    #region Fields

    private readonly Services.IBleService? _bleService;
    private readonly IPowerHeartbeatService? _powerHeartbeatService;
    private readonly IWindowService _windowService;
    private readonly AppSettings _settings;
    private readonly ICarStorage _carStorage;
    private readonly IDriverStorage _driverStorage;
    private readonly IRaceStorage _raceStorage;
    private readonly SynchronizationContext? _syncContext;
    private bool _isInitializing = true;

    #endregion

    #region Connection State

    /// <summary>
    /// Indicates whether BLE scanning is active.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionStatusText))]
    [NotifyPropertyChangedFor(nameof(CurrentConnectionState))]
    private bool _isScanning;

    /// <summary>
    /// Indicates whether a Scalextric device has been detected via BLE advertisement.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionStatusText))]
    [NotifyPropertyChangedFor(nameof(CurrentConnectionState))]
    private bool _isDeviceDetected;

    /// <summary>
    /// Indicates whether an active GATT connection exists to the powerbase.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionStatusText))]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    [NotifyPropertyChangedFor(nameof(CurrentConnectionState))]
    private bool _isGattConnected;

    /// <summary>
    /// Status message to display to the user.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "Starting...";

    /// <summary>
    /// Gets whether the device is connected and ready for commands.
    /// </summary>
    public bool IsConnected => IsGattConnected;

    /// <summary>
    /// Gets the current connection status as a display string.
    /// </summary>
    public string ConnectionStatusText => (IsScanning, IsDeviceDetected, IsGattConnected) switch
    {
        (_, _, true) => "Connected",
        (_, true, false) => "Connecting...",
        (true, false, _) => "Scanning",
        _ => "Disconnected"
    };

    /// <summary>
    /// Gets the current connection state for UI display.
    /// Used with a converter to determine status indicator color.
    /// </summary>
    public ConnectionState CurrentConnectionState => (IsScanning, IsDeviceDetected, IsGattConnected) switch
    {
        (_, _, true) => ConnectionState.Connected,
        (_, true, false) => ConnectionState.Connecting,
        (true, false, _) => ConnectionState.Connecting,
        _ => ConnectionState.Disconnected
    };

    #endregion

    #region Race State

    /// <summary>
    /// Indicates whether track power is currently enabled.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PowerButtonText))]
    private bool _isPowerEnabled;

    /// <summary>
    /// Gets the text to display on the power toggle button.
    /// </summary>
    public string PowerButtonText => IsPowerEnabled ? "POWER OFF" : "POWER ON";

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

    #endregion

    #region Navigation

    /// <summary>
    /// The current top-level application mode (Setup, Configure, or Racing).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSetupMode))]
    [NotifyPropertyChangedFor(nameof(IsConfigureMode))]
    [NotifyPropertyChangedFor(nameof(IsRacingMode))]
    [NotifyPropertyChangedFor(nameof(IsRaceMode))]
    [NotifyPropertyChangedFor(nameof(IsRaceConfigMode))]
    [NotifyPropertyChangedFor(nameof(IsCarsMode))]
    [NotifyPropertyChangedFor(nameof(IsDriversMode))]
    [NotifyPropertyChangedFor(nameof(IsSettingsMode))]
    private AppMode _currentAppMode = AppMode.Setup;

    /// <summary>
    /// Gets whether the app is in Setup mode.
    /// </summary>
    public bool IsSetupMode => CurrentAppMode == AppMode.Setup;

    /// <summary>
    /// Gets whether the app is in Configure mode (setting up a race before starting).
    /// </summary>
    public bool IsConfigureMode => CurrentAppMode == AppMode.Configure;

    /// <summary>
    /// Gets whether the app is in Racing mode.
    /// </summary>
    public bool IsRacingMode => CurrentAppMode == AppMode.Racing;

    /// <summary>
    /// The race configuration currently being set up or run.
    /// </summary>
    [ObservableProperty]
    private RaceViewModel? _activeRace;

    /// <summary>
    /// Collection of race entries (car/driver pairings) for the current race.
    /// </summary>
    public ObservableCollection<RaceEntryViewModel> RaceEntries { get; } = [];

    /// <summary>
    /// Gets whether the race can be started (at least one configured entry).
    /// </summary>
    [ObservableProperty]
    private bool _canStartRace;

    // Runtime race settings (overrides from the race template)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigFreePracticeDisplay))]
    private bool _configFreePracticeEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigFreePracticeDisplay))]
    private RaceStageMode _configFreePracticeMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigFreePracticeDisplay))]
    private int _configFreePracticeLapCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigFreePracticeDisplay))]
    private int _configFreePracticeTimeMinutes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigQualifyingDisplay))]
    private bool _configQualifyingEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigQualifyingDisplay))]
    private RaceStageMode _configQualifyingMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigQualifyingDisplay))]
    private int _configQualifyingLapCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigQualifyingDisplay))]
    private int _configQualifyingTimeMinutes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigRaceDisplay))]
    private bool _configRaceEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigRaceDisplay))]
    private RaceStageMode _configRaceMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigRaceDisplay))]
    private int _configRaceLapCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigRaceDisplay))]
    private int _configRaceTimeMinutes;

    /// <summary>
    /// Default power level (0-63) for entries without a car/driver configured.
    /// </summary>
    [ObservableProperty]
    private int _configDefaultPower = 40;

    /// <summary>
    /// Whether test mode is active (power on for testing controllers).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TestButtonText))]
    private bool _isTestModeActive;

    /// <summary>
    /// Gets the text for the test mode toggle button.
    /// </summary>
    public string TestButtonText => IsTestModeActive ? "Stop Test" : "Test";

    /// <summary>
    /// Gets the display string for Free Practice configuration.
    /// </summary>
    public string ConfigFreePracticeDisplay => GetStageDisplay(ConfigFreePracticeMode, ConfigFreePracticeLapCount, ConfigFreePracticeTimeMinutes);

    /// <summary>
    /// Gets the display string for Qualifying configuration.
    /// </summary>
    public string ConfigQualifyingDisplay => GetStageDisplay(ConfigQualifyingMode, ConfigQualifyingLapCount, ConfigQualifyingTimeMinutes);

    /// <summary>
    /// Gets the display string for Race configuration.
    /// </summary>
    public string ConfigRaceDisplay => GetStageDisplay(ConfigRaceMode, ConfigRaceLapCount, ConfigRaceTimeMinutes);

    private static string GetStageDisplay(RaceStageMode mode, int laps, int minutes)
    {
        return mode == RaceStageMode.Laps
            ? $"{laps} lap{(laps == 1 ? "" : "s")}"
            : $"{minutes} min";
    }

    /// <summary>
    /// The current navigation mode/page (within Setup mode).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRaceMode))]
    [NotifyPropertyChangedFor(nameof(IsRaceConfigMode))]
    [NotifyPropertyChangedFor(nameof(IsCarsMode))]
    [NotifyPropertyChangedFor(nameof(IsDriversMode))]
    [NotifyPropertyChangedFor(nameof(IsSettingsMode))]
    private NavigationMode _currentMode = NavigationMode.Race;

    /// <summary>
    /// Whether the hamburger menu is currently open.
    /// </summary>
    [ObservableProperty]
    private bool _isMenuOpen;

    /// <summary>
    /// Gets whether Race mode is active (within Setup mode).
    /// </summary>
    public bool IsRaceMode => CurrentAppMode == AppMode.Setup && CurrentMode == NavigationMode.Race;

    /// <summary>
    /// Gets whether Race Config mode is active (within Setup mode).
    /// </summary>
    public bool IsRaceConfigMode => CurrentAppMode == AppMode.Setup && CurrentMode == NavigationMode.RaceConfig;

    /// <summary>
    /// Gets whether Cars mode is active (within Setup mode).
    /// </summary>
    public bool IsCarsMode => CurrentAppMode == AppMode.Setup && CurrentMode == NavigationMode.Cars;

    /// <summary>
    /// Gets whether Drivers mode is active (within Setup mode).
    /// </summary>
    public bool IsDriversMode => CurrentAppMode == AppMode.Setup && CurrentMode == NavigationMode.Drivers;

    /// <summary>
    /// Gets whether Settings mode is active (within Setup mode).
    /// </summary>
    public bool IsSettingsMode => CurrentAppMode == AppMode.Setup && CurrentMode == NavigationMode.Settings;

    #endregion

    #region Car Management

    /// <summary>
    /// Collection of all cars available for racing.
    /// </summary>
    public ObservableCollection<CarViewModel> Cars { get; } = [];

    /// <summary>
    /// The currently selected car for editing.
    /// </summary>
    [ObservableProperty]
    private CarViewModel? _selectedCar;

    #endregion

    #region Driver Management

    /// <summary>
    /// Collection of all drivers available for racing.
    /// </summary>
    public ObservableCollection<DriverViewModel> Drivers { get; } = [];

    /// <summary>
    /// The currently selected driver for editing.
    /// </summary>
    [ObservableProperty]
    private DriverViewModel? _selectedDriver;

    #endregion

    #region Race Management

    /// <summary>
    /// Collection of all race configurations.
    /// </summary>
    public ObservableCollection<RaceViewModel> Races { get; } = [];

    /// <summary>
    /// The currently selected race for editing.
    /// </summary>
    [ObservableProperty]
    private RaceViewModel? _selectedRace;

    #endregion

    #region Application Info

    /// <summary>
    /// Gets the application title.
    /// </summary>
    public string Title => "Scalextric Race";

    /// <summary>
    /// Gets the application version.
    /// </summary>
    public string Version => "1.0.0";

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the MainViewModel with dependencies.
    /// </summary>
    /// <param name="settings">The application settings.</param>
    /// <param name="windowService">The window service for dialogs.</param>
    /// <param name="bleService">The BLE service for device communication.</param>
    /// <param name="powerHeartbeatService">The power heartbeat service for maintaining track power.</param>
    /// <param name="carStorage">The car storage service.</param>
    /// <param name="driverStorage">The driver storage service.</param>
    /// <param name="raceStorage">The race storage service.</param>
    public MainViewModel(AppSettings settings, IWindowService windowService,
        Services.IBleService? bleService = null, IPowerHeartbeatService? powerHeartbeatService = null,
        ICarStorage? carStorage = null, IDriverStorage? driverStorage = null, IRaceStorage? raceStorage = null)
    {
        _settings = settings;
        _windowService = windowService;
        _bleService = bleService;
        _powerHeartbeatService = powerHeartbeatService;
        _carStorage = carStorage ?? new CarStorage();
        _driverStorage = driverStorage ?? new DriverStorage();
        _raceStorage = raceStorage ?? new RaceStorage();
        _syncContext = SynchronizationContext.Current;

        // Load startup global settings (ultra-safe values)
        PowerLevel = _settings.Startup.PowerLevel;
        SelectedThrottleProfile = Enum.TryParse<ThrottleProfileType>(_settings.Startup.ThrottleProfile, out var profile)
            ? profile
            : ThrottleProfileType.Linear;

        // Load per-slot power mode setting
        IsPerSlotPowerMode = _settings.Startup.IsPerSlotPowerMode;

        // Load startup power state - will be applied when connected
        IsPowerEnabled = _settings.StartWithPowerEnabled;

        // Initialize controllers for all 6 slots
        for (int i = 1; i <= 6; i++)
        {
            var controller = new ControllerViewModel(i);

            // Load startup per-slot settings
            var slotStartup = _settings.Startup.SlotSettings[i - 1];
            controller.PowerLevel = slotStartup.PowerLevel;
            controller.ThrottleProfile = Enum.TryParse<ThrottleProfileType>(slotStartup.ThrottleProfile, out var slotProfile)
                ? slotProfile
                : ThrottleProfileType.Linear;

            // Subscribe to changes for auto-save
            controller.PowerLevelChanged += OnControllerPowerLevelChanged;
            controller.ThrottleProfileChanged += OnControllerThrottleProfileChanged;

            Controllers.Add(controller);
        }

        // Load cars, drivers, and races from storage
        LoadCars();
        LoadDrivers();
        LoadRaces();

        // Initialization complete - enable auto-save
        _isInitializing = false;

        Log.Information("MainViewModel initialized. PowerLevel={PowerLevel}, ThrottleProfile={ThrottleProfile}, PerSlotMode={PerSlotMode}, PowerEnabled={PowerEnabled}",
            PowerLevel, SelectedThrottleProfile, IsPerSlotPowerMode, IsPowerEnabled);

        // Subscribe to BLE service events
        if (_bleService != null)
        {
            _bleService.ConnectionStateChanged += OnConnectionStateChanged;
            _bleService.StatusMessageChanged += OnStatusMessageChanged;
            _bleService.NotificationReceived += OnNotificationReceived;
        }

        // Subscribe to heartbeat service events
        if (_powerHeartbeatService != null)
        {
            _powerHeartbeatService.HeartbeatError += OnHeartbeatError;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts monitoring for Scalextric devices.
    /// Called automatically when the application starts.
    /// </summary>
    public void StartMonitoring()
    {
        if (_bleService == null)
        {
            Log.Warning("BLE service not available - cannot start monitoring");
            StatusMessage = "BLE service not available";
            return;
        }

        Log.Information("Starting BLE monitoring");
        _bleService.StartScanning();
        IsScanning = _bleService.IsScanning;
    }

    /// <summary>
    /// Stops monitoring and cleans up resources.
    /// Called when the application is closing.
    /// </summary>
    public void StopMonitoring()
    {
        // Save settings before stopping
        SaveSettings();

        if (_bleService == null) return;

        Log.Information("Stopping BLE monitoring");

        // Disable power if enabled
        if (IsPowerEnabled)
        {
            DisablePower();
        }

        _bleService.StopScanning();
        IsScanning = false;
    }

    #endregion

    #region Commands

    /// <summary>
    /// Toggles track power on or off.
    /// </summary>
    [RelayCommand]
    private void TogglePower()
    {
        IsPowerEnabled = !IsPowerEnabled;

        if (IsPowerEnabled)
        {
            EnablePower();
        }
        else
        {
            DisablePower();
        }

        // Save settings when power state changes
        SaveSettings();
    }

    /// <summary>
    /// Toggles between global and per-slot power mode.
    /// </summary>
    [RelayCommand]
    private void TogglePerSlotPowerMode()
    {
        IsPerSlotPowerMode = !IsPerSlotPowerMode;
        Log.Information("Per-slot power mode toggled to {PerSlotMode}", IsPerSlotPowerMode);
        SaveSettings();
    }

    /// <summary>
    /// Toggles the hamburger menu open/closed state.
    /// </summary>
    [RelayCommand]
    private void ToggleMenu()
    {
        IsMenuOpen = !IsMenuOpen;
    }

    /// <summary>
    /// Navigates to the specified mode and closes the menu.
    /// </summary>
    /// <param name="mode">The navigation mode to switch to.</param>
    [RelayCommand]
    private void NavigateTo(NavigationMode mode)
    {
        CurrentMode = mode;
        IsMenuOpen = false;
        Log.Information("Navigated to {Mode} mode", mode);
    }

    /// <summary>
    /// Opens the configure screen for the specified race.
    /// </summary>
    /// <param name="race">The race to configure.</param>
    [RelayCommand]
    private void StartRacing(RaceViewModel? race)
    {
        if (race == null) return;

        ActiveRace = race;

        // Copy race settings to config properties
        ConfigFreePracticeEnabled = race.FreePracticeEnabled;
        ConfigFreePracticeMode = race.FreePracticeMode;
        ConfigFreePracticeLapCount = race.FreePracticeLapCount;
        ConfigFreePracticeTimeMinutes = race.FreePracticeTimeMinutes;

        ConfigQualifyingEnabled = race.QualifyingEnabled;
        ConfigQualifyingMode = race.QualifyingMode;
        ConfigQualifyingLapCount = race.QualifyingLapCount;
        ConfigQualifyingTimeMinutes = race.QualifyingTimeMinutes;

        ConfigRaceEnabled = race.RaceStageEnabled;
        ConfigRaceMode = race.RaceStageMode;
        ConfigRaceLapCount = race.RaceStageLapCount;
        ConfigRaceTimeMinutes = race.RaceStageTimeMinutes;

        ConfigDefaultPower = race.DefaultPower;

        // Initialize all 6 race entries (reset to default state)
        InitializeRaceEntries();

        CurrentAppMode = AppMode.Configure;
        Log.Information("Configuring race: {RaceName}", race.Name);
    }

    /// <summary>
    /// Initializes all 6 race entries for a new race configuration.
    /// Creates entries if they don't exist, loads saved entries from race if available.
    /// </summary>
    private void InitializeRaceEntries()
    {
        if (RaceEntries.Count == 0)
        {
            // Create all 6 entries
            for (int i = 1; i <= 6; i++)
            {
                var entry = new RaceEntryViewModel(i, Cars, Drivers);
                entry.PropertyChanged += OnRaceEntryPropertyChanged;
                entry.RaceDefaultPower = ConfigDefaultPower;
                RaceEntries.Add(entry);
            }
        }
        else
        {
            // Reset existing entries first
            foreach (var entry in RaceEntries)
            {
                entry.Reset();
                entry.RaceDefaultPower = ConfigDefaultPower;
            }
        }

        // Load saved entries from the active race
        if (ActiveRace != null)
        {
            var savedEntries = ActiveRace.GetModel().Entries;
            foreach (var savedEntry in savedEntries)
            {
                var entryVm = RaceEntries.FirstOrDefault(e => e.SlotNumber == savedEntry.SlotNumber);
                if (entryVm != null)
                {
                    entryVm.IsEnabled = savedEntry.IsEnabled;
                    if (savedEntry.CarId.HasValue)
                    {
                        entryVm.SelectedCar = Cars.FirstOrDefault(c => c.Id == savedEntry.CarId.Value);
                    }
                    if (savedEntry.DriverId.HasValue)
                    {
                        entryVm.SelectedDriver = Drivers.FirstOrDefault(d => d.Id == savedEntry.DriverId.Value);
                    }
                }
            }
        }

        UpdateCanStartRace();
    }

    /// <summary>
    /// Saves the current race entries to the active race.
    /// </summary>
    private void SaveRaceEntries()
    {
        if (ActiveRace == null) return;

        var race = ActiveRace.GetModel();
        race.Entries.Clear();

        foreach (var entry in RaceEntries)
        {
            race.Entries.Add(new Models.RaceEntry
            {
                SlotNumber = entry.SlotNumber,
                IsEnabled = entry.IsEnabled,
                CarId = entry.SelectedCar?.Id,
                DriverId = entry.SelectedDriver?.Id
            });
        }

        // Trigger save
        _raceStorage.Save(Races.Select(r => r.GetModel()).ToList());
        Log.Information("Saved race entries for {RaceName}", ActiveRace.Name);
    }

    private void OnRaceEntryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RaceEntryViewModel.IsConfigured))
        {
            UpdateCanStartRace();
        }
    }

    private void UpdateCanStartRace()
    {
        CanStartRace = RaceEntries.Any(e => e.IsConfigured);
    }

    private void ClearRaceEntries()
    {
        // Reset all entries to default state
        foreach (var entry in RaceEntries)
        {
            entry.Reset();
        }
        CanStartRace = false;
    }

    /// <summary>
    /// Proceeds from configure mode to racing mode.
    /// </summary>
    [RelayCommand]
    private void ProceedToRacing()
    {
        if (!CanStartRace) return;

        // Save entries before starting race
        SaveRaceEntries();

        CurrentAppMode = AppMode.Racing;
        Log.Information("Started racing: {RaceName} with {EntryCount} entries",
            ActiveRace?.Name, RaceEntries.Count(e => e.IsConfigured));
    }

    /// <summary>
    /// Exits configure mode and returns to setup.
    /// </summary>
    [RelayCommand]
    private void ExitConfigure()
    {
        Log.Information("Exiting configure mode");

        // Stop test mode if active
        StopTestMode();

        // Save entries before exiting
        SaveRaceEntries();

        ClearRaceEntries();
        CurrentAppMode = AppMode.Setup;
        ActiveRace = null;
    }

    /// <summary>
    /// Exits racing mode and returns to setup.
    /// </summary>
    [RelayCommand]
    private void ExitRacing()
    {
        Log.Information("Exiting racing mode");

        // Stop test mode if active
        StopTestMode();

        ClearRaceEntries();
        CurrentAppMode = AppMode.Setup;
        ActiveRace = null;
    }

    /// <summary>
    /// Toggles test mode on/off. When on, sends power to all enabled controllers
    /// based on their MaxThrottle settings.
    /// </summary>
    [RelayCommand]
    private void ToggleTestMode()
    {
        if (IsTestModeActive)
        {
            StopTestMode();
        }
        else
        {
            StartTestMode();
        }
    }

    /// <summary>
    /// Starts test mode - enables power for all configured controllers.
    /// </summary>
    private void StartTestMode()
    {
        if (_bleService == null || !IsConnected)
        {
            Log.Warning("Cannot start test mode - not connected");
            return;
        }

        IsTestModeActive = true;
        Log.Information("Test mode started");

        // Start heartbeat with test power settings
        _powerHeartbeatService?.Start(BuildTestPowerCommand);
    }

    /// <summary>
    /// Stops test mode - disables power.
    /// </summary>
    private void StopTestMode()
    {
        if (!IsTestModeActive) return;

        IsTestModeActive = false;
        Log.Information("Test mode stopped");

        // Stop heartbeat and send power off
        _ = _powerHeartbeatService?.SendPowerOffSequenceAsync();
        _powerHeartbeatService?.Stop();
    }

    /// <summary>
    /// Builds the power command for test mode based on race entry MaxThrottle values.
    /// </summary>
    private byte[] BuildTestPowerCommand()
    {
        var builder = new ScalextricProtocol.CommandBuilder
        {
            Type = ScalextricProtocol.CommandType.PowerOnRacing
        };

        // Set power for each slot based on enabled entries and their MaxThrottle
        foreach (var entry in RaceEntries)
        {
            var power = entry.IsEnabled ? (byte)entry.MaxThrottle : (byte)0;
            builder.SetSlotPower(entry.SlotNumber, power);
        }

        return builder.Build();
    }

    /// <summary>
    /// Adds a new car based on the default car template.
    /// </summary>
    [RelayCommand]
    private void AddCar()
    {
        // Find the default car to copy settings from
        var defaultCar = Cars.FirstOrDefault(c => c.IsDefault);

        var newCar = new Car($"Car {Cars.Count + 1}");

        // Copy power settings from default car if available
        if (defaultCar != null)
        {
            newCar.DefaultPower = defaultCar.DefaultPower;
            newCar.GhostMaxPower = defaultCar.GhostMaxPower;
            newCar.MinPower = defaultCar.MinPower;
        }

        var viewModel = new CarViewModel(newCar, isDefault: false);
        viewModel.DeleteRequested += OnCarDeleteRequested;
        viewModel.Changed += OnCarChanged;
        viewModel.TuneRequested += OnCarTuneRequested;
        viewModel.ImageChangeRequested += OnCarImageChangeRequested;
        Cars.Add(viewModel);
        SelectedCar = viewModel;
        Log.Information("Added new car: {CarName} (copied settings from default)", newCar.Name);
        SaveCars();
    }

    /// <summary>
    /// Handles delete request from a car view model.
    /// </summary>
    private void OnCarDeleteRequested(object? sender, EventArgs e)
    {
        if (sender is CarViewModel car)
        {
            DeleteCar(car);
        }
    }

    /// <summary>
    /// Handles property change on a car view model.
    /// </summary>
    private void OnCarChanged(object? sender, EventArgs e)
    {
        SaveCars();
    }

    /// <summary>
    /// Handles tune request from a car view model.
    /// Opens the tuning window via the window service.
    /// </summary>
    private async void OnCarTuneRequested(object? sender, EventArgs e)
    {
        if (sender is CarViewModel car)
        {
            Log.Information("Opening tuning window for car: {CarName}", car.Name);
            await _windowService.ShowCarTuningDialogAsync(car, _bleService);
            SaveCars();
        }
    }

    /// <summary>
    /// Handles image change request from a car view model.
    /// Opens a file picker and copies the image via the window service.
    /// </summary>
    private async void OnCarImageChangeRequested(object? sender, EventArgs e)
    {
        if (sender is CarViewModel car)
        {
            Log.Information("Image change requested for car: {CarName}", car.Name);
            var imagePath = await _windowService.PickAndCopyImageAsync("Select Car Image", car.Id);
            if (imagePath != null)
            {
                car.ImagePath = imagePath;
                SaveCars();
            }
        }
    }

    /// <summary>
    /// Deletes the specified car (cannot delete the default car).
    /// </summary>
    /// <param name="car">The car view model to delete.</param>
    private void DeleteCar(CarViewModel? car)
    {
        if (car == null || car.IsDefault)
        {
            Log.Warning("Cannot delete null or default car");
            return;
        }

        car.DeleteRequested -= OnCarDeleteRequested;
        car.Changed -= OnCarChanged;
        car.TuneRequested -= OnCarTuneRequested;
        car.ImageChangeRequested -= OnCarImageChangeRequested;
        Cars.Remove(car);
        if (SelectedCar == car)
        {
            SelectedCar = null;
        }
        Log.Information("Deleted car: {CarName}", car.Name);
        SaveCars();
    }

    /// <summary>
    /// Loads cars from storage.
    /// Ensures the default car is always present.
    /// </summary>
    private void LoadCars()
    {
        var storedCars = _carStorage.Load();

        // Check if default car exists in storage
        var hasDefaultCar = storedCars.Any(c => c.Id == Car.DefaultCarId);

        if (!hasDefaultCar)
        {
            // Create default car if not in storage
            var defaultCar = Car.CreateDefault();
            storedCars.Insert(0, defaultCar);
        }

        // Create view models for all cars
        foreach (var car in storedCars)
        {
            var isDefault = car.Id == Car.DefaultCarId;
            var viewModel = new CarViewModel(car, isDefault);
            viewModel.DeleteRequested += OnCarDeleteRequested;
            viewModel.Changed += OnCarChanged;
            viewModel.TuneRequested += OnCarTuneRequested;
            viewModel.ImageChangeRequested += OnCarImageChangeRequested;
            Cars.Add(viewModel);
        }

        Log.Information("Loaded {Count} cars", Cars.Count);
    }

    /// <summary>
    /// Saves all cars to storage.
    /// </summary>
    private void SaveCars()
    {
        if (_isInitializing) return;

        var cars = Cars.Select(vm => vm.GetModel());
        _carStorage.Save(cars);
    }

    /// <summary>
    /// Saves all data on application shutdown.
    /// Called from MainWindow.Closing event.
    /// </summary>
    public void SaveAllOnShutdown()
    {
        Log.Information("Saving all data on shutdown");

        // Force save all collections
        var cars = Cars.Select(vm => vm.GetModel());
        _carStorage.Save(cars);

        var drivers = Drivers.Select(vm => vm.GetModel());
        _driverStorage.Save(drivers);

        var races = Races.Select(vm => vm.GetModel());
        _raceStorage.Save(races);

        // Save current race entries if in configure mode
        if (CurrentAppMode == AppMode.Configure)
        {
            SaveRaceEntries();
        }

        SaveSettings();

        Log.Information("All data saved on shutdown");
    }

    /// <summary>
    /// Adds a new driver.
    /// </summary>
    [RelayCommand]
    private void AddDriver()
    {
        var newDriver = new Driver($"Driver {Drivers.Count + 1}");
        var viewModel = new DriverViewModel(newDriver, isDefault: false);
        viewModel.DeleteRequested += OnDriverDeleteRequested;
        viewModel.Changed += OnDriverChanged;
        viewModel.ImageChangeRequested += OnDriverImageChangeRequested;
        Drivers.Add(viewModel);
        SelectedDriver = viewModel;
        Log.Information("Added new driver: {DriverName}", newDriver.Name);
        SaveDrivers();
    }

    /// <summary>
    /// Handles delete request from a driver view model.
    /// </summary>
    private void OnDriverDeleteRequested(object? sender, EventArgs e)
    {
        if (sender is DriverViewModel driver)
        {
            DeleteDriver(driver);
        }
    }

    /// <summary>
    /// Handles property change on a driver view model.
    /// </summary>
    private void OnDriverChanged(object? sender, EventArgs e)
    {
        SaveDrivers();
    }

    /// <summary>
    /// Handles image change request from a driver view model.
    /// Opens a file picker and copies the image via the window service.
    /// </summary>
    private async void OnDriverImageChangeRequested(object? sender, EventArgs e)
    {
        if (sender is DriverViewModel driver)
        {
            Log.Information("Image change requested for driver: {DriverName}", driver.Name);
            var imagePath = await _windowService.PickAndCopyImageAsync("Select Driver Image", driver.Id, ImageConstants.DriverImagePrefix);
            if (imagePath != null)
            {
                driver.ImagePath = imagePath;
                SaveDrivers();
            }
        }
    }

    /// <summary>
    /// Deletes the specified driver (cannot delete the default driver).
    /// </summary>
    /// <param name="driver">The driver view model to delete.</param>
    private void DeleteDriver(DriverViewModel? driver)
    {
        if (driver == null || driver.IsDefault)
        {
            Log.Warning("Cannot delete null or default driver");
            return;
        }

        driver.DeleteRequested -= OnDriverDeleteRequested;
        driver.Changed -= OnDriverChanged;
        driver.ImageChangeRequested -= OnDriverImageChangeRequested;
        Drivers.Remove(driver);
        if (SelectedDriver == driver)
        {
            SelectedDriver = null;
        }
        Log.Information("Deleted driver: {DriverName}", driver.Name);
        SaveDrivers();
    }

    /// <summary>
    /// Loads drivers from storage.
    /// Ensures the default driver is always present.
    /// </summary>
    private void LoadDrivers()
    {
        var storedDrivers = _driverStorage.Load();

        // Check if default driver exists in storage
        var hasDefaultDriver = storedDrivers.Any(d => d.Id == Driver.DefaultDriverId);

        if (!hasDefaultDriver)
        {
            // Create default driver if not in storage
            var defaultDriver = Driver.CreateDefault();
            storedDrivers.Insert(0, defaultDriver);
        }

        // Create view models for all drivers
        foreach (var driver in storedDrivers)
        {
            var isDefault = driver.Id == Driver.DefaultDriverId;
            var viewModel = new DriverViewModel(driver, isDefault);
            viewModel.DeleteRequested += OnDriverDeleteRequested;
            viewModel.Changed += OnDriverChanged;
            viewModel.ImageChangeRequested += OnDriverImageChangeRequested;
            Drivers.Add(viewModel);
        }

        Log.Information("Loaded {Count} drivers", Drivers.Count);
    }

    /// <summary>
    /// Saves all drivers to storage.
    /// </summary>
    private void SaveDrivers()
    {
        if (_isInitializing) return;

        var drivers = Drivers.Select(vm => vm.GetModel());
        _driverStorage.Save(drivers);
    }

    /// <summary>
    /// Adds a new race configuration.
    /// </summary>
    [RelayCommand]
    private void AddRace()
    {
        var newRace = new Race { Name = $"Race {Races.Count + 1}" };
        var viewModel = new RaceViewModel(newRace, isDefault: false);
        viewModel.DeleteRequested += OnRaceDeleteRequested;
        viewModel.Changed += OnRaceChanged;
        viewModel.ImageChangeRequested += OnRaceImageChangeRequested;
        viewModel.EditRequested += OnRaceEditRequested;
        viewModel.StartRequested += OnRaceStartRequested;
        Races.Add(viewModel);
        SelectedRace = viewModel;
        Log.Information("Added new race: {RaceName}", newRace.Name);
        SaveRaces();
    }

    /// <summary>
    /// Handles delete request from a race view model.
    /// </summary>
    private void OnRaceDeleteRequested(object? sender, EventArgs e)
    {
        if (sender is RaceViewModel race)
        {
            DeleteRace(race);
        }
    }

    /// <summary>
    /// Handles property change on a race view model.
    /// </summary>
    private void OnRaceChanged(object? sender, EventArgs e)
    {
        SaveRaces();
    }

    /// <summary>
    /// Handles image change request from a race view model.
    /// Opens a file picker and copies the image via the window service.
    /// </summary>
    private async void OnRaceImageChangeRequested(object? sender, EventArgs e)
    {
        if (sender is RaceViewModel race)
        {
            Log.Information("Image change requested for race: {RaceName}", race.Name);
            var imagePath = await _windowService.PickAndCopyImageAsync("Select Race Image", race.Id, ImageConstants.RaceImagePrefix);
            if (imagePath != null)
            {
                race.ImagePath = imagePath;
                SaveRaces();
            }
        }
    }

    /// <summary>
    /// Handles edit request from a race view model.
    /// Opens the race config editing window.
    /// </summary>
    private async void OnRaceEditRequested(object? sender, EventArgs e)
    {
        if (sender is RaceViewModel race)
        {
            Log.Information("Edit requested for race: {RaceName}", race.Name);
            await _windowService.ShowRaceConfigDialogAsync(race);
            SaveRaces();
        }
    }

    /// <summary>
    /// Handles start request from a race view model.
    /// Switches to racing mode.
    /// </summary>
    private void OnRaceStartRequested(object? sender, EventArgs e)
    {
        if (sender is RaceViewModel race)
        {
            StartRacing(race);
        }
    }

    /// <summary>
    /// Deletes the specified race (cannot delete the default race).
    /// </summary>
    /// <param name="race">The race view model to delete.</param>
    private void DeleteRace(RaceViewModel? race)
    {
        if (race == null || race.IsDefault)
        {
            Log.Warning("Cannot delete null or default race");
            return;
        }

        race.DeleteRequested -= OnRaceDeleteRequested;
        race.Changed -= OnRaceChanged;
        race.ImageChangeRequested -= OnRaceImageChangeRequested;
        race.EditRequested -= OnRaceEditRequested;
        race.StartRequested -= OnRaceStartRequested;
        Races.Remove(race);
        if (SelectedRace == race)
        {
            SelectedRace = null;
        }
        Log.Information("Deleted race: {RaceName}", race.Name);
        SaveRaces();
    }

    /// <summary>
    /// Loads races from storage.
    /// Ensures the default race is always present.
    /// </summary>
    private void LoadRaces()
    {
        var storedRaces = _raceStorage.Load();

        // Check if default race exists in storage
        var hasDefaultRace = storedRaces.Any(r => r.Id == Race.DefaultRaceId);

        if (!hasDefaultRace)
        {
            // Create default race if not in storage
            var defaultRace = Race.CreateDefault();
            storedRaces.Insert(0, defaultRace);
        }

        // Create view models for all races
        foreach (var race in storedRaces)
        {
            var isDefault = race.Id == Race.DefaultRaceId;
            var viewModel = new RaceViewModel(race, isDefault);
            viewModel.DeleteRequested += OnRaceDeleteRequested;
            viewModel.Changed += OnRaceChanged;
            viewModel.ImageChangeRequested += OnRaceImageChangeRequested;
            viewModel.EditRequested += OnRaceEditRequested;
            viewModel.StartRequested += OnRaceStartRequested;
            Races.Add(viewModel);
        }

        Log.Information("Loaded {Count} races", Races.Count);
    }

    /// <summary>
    /// Saves all races to storage.
    /// </summary>
    private void SaveRaces()
    {
        if (_isInitializing) return;

        var races = Races.Select(vm => vm.GetModel());
        _raceStorage.Save(races);
    }

    #endregion

    #region Partial Methods (Property Change Handlers)

    /// <summary>
    /// Called when PowerLevel changes. Saves settings and sends command if power is on.
    /// </summary>
    partial void OnPowerLevelChanged(int value)
    {
        SaveSettings();
        if (IsPowerEnabled && !IsPerSlotPowerMode)
        {
            SendPowerCommand();
        }
    }

    /// <summary>
    /// Called when SelectedThrottleProfile changes. Saves settings.
    /// </summary>
    partial void OnSelectedThrottleProfileChanged(ThrottleProfileType value)
    {
        SaveSettings();
    }

    /// <summary>
    /// Called when IsPerSlotPowerMode changes. Sends power command if power is on.
    /// </summary>
    partial void OnIsPerSlotPowerModeChanged(bool value)
    {
        if (IsPowerEnabled)
        {
            SendPowerCommand();
        }
    }

    /// <summary>
    /// Called when ConfigDefaultPower changes. Updates all race entries with the new default power.
    /// </summary>
    partial void OnConfigDefaultPowerChanged(int value)
    {
        foreach (var entry in RaceEntries)
        {
            entry.RaceDefaultPower = value;
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles BLE connection state changes.
    /// </summary>
    private void OnConnectionStateChanged(object? sender, BleConnectionStateEventArgs e)
    {
        // Marshal to UI thread using SynchronizationContext
        PostToUIThread(() =>
        {
            var wasConnected = IsGattConnected;

            IsDeviceDetected = e.IsDeviceDetected;
            IsGattConnected = e.IsGattConnected;

            // If we just connected and power should be on, send the power command
            if (!wasConnected && IsGattConnected && IsPowerEnabled)
            {
                Log.Information("Connection established, enabling power from saved settings");
                EnablePower();
            }
        });
    }

    /// <summary>
    /// Handles BLE status message changes.
    /// </summary>
    private void OnStatusMessageChanged(object? sender, string message)
    {
        PostToUIThread(() =>
        {
            StatusMessage = message;
        });
    }

    /// <summary>
    /// Handles BLE notification data.
    /// </summary>
    private void OnNotificationReceived(object? sender, BleNotificationEventArgs e)
    {
        // Process notifications (throttle, lap timing, etc.)
        // TODO: Implement notification handling
    }

    /// <summary>
    /// Handles power level changes from individual controllers.
    /// </summary>
    private void OnControllerPowerLevelChanged(object? sender, int value)
    {
        SaveSettings();
        if (IsPowerEnabled && IsPerSlotPowerMode)
        {
            SendPowerCommand();
        }
    }

    /// <summary>
    /// Handles throttle profile changes from individual controllers.
    /// </summary>
    private void OnControllerThrottleProfileChanged(object? sender, ThrottleProfileType value)
    {
        SaveSettings();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Saves current settings to disk.
    /// </summary>
    private void SaveSettings()
    {
        // Don't save during initialization - we're loading values, not changing them
        if (_isInitializing)
        {
            return;
        }

        _settings.StartWithPowerEnabled = IsPowerEnabled;
        _settings.Startup.PowerLevel = PowerLevel;
        _settings.Startup.ThrottleProfile = SelectedThrottleProfile.ToString();
        _settings.Startup.IsPerSlotPowerMode = IsPerSlotPowerMode;

        // Save per-slot startup settings
        for (int i = 0; i < Controllers.Count; i++)
        {
            var controller = Controllers[i];
            _settings.Startup.SlotSettings[i].PowerLevel = controller.PowerLevel;
            _settings.Startup.SlotSettings[i].ThrottleProfile = controller.ThrottleProfile.ToString();
        }

        _settings.Save();
    }

    /// <summary>
    /// Enables track power and starts the heartbeat loop.
    /// </summary>
    private void EnablePower()
    {
        if (_powerHeartbeatService == null)
        {
            Log.Warning("Power heartbeat service not available");
            StatusMessage = "Power service not available";
            IsPowerEnabled = false;
            SaveSettings();
            return;
        }

        Log.Information("Enabling track power");
        _powerHeartbeatService.Start(BuildPowerCommand);
        StatusMessage = "Power enabled";
    }

    /// <summary>
    /// Sends the current power settings to the powerbase.
    /// Called when power is enabled or when settings change while power is on.
    /// </summary>
    private async void SendPowerCommand()
    {
        await SendPowerCommandAsync();
    }

    /// <summary>
    /// Sends the current power settings to the powerbase.
    /// </summary>
    /// <returns>True if the command was sent successfully.</returns>
    private async Task<bool> SendPowerCommandAsync()
    {
        if (_bleService == null) return false;

        // Build power-on command using ScalextricBle library
        var builder = new ScalextricProtocol.CommandBuilder
        {
            Type = ScalextricProtocol.CommandType.PowerOnRacing
        };

        if (IsPerSlotPowerMode)
        {
            // Set individual power levels per slot
            for (int i = 0; i < Controllers.Count; i++)
            {
                builder.SetSlotPower(i + 1, (byte)Controllers[i].PowerLevel);
            }
        }
        else
        {
            // Set global power level for all slots
            builder.SetAllPower((byte)PowerLevel);
        }

        byte[] command = builder.Build();

        return await _bleService.WriteCharacteristicAsync(
            ScalextricProtocol.Characteristics.Command,
            command);
    }

    /// <summary>
    /// Builds the power command based on current settings.
    /// Used as the delegate for the power heartbeat service.
    /// </summary>
    /// <returns>The 20-byte power command to send to the powerbase.</returns>
    private byte[] BuildPowerCommand()
    {
        var builder = new ScalextricProtocol.CommandBuilder
        {
            Type = ScalextricProtocol.CommandType.PowerOnRacing
        };

        if (IsPerSlotPowerMode)
        {
            // Set individual power levels per slot
            for (int i = 0; i < Controllers.Count; i++)
            {
                builder.SetSlotPower(i + 1, (byte)Controllers[i].PowerLevel);
            }
        }
        else
        {
            // Set global power level for all slots
            builder.SetAllPower((byte)PowerLevel);
        }

        return builder.Build();
    }

    /// <summary>
    /// Handles heartbeat errors from the power heartbeat service.
    /// </summary>
    private void OnHeartbeatError(object? sender, string errorMessage)
    {
        Log.Error("Power heartbeat error: {ErrorMessage}", errorMessage);

        PostToUIThread(() =>
        {
            StatusMessage = errorMessage;
            IsPowerEnabled = false;
            SaveSettings();
        });
    }

    /// <summary>
    /// Disables track power and stops the heartbeat loop.
    /// </summary>
    private async void DisablePower()
    {
        if (_powerHeartbeatService == null) return;

        Log.Information("Disabling track power");

        // Stop the heartbeat loop first
        _powerHeartbeatService.Stop();

        // Send power-off sequence (clear ghost + power off)
        await _powerHeartbeatService.SendPowerOffSequenceAsync();

        Log.Information("Power disabled successfully");
        StatusMessage = "Power disabled";
    }

    /// <summary>
    /// Posts an action to the UI thread using SynchronizationContext.
    /// Falls back to direct execution if no context is available.
    /// </summary>
    private void PostToUIThread(Action action)
    {
        if (_syncContext != null)
        {
            _syncContext.Post(_ => action(), null);
        }
        else
        {
            // No sync context - execute directly (may be on wrong thread)
            action();
        }
    }

    #endregion
}
