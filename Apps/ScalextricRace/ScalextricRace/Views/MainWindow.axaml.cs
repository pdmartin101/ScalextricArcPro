using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using ScalextricRace.Services;
using ScalextricRace.ViewModels;

namespace ScalextricRace.Views;

/// <summary>
/// Main window for the ScalextricRace application.
/// Uses MVVM pattern - all business logic is in MainViewModel.
/// Window service configuration is handled by App.axaml.cs.
/// Keyboard handling (Escape key) is done via XAML KeyBinding.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Initializes the main window.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        Closing += OnWindowClosing;
    }

    /// <summary>
    /// Handles window closing - saves all data and window size.
    /// </summary>
    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SaveAllOnShutdown();
        }

        // Save window size
        var settings = App.Services?.GetService<AppSettings>();
        if (settings != null)
        {
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
            settings.Save();
        }
    }
}
