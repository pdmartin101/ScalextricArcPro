using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScalextricRace.Models;
using ScalextricRace.Services;
using Serilog;

namespace ScalextricRace.ViewModels;

/// <summary>
/// Manages the collection of drivers and driver-related operations.
/// Handles driver CRUD operations and image management.
/// </summary>
public partial class DriverManagementViewModel : ObservableObject
{
    private readonly IDriverStorage _driverStorage;
    private readonly IWindowService _windowService;
    private bool _isInitializing = true;

    /// <summary>
    /// Collection of all drivers available for racing.
    /// </summary>
    public ObservableCollection<DriverViewModel> Drivers { get; } = [];

    /// <summary>
    /// The currently selected driver for editing.
    /// </summary>
    [ObservableProperty]
    private DriverViewModel? _selectedDriver;

    /// <summary>
    /// Initializes a new instance of the DriverManagementViewModel.
    /// </summary>
    /// <param name="driverStorage">The driver storage service.</param>
    /// <param name="windowService">The window service for dialogs.</param>
    public DriverManagementViewModel(
        IDriverStorage driverStorage,
        IWindowService windowService)
    {
        _driverStorage = driverStorage;
        _windowService = windowService;

        LoadDrivers();
        _isInitializing = false;
    }

    /// <summary>
    /// Adds a new driver to the collection.
    /// </summary>
    [RelayCommand]
    private void AddDriver()
    {
        // Find the default driver to copy settings from
        var defaultDriver = Drivers.FirstOrDefault(d => d.IsDefault);

        var newDriver = new Driver($"Driver {Drivers.Count + 1}");

        // Copy power percentage from default driver if available
        if (defaultDriver != null)
        {
            newDriver.PowerPercentage = defaultDriver.PowerPercentage;
        }

        var viewModel = new DriverViewModel(newDriver, isDefault: false);
        viewModel.DeleteRequested += OnDriverDeleteRequested;
        viewModel.Changed += OnDriverChanged;
        viewModel.ImageChangeRequested += OnDriverImageChangeRequested;
        Drivers.Add(viewModel);
        SelectedDriver = viewModel;
        Log.Information("Added new driver: {DriverName} (copied settings from default)", newDriver.Name);
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
            var imagePath = await _windowService.PickAndCopyImageAsync("Select Driver Image", driver.Id);
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
    public void SaveDrivers()
    {
        if (_isInitializing) return;

        var drivers = Drivers.Select(vm => vm.GetModel());
        _driverStorage.Save(drivers);
    }
}
