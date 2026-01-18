using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ScalextricRace.ViewModels;

/// <summary>
/// ViewModel for the confirmation dialog.
/// </summary>
public partial class ConfirmationDialogViewModel : ObservableObject
{
    /// <summary>
    /// Gets or sets the message to display.
    /// </summary>
    [ObservableProperty]
    private string _message = string.Empty;

    /// <summary>
    /// Gets or sets the callback for when a result is selected.
    /// </summary>
    public Action<bool>? ResultCallback { get; set; }

    /// <summary>
    /// Command for Yes button - returns true.
    /// </summary>
    [RelayCommand]
    private void Yes()
    {
        ResultCallback?.Invoke(true);
    }

    /// <summary>
    /// Command for No button - returns false.
    /// </summary>
    [RelayCommand]
    private void No()
    {
        ResultCallback?.Invoke(false);
    }
}
