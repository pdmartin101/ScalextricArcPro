using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using ScalextricRace.Services;
using ScalextricRace.ViewModels;

namespace ScalextricRace.Views;

/// <summary>
/// Main window for the ScalextricRace application.
/// Uses MVVM pattern - all logic is in MainViewModel.
/// Lifecycle management is handled by App.axaml.cs.
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? _currentViewModel;
    private IWindowService? _windowService;

    /// <summary>
    /// Initializes the main window.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        // Subscribe to events after DataContext is set, unsubscribe from old context
        DataContextChanged += (_, _) =>
        {
            // Unsubscribe from old view model
            if (_currentViewModel != null)
            {
                _currentViewModel.TuneWindowRequested -= OnTuneWindowRequested;
                _currentViewModel.ImageChangeRequested -= OnImageChangeRequested;
                _currentViewModel.DriverImageChangeRequested -= OnDriverImageChangeRequested;
                _currentViewModel = null;
            }

            // Subscribe to new view model
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.TuneWindowRequested += OnTuneWindowRequested;
                viewModel.ImageChangeRequested += OnImageChangeRequested;
                viewModel.DriverImageChangeRequested += OnDriverImageChangeRequested;
                _currentViewModel = viewModel;

                // Create window service for this window
                _windowService = new WindowService(this);
            }
        };
    }

    /// <summary>
    /// Handles pointer press on the menu overlay to close the menu.
    /// </summary>
    private void OnMenuOverlayPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.IsMenuOpen = false;
        }
    }

    /// <summary>
    /// Opens the car tuning window for the specified car.
    /// </summary>
    private async void OnTuneWindowRequested(object? sender, CarViewModel car)
    {
        if (_windowService == null) return;

        var bleService = App.Services?.GetService<IBleService>();
        await _windowService.ShowCarTuningDialogAsync(car, bleService);
    }

    /// <summary>
    /// Opens a file picker to select an image for the specified car.
    /// </summary>
    private async void OnImageChangeRequested(object? sender, CarViewModel car)
    {
        if (_windowService == null) return;

        var sourcePath = await _windowService.ShowImagePickerAsync("Select Car Image");
        if (sourcePath != null)
        {
            car.ImagePath = _windowService.CopyImageToAppFolder(sourcePath, car.Id);
        }
    }

    /// <summary>
    /// Opens a file picker to select an image for the specified driver.
    /// </summary>
    private async void OnDriverImageChangeRequested(object? sender, DriverViewModel driver)
    {
        if (_windowService == null) return;

        var sourcePath = await _windowService.ShowImagePickerAsync("Select Driver Image");
        if (sourcePath != null)
        {
            driver.ImagePath = _windowService.CopyImageToAppFolder(sourcePath, driver.Id, "driver_");
        }
    }
}
