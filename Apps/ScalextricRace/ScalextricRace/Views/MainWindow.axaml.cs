using Avalonia.Controls;
using ScalextricRace.ViewModels;

namespace ScalextricRace.Views;

/// <summary>
/// Main window for the ScalextricRace application.
/// Uses MVVM pattern - all logic is in MainViewModel.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Initializes the main window.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        // Start monitoring when window opens
        Opened += OnWindowOpened;

        // Stop monitoring when window closes
        Closing += OnWindowClosing;
    }

    /// <summary>
    /// Handles window opened event - starts BLE monitoring.
    /// </summary>
    private void OnWindowOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.StartMonitoring();
        }
    }

    /// <summary>
    /// Handles window closing event - stops BLE monitoring.
    /// </summary>
    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.StopMonitoring();
        }
    }
}
