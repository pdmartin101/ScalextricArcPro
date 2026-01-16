using Avalonia.Controls;
using Avalonia.Input;
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
}
