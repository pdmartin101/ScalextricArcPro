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
    /// <summary>
    /// Initializes the main window.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        // Subscribe to events after DataContext is set
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.TuneWindowRequested += OnTuneWindowRequested;
                viewModel.ImageChangeRequested += OnImageChangeRequested;
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
            var file = result[0];
            car.ImagePath = file.Path.LocalPath;
        }
    }
}
