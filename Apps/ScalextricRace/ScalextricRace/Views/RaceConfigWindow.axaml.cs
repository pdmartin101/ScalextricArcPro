using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ScalextricRace.Views;

/// <summary>
/// Race configuration editing window.
/// </summary>
public partial class RaceConfigWindow : Window
{
    /// <summary>
    /// Initializes the race config window.
    /// </summary>
    public RaceConfigWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Handles the Close button click.
    /// </summary>
    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
