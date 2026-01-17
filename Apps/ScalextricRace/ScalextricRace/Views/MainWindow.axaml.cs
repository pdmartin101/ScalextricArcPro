using Avalonia.Controls;
using Avalonia.Input;
using ScalextricRace.Models;
using ScalextricRace.ViewModels;

namespace ScalextricRace.Views;

/// <summary>
/// Main window for the ScalextricRace application.
/// Uses MVVM pattern - all business logic is in MainViewModel.
/// Window service configuration is handled by App.axaml.cs.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Initializes the main window.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    /// <summary>
    /// Handles key down events.
    /// Escape key exits racing mode.
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is MainViewModel viewModel)
        {
            if (viewModel.CurrentAppMode == AppMode.Racing)
            {
                viewModel.ExitRacingCommand.Execute(null);
                e.Handled = true;
            }
            else if (viewModel.IsMenuOpen)
            {
                viewModel.IsMenuOpen = false;
                e.Handled = true;
            }
        }
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
