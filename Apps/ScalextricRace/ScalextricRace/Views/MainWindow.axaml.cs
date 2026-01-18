using Avalonia.Controls;
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
    private readonly AppSettings? _settings;

    /// <summary>
    /// Initializes the main window.
    /// Parameterless constructor for XAML designer support.
    /// </summary>
    public MainWindow() : this(null!)
    {
    }

    /// <summary>
    /// Initializes the main window with dependency injection.
    /// </summary>
    /// <param name="settings">Application settings for window size persistence.</param>
    public MainWindow(AppSettings settings)
    {
        _settings = settings;
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
        if (_settings != null)
        {
            _settings.WindowWidth = Width;
            _settings.WindowHeight = Height;
            _settings.Save();
        }
    }
}
