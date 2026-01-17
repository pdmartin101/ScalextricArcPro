using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
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
        // Get BLE service from DI container
        var bleService = App.Services?.GetService<IBleService>();

        // Create the tuning view model
        var tuningViewModel = new CarTuningViewModel(car, bleService);

        // Create and show the tuning window
        var window = new CarTuningWindow(tuningViewModel)
        {
            // Set owner for proper modal behavior
        };

        // Show as dialog and wait for result
        var result = await window.ShowDialog<bool?>(this);

        if (result == true)
        {
            // Tuning completed successfully - values were saved to car
        }
    }

    /// <summary>
    /// Opens a file picker to select an image for the specified car.
    /// Copies the image to the app's Images folder for persistence.
    /// </summary>
    private async void OnImageChangeRequested(object? sender, CarViewModel car)
    {
        var storageProvider = StorageProvider;

        var options = new FilePickerOpenOptions
        {
            Title = "Select Car Image",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp"]
                }
            ]
        };

        var result = await storageProvider.OpenFilePickerAsync(options);

        if (result.Count > 0)
        {
            var sourceFile = result[0];
            var sourcePath = sourceFile.Path.LocalPath;

            try
            {
                // Ensure Images folder exists
                var imagesFolder = AppSettings.ImagesFolder;
                if (!System.IO.Directory.Exists(imagesFolder))
                {
                    System.IO.Directory.CreateDirectory(imagesFolder);
                }

                // Generate unique filename using car ID and original extension
                var extension = System.IO.Path.GetExtension(sourcePath);
                var destFileName = $"{car.Id}{extension}";
                var destPath = System.IO.Path.Combine(imagesFolder, destFileName);

                // Copy the file (overwrite if exists)
                System.IO.File.Copy(sourcePath, destPath, overwrite: true);

                // Update car with local path
                car.ImagePath = destPath;
            }
            catch (System.Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to copy image for car {CarId}", car.Id);
                // Fall back to using original path
                car.ImagePath = sourcePath;
            }
        }
    }

    /// <summary>
    /// Opens a file picker to select an image for the specified driver.
    /// Copies the image to the app's Images folder for persistence.
    /// </summary>
    private async void OnDriverImageChangeRequested(object? sender, DriverViewModel driver)
    {
        var storageProvider = StorageProvider;

        var options = new FilePickerOpenOptions
        {
            Title = "Select Driver Image",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp"]
                }
            ]
        };

        var result = await storageProvider.OpenFilePickerAsync(options);

        if (result.Count > 0)
        {
            var sourceFile = result[0];
            var sourcePath = sourceFile.Path.LocalPath;

            try
            {
                // Ensure Images folder exists
                var imagesFolder = AppSettings.ImagesFolder;
                if (!System.IO.Directory.Exists(imagesFolder))
                {
                    System.IO.Directory.CreateDirectory(imagesFolder);
                }

                // Generate unique filename using driver ID and original extension
                var extension = System.IO.Path.GetExtension(sourcePath);
                var destFileName = $"driver_{driver.Id}{extension}";
                var destPath = System.IO.Path.Combine(imagesFolder, destFileName);

                // Copy the file (overwrite if exists)
                System.IO.File.Copy(sourcePath, destPath, overwrite: true);

                // Update driver with local path
                driver.ImagePath = destPath;
            }
            catch (System.Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to copy image for driver {DriverId}", driver.Id);
                // Fall back to using original path
                driver.ImagePath = sourcePath;
            }
        }
    }
}
