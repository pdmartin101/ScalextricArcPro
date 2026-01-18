using Avalonia.Controls;

namespace ScalextricRace.Views;

/// <summary>
/// Simple confirmation dialog with Yes/No buttons.
/// Uses MVVM pattern - business logic is in ConfirmationDialogViewModel.
/// </summary>
public partial class ConfirmationDialog : Window
{
    /// <summary>
    /// Initializes a new confirmation dialog.
    /// </summary>
    public ConfirmationDialog()
    {
        InitializeComponent();
    }
}
