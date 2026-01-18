using Avalonia.Controls;
using ScalextricRace.ViewModels;

namespace ScalextricRace.Views;

/// <summary>
/// Car tuning wizard window.
/// Uses MVVM pattern - business logic is in CarTuningViewModel.
/// </summary>
public partial class CarTuningWindow : Window
{
    private bool _isClosingHandled;

    /// <summary>
    /// Initializes the car tuning window.
    /// </summary>
    public CarTuningWindow()
    {
        InitializeComponent();

        Closing += async (_, e) =>
        {
            // If already handled, allow close
            if (_isClosingHandled)
                return;

            // Cancel the close, await cleanup, then close properly
            e.Cancel = true;
            if (DataContext is CarTuningViewModel viewModel)
            {
                await viewModel.OnClosing();
            }
            _isClosingHandled = true;
            Close();
        };
    }

    /// <summary>
    /// Closes the window with cleanup.
    /// Called by the ViewModel via CompletionCallback.
    /// </summary>
    public async void CloseWithResult(bool result)
    {
        if (_isClosingHandled)
        {
            Close(result);
            return;
        }

        if (DataContext is CarTuningViewModel viewModel)
        {
            await viewModel.OnClosing();
        }
        _isClosingHandled = true;
        Close(result);
    }
}
