using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scalextric;
using ScalextricBle;
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

    private readonly IBleService? _bleService;
    private readonly AppSettings _settings;
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
    /// The current navigation mode/page.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRaceMode))]
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
    /// Gets whether Race mode is active.
    /// </summary>
    public bool IsRaceMode => CurrentMode == NavigationMode.Race;

    /// <summary>
    /// Gets whether Cars mode is active.
    /// </summary>
    public bool IsCarsMode => CurrentMode == NavigationMode.Cars;

    /// <summary>
    /// Gets whether Drivers mode is active.
    /// </summary>
    public bool IsDriversMode => CurrentMode == NavigationMode.Drivers;

    /// <summary>
    /// Gets whether Settings mode is active.
    /// </summary>
    public bool IsSettingsMode => CurrentMode == NavigationMode.Settings;

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
    /// <param name="bleService">The BLE service for device communication.</param>
    /// <param name="settings">The application settings.</param>
    public MainViewModel(AppSettings settings, IBleService? bleService = null)
    {
        _settings = settings;
        _bleService = bleService;
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

        // Load cars from storage
        LoadCars();

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
    /// </summary>
    private void OnCarTuneRequested(object? sender, EventArgs e)
    {
        if (sender is CarViewModel car)
        {
            OpenTuningWindow(car);
        }
    }

    /// <summary>
    /// Event raised when a tuning window should be opened.
    /// The view subscribes to this event and opens the window.
    /// </summary>
    public event EventHandler<CarViewModel>? TuneWindowRequested;

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
        Cars.Remove(car);
        if (SelectedCar == car)
        {
            SelectedCar = null;
        }
        Log.Information("Deleted car: {CarName}", car.Name);
        SaveCars();
    }

    /// <summary>
    /// Opens the tuning window for the specified car.
    /// </summary>
    /// <param name="car">The car to tune.</param>
    private void OpenTuningWindow(CarViewModel car)
    {
        Log.Information("Opening tuning window for car: {CarName}", car.Name);
        TuneWindowRequested?.Invoke(this, car);
    }

    /// <summary>
    /// Loads cars from storage.
    /// Ensures the default car is always present.
    /// </summary>
    private void LoadCars()
    {
        var storedCars = CarStorage.Load();

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
        CarStorage.Save(cars);
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
    private async void EnablePower()
    {
        var success = await SendPowerCommandAsync();

        if (success)
        {
            Log.Information("Power enabled successfully");
            StatusMessage = "Power enabled";
            // TODO: Start heartbeat loop (200ms interval)
        }
        else
        {
            Log.Warning("Failed to enable power");
            StatusMessage = "Failed to enable power";
            IsPowerEnabled = false;
            SaveSettings();
        }
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
    /// Disables track power and stops the heartbeat loop.
    /// </summary>
    private async void DisablePower()
    {
        if (_bleService == null) return;

        Log.Information("Disabling track power");

        // Build and send power-off command
        byte[] command = ScalextricProtocol.CommandBuilder.CreatePowerOffCommand();

        var success = await _bleService.WriteCharacteristicAsync(
            ScalextricProtocol.Characteristics.Command,
            command);

        if (success)
        {
            Log.Information("Power disabled successfully");
            StatusMessage = "Power disabled";
            // TODO: Stop heartbeat loop
        }
        else
        {
            Log.Warning("Failed to disable power");
            StatusMessage = "Failed to disable power";
        }
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
