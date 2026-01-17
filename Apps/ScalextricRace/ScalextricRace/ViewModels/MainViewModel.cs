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

    private readonly IPowerHeartbeatService? _powerHeartbeatService;
    private readonly IWindowService _windowService;
    private readonly AppSettings _settings;
    private readonly ICarStorage _carStorage;
    private bool _isInitializing = true;

    // Child ViewModels
    private readonly BleConnectionViewModel _connection;
    private readonly CarManagementViewModel _carManagement;
    private readonly DriverManagementViewModel _driverManagement;
    private readonly RaceManagementViewModel _raceManagement;
    private readonly PowerControlViewModel _powerControl;
    private readonly RaceConfigurationViewModel _raceConfig;

    #endregion

    #region Connection State (Delegated to BleConnectionViewModel)

    /// <summary>
    /// Indicates whether BLE scanning is active.
    /// </summary>
    public bool IsScanning => _connection.IsScanning;

    /// <summary>
    /// Indicates whether a Scalextric device has been detected via BLE advertisement.
    /// </summary>
    public bool IsDeviceDetected => _connection.IsDeviceDetected;

    /// <summary>
    /// Indicates whether an active GATT connection exists to the powerbase.
    /// </summary>
    public bool IsGattConnected => _connection.IsGattConnected;

    /// <summary>
    /// Status message to display to the user.
    /// </summary>
    public string StatusMessage
    {
        get => _connection.StatusMessage;
        set => _connection.StatusMessage = value;
    }

    /// <summary>
    /// Gets whether the device is connected and ready for commands.
    /// </summary>
    public bool IsConnected => _connection.IsConnected;

    /// <summary>
    /// Gets the current connection status as a display string.
    /// </summary>
    public string ConnectionStatusText => _connection.ConnectionStatusText;

    /// <summary>
    /// Gets the current connection state for UI display.
    /// Used with a converter to determine status indicator color.
    /// </summary>
    public ConnectionState CurrentConnectionState => _connection.CurrentConnectionState;

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
    /// Gets the power control child ViewModel.
    /// </summary>
    public PowerControlViewModel PowerControl => _powerControl;

    /// <summary>
    /// The global power level (0-63) applied to all slots.
    /// </summary>
    public int PowerLevel
    {
        get => _powerControl.PowerLevel;
        set => _powerControl.PowerLevel = value;
    }

    /// <summary>
    /// The selected throttle profile type for all cars.
    /// </summary>
    public ThrottleProfileType SelectedThrottleProfile
    {
        get => _powerControl.SelectedThrottleProfile;
        set => _powerControl.SelectedThrottleProfile = value;
    }

    /// <summary>
    /// Available throttle profile types for selection.
    /// </summary>
    public static ThrottleProfileType[] AvailableThrottleProfiles => PowerControlViewModel.AvailableThrottleProfiles;

    /// <summary>
    /// Whether per-slot power mode is enabled.
    /// </summary>
    public bool IsPerSlotPowerMode
    {
        get => _powerControl.IsPerSlotPowerMode;
        set => _powerControl.IsPerSlotPowerMode = value;
    }

    /// <summary>
    /// Gets the text for the per-slot power mode toggle button.
    /// </summary>
    public string PerSlotToggleText => _powerControl.PerSlotToggleText;

    /// <summary>
    /// Collection of controller view models for per-slot power settings.
    /// </summary>
    public ObservableCollection<ControllerViewModel> Controllers => _powerControl.Controllers;

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
    /// Gets the race configuration child ViewModel.
    /// </summary>
    public RaceConfigurationViewModel RaceConfig => _raceConfig;

    /// <summary>
    /// Collection of race entries (car/driver pairings) for the current race.
    /// </summary>
    public ObservableCollection<RaceEntryViewModel> RaceEntries => _raceConfig.RaceEntries;

    /// <summary>
    /// Gets whether the race can be started (at least one configured entry).
    /// </summary>
    public bool CanStartRace => _raceConfig.CanStartRace;

    // Race stage configuration - delegated to RaceConfigurationViewModel
    public bool ConfigFreePracticeEnabled { get => _raceConfig.ConfigFreePracticeEnabled; set => _raceConfig.ConfigFreePracticeEnabled = value; }
    public RaceStageMode ConfigFreePracticeMode { get => _raceConfig.ConfigFreePracticeMode; set => _raceConfig.ConfigFreePracticeMode = value; }
    public int ConfigFreePracticeLapCount { get => _raceConfig.ConfigFreePracticeLapCount; set => _raceConfig.ConfigFreePracticeLapCount = value; }
    public int ConfigFreePracticeTimeMinutes { get => _raceConfig.ConfigFreePracticeTimeMinutes; set => _raceConfig.ConfigFreePracticeTimeMinutes = value; }

    public bool ConfigQualifyingEnabled { get => _raceConfig.ConfigQualifyingEnabled; set => _raceConfig.ConfigQualifyingEnabled = value; }
    public RaceStageMode ConfigQualifyingMode { get => _raceConfig.ConfigQualifyingMode; set => _raceConfig.ConfigQualifyingMode = value; }
    public int ConfigQualifyingLapCount { get => _raceConfig.ConfigQualifyingLapCount; set => _raceConfig.ConfigQualifyingLapCount = value; }
    public int ConfigQualifyingTimeMinutes { get => _raceConfig.ConfigQualifyingTimeMinutes; set => _raceConfig.ConfigQualifyingTimeMinutes = value; }

    public bool ConfigRaceEnabled { get => _raceConfig.ConfigRaceEnabled; set => _raceConfig.ConfigRaceEnabled = value; }
    public RaceStageMode ConfigRaceMode { get => _raceConfig.ConfigRaceMode; set => _raceConfig.ConfigRaceMode = value; }
    public int ConfigRaceLapCount { get => _raceConfig.ConfigRaceLapCount; set => _raceConfig.ConfigRaceLapCount = value; }
    public int ConfigRaceTimeMinutes { get => _raceConfig.ConfigRaceTimeMinutes; set => _raceConfig.ConfigRaceTimeMinutes = value; }

    /// <summary>
    /// Default power level (0-63) for entries without a car/driver configured.
    /// </summary>
    public int ConfigDefaultPower { get => _raceConfig.ConfigDefaultPower; set => _raceConfig.ConfigDefaultPower = value; }

    /// <summary>
    /// Whether test mode is active (power on for testing controllers).
    /// </summary>
    public bool IsTestModeActive { get => _raceConfig.IsTestModeActive; set => _raceConfig.IsTestModeActive = value; }

    /// <summary>
    /// Gets the text for the test mode toggle button.
    /// </summary>
    public string TestButtonText => _raceConfig.TestButtonText;

    /// <summary>
    /// Gets the display string for Free Practice configuration.
    /// </summary>
    public string ConfigFreePracticeDisplay => _raceConfig.ConfigFreePracticeDisplay;

    /// <summary>
    /// Gets the display string for Qualifying configuration.
    /// </summary>
    public string ConfigQualifyingDisplay => _raceConfig.ConfigQualifyingDisplay;

    /// <summary>
    /// Gets the display string for Race configuration.
    /// </summary>
    public string ConfigRaceDisplay => _raceConfig.ConfigRaceDisplay;

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
    /// Gets the car management view model.
    /// </summary>
    public CarManagementViewModel CarManagement => _carManagement;

    /// <summary>
    /// Collection of all cars available for racing.
    /// Delegates to CarManagementViewModel.
    /// </summary>
    public ObservableCollection<CarViewModel> Cars => _carManagement.Cars;

    /// <summary>
    /// The currently selected car for editing.
    /// Delegates to CarManagementViewModel.
    /// </summary>
    public CarViewModel? SelectedCar
    {
        get => _carManagement.SelectedCar;
        set => _carManagement.SelectedCar = value;
    }

    #endregion

    #region Driver Management

    /// <summary>
    /// Gets the driver management child ViewModel.
    /// </summary>
    public DriverManagementViewModel DriverManagement => _driverManagement;

    /// <summary>
    /// Collection of all drivers available for racing.
    /// </summary>
    public ObservableCollection<DriverViewModel> Drivers => _driverManagement.Drivers;

    /// <summary>
    /// The currently selected driver for editing.
    /// </summary>
    public DriverViewModel? SelectedDriver
    {
        get => _driverManagement.SelectedDriver;
        set => _driverManagement.SelectedDriver = value;
    }

    #endregion

    #region Race Management

    /// <summary>
    /// Gets the race management child ViewModel.
    /// </summary>
    public RaceManagementViewModel RaceManagement => _raceManagement;

    /// <summary>
    /// Collection of all race configurations.
    /// </summary>
    public ObservableCollection<RaceViewModel> Races => _raceManagement.Races;

    /// <summary>
    /// The currently selected race for editing.
    /// </summary>
    public RaceViewModel? SelectedRace
    {
        get => _raceManagement.SelectedRace;
        set => _raceManagement.SelectedRace = value;
    }

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
    /// <param name="connection">The BLE connection view model.</param>
    /// <param name="carManagement">The car management view model.</param>
    /// <param name="driverManagement">The driver management view model.</param>
    /// <param name="raceManagement">The race management view model.</param>
    /// <param name="powerControl">The power control view model.</param>
    /// <param name="raceConfig">The race configuration view model.</param>
    /// <param name="powerHeartbeatService">The power heartbeat service for maintaining track power.</param>
    /// <param name="carStorage">The car storage service.</param>
    public MainViewModel(AppSettings settings, IWindowService windowService,
        BleConnectionViewModel connection,
        CarManagementViewModel carManagement, DriverManagementViewModel driverManagement,
        RaceManagementViewModel raceManagement, PowerControlViewModel powerControl,
        RaceConfigurationViewModel raceConfig,
        IPowerHeartbeatService? powerHeartbeatService = null,
        ICarStorage? carStorage = null)
    {
        _settings = settings;
        _windowService = windowService;
        _connection = connection;
        _carManagement = carManagement;
        _driverManagement = driverManagement;
        _raceManagement = raceManagement;
        _powerControl = powerControl;
        _raceConfig = raceConfig;
        _powerHeartbeatService = powerHeartbeatService;
        _carStorage = carStorage ?? new CarStorage();

        // Load startup power state - will be applied when connected
        // (Power settings are loaded by PowerControlViewModel)
        IsPowerEnabled = _settings.StartWithPowerEnabled;

        // Set up race management callback for start requests
        // (Cars/Drivers/Races are loaded by their respective ViewModels)
        _raceManagement.SetStartRequestedCallback(OnRaceStartRequested);

        // Wire up connection events
        _connection.GattConnected += OnGattConnected;
        _connection.NotificationReceived += OnNotificationReceived;
        _connection.PropertyChanged += (s, e) =>
        {
            // Forward property change notifications for delegated properties
            if (e.PropertyName == nameof(BleConnectionViewModel.IsScanning))
                OnPropertyChanged(nameof(IsScanning));
            else if (e.PropertyName == nameof(BleConnectionViewModel.IsDeviceDetected))
                OnPropertyChanged(nameof(IsDeviceDetected));
            else if (e.PropertyName == nameof(BleConnectionViewModel.IsGattConnected))
            {
                OnPropertyChanged(nameof(IsGattConnected));
                OnPropertyChanged(nameof(IsConnected));
            }
            else if (e.PropertyName == nameof(BleConnectionViewModel.StatusMessage))
                OnPropertyChanged(nameof(StatusMessage));
            else if (e.PropertyName == nameof(BleConnectionViewModel.ConnectionStatusText))
                OnPropertyChanged(nameof(ConnectionStatusText));
            else if (e.PropertyName == nameof(BleConnectionViewModel.CurrentConnectionState))
                OnPropertyChanged(nameof(CurrentConnectionState));
        };

        // Subscribe to heartbeat service events
        if (_powerHeartbeatService != null)
        {
            _powerHeartbeatService.HeartbeatError += OnHeartbeatError;
        }

        // Initialization complete - enable auto-save
        _isInitializing = false;

        Log.Information("MainViewModel initialized. PowerLevel={PowerLevel}, ThrottleProfile={ThrottleProfile}, PerSlotMode={PerSlotMode}, PowerEnabled={PowerEnabled}",
            PowerLevel, SelectedThrottleProfile, IsPerSlotPowerMode, IsPowerEnabled);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts monitoring for Scalextric devices.
    /// Called automatically when the application starts.
    /// </summary>
    public void StartMonitoring()
    {
        _connection.StartMonitoring();
    }

    /// <summary>
    /// Stops monitoring and cleans up resources.
    /// Called when the application is closing.
    /// </summary>
    public void StopMonitoring()
    {
        // Save settings before stopping
        SaveSettings();

        // Disable power if enabled
        if (IsPowerEnabled)
        {
            DisablePower();
        }

        _connection.StopMonitoring();
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

        // Load race settings via RaceConfigurationViewModel
        _raceConfig.LoadFromRace(race);

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
        // Initialize or reset entries via RaceConfigurationViewModel
        _raceConfig.InitializeRaceEntries(Cars, Drivers);

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

        _raceConfig.UpdateCanStartRace();
    }

    /// <summary>
    /// Saves the current race entries to the active race.
    /// </summary>
    private void SaveRaceEntries()
    {
        if (ActiveRace == null) return;

        _raceConfig.SaveRaceEntries(ActiveRace);
        _raceManagement.SaveRaces();
    }

    private void ClearRaceEntries()
    {
        // Reset all entries to default state
        foreach (var entry in RaceEntries)
        {
            entry.Reset();
        }
        _raceConfig.UpdateCanStartRace();
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
        if (!IsConnected)
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

    // Car management methods moved to CarManagementViewModel

    /// <summary>
    /// Saves all data on application shutdown.
    /// Called from MainWindow.Closing event.
    /// </summary>
    public void SaveAllOnShutdown()
    {
        Log.Information("Saving all data on shutdown");

        // Force save all collections
        _carManagement.SaveCars();
        _driverManagement.SaveDrivers();
        _raceManagement.SaveRaces();

        // Save current race entries if in configure mode
        if (CurrentAppMode == AppMode.Configure)
        {
            SaveRaceEntries();
        }

        SaveSettings();

        Log.Information("All data saved on shutdown");
    }

    // Driver management methods moved to DriverManagementViewModel
    // Most race management methods moved to RaceManagementViewModel

    /// <summary>
    /// Handles start request from a race view model.
    /// Switches to racing mode. This remains in MainViewModel because it affects AppMode.
    /// </summary>
    private void OnRaceStartRequested(RaceViewModel race)
    {
        StartRacing(race);
    }

    #endregion

    #region Partial Methods (Property Change Handlers)

    // Power control partial methods removed - handled by PowerControlViewModel
    // ConfigDefaultPower partial method removed - handled by RaceConfigurationViewModel

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles GATT connection established.
    /// </summary>
    private void OnGattConnected(object? sender, EventArgs e)
    {
        // If power should be on, enable it now that we're connected
        if (IsPowerEnabled)
        {
            Log.Information("Connection established, enabling power from saved settings");
            EnablePower();
        }
    }

    /// <summary>
    /// Handles BLE notification data.
    /// </summary>
    private void OnNotificationReceived(object? sender, BleNotificationEventArgs e)
    {
        // Process notifications (throttle, lap timing, etc.)
        // TODO: Implement notification handling
    }

    // Controller event handlers removed - handled by PowerControlViewModel

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
    /// Builds the power command based on current settings.
    /// Used as the delegate for the power heartbeat service.
    /// </summary>
    /// <returns>The 20-byte power command to send to the powerbase.</returns>
    private byte[] BuildPowerCommand()
    {
        return _powerControl.BuildPowerCommand(ScalextricProtocol.CommandType.PowerOnRacing, RaceEntries);
    }

    /// <summary>
    /// Handles heartbeat errors from the power heartbeat service.
    /// </summary>
    private void OnHeartbeatError(object? sender, string errorMessage)
    {
        Log.Error("Power heartbeat error: {ErrorMessage}", errorMessage);

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
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

    #endregion
}
