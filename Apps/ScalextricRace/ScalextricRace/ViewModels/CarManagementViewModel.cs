using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScalextricRace.Models;
using ScalextricRace.Services;
using Serilog;

namespace ScalextricRace.ViewModels;

/// <summary>
/// Manages the collection of cars and car-related operations.
/// Handles car CRUD operations, tuning, and image management.
/// </summary>
public partial class CarManagementViewModel : ObservableObject
{
    private readonly ICarStorage _carStorage;
    private readonly IWindowService _windowService;
    private readonly Services.IBleService? _bleService;
    private bool _isInitializing = true;

    /// <summary>
    /// Collection of all cars available for racing.
    /// </summary>
    public ObservableCollection<CarViewModel> Cars { get; } = [];

    /// <summary>
    /// The currently selected car for editing.
    /// </summary>
    [ObservableProperty]
    private CarViewModel? _selectedCar;

    /// <summary>
    /// Initializes a new instance of the CarManagementViewModel.
    /// </summary>
    /// <param name="carStorage">The car storage service.</param>
    /// <param name="windowService">The window service for dialogs.</param>
    /// <param name="bleService">The BLE service for tuning operations.</param>
    public CarManagementViewModel(
        ICarStorage carStorage,
        IWindowService windowService,
        Services.IBleService? bleService = null)
    {
        _carStorage = carStorage;
        _windowService = windowService;
        _bleService = bleService;

        LoadCars();
        _isInitializing = false;
    }

    /// <summary>
    /// Adds a new car to the collection.
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
    public void SaveCars()
    {
        if (_isInitializing) return;

        var cars = Cars.Select(vm => vm.GetModel());
        _carStorage.Save(cars);
    }
}
