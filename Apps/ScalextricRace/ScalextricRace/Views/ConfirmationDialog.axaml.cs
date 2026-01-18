using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ScalextricRace.Views;

/// <summary>
/// Simple confirmation dialog with Yes/No buttons.
/// </summary>
public partial class ConfirmationDialog : Window
{
    /// <summary>
    /// Gets or sets the message to display.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new confirmation dialog.
    /// </summary>
    public ConfirmationDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    /// <summary>
    /// Handles Yes button click.
    /// </summary>
    private void OnYesClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    /// <summary>
    /// Handles No button click.
    /// </summary>
    private void OnNoClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
