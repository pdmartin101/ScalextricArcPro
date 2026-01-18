using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
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
    /// Handles window closing - saves all data.
    /// </summary>
    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SaveAllOnShutdown();
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

    /// <summary>
    /// Handles car selection changed - closes the flyout.
    /// </summary>
    private void OnCarSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox && e.AddedItems.Count > 0)
        {
            // Use dispatcher to close after binding updates
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // Find all car selection buttons and hide their flyouts
                HideAllFlyoutsInRaceEntries();
            });
        }
    }

    /// <summary>
    /// Handles driver selection changed - closes the flyout.
    /// </summary>
    private void OnDriverSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox && e.AddedItems.Count > 0)
        {
            // Use dispatcher to close after binding updates
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // Find all driver selection buttons and hide their flyouts
                HideAllFlyoutsInRaceEntries();
            });
        }
    }

    /// <summary>
    /// Hides all open flyouts in race entry buttons.
    /// </summary>
    private void HideAllFlyoutsInRaceEntries()
    {
        // Find all buttons with flyouts and hide them
        foreach (var button in this.GetVisualDescendants().OfType<Button>())
        {
            if (button.Flyout is Flyout flyout)
            {
                flyout.Hide();
            }
        }
    }
}
