using Avalonia.Controls;
using ScalextricRace.ViewModels;

namespace ScalextricRace.Views;

/// <summary>
/// Car tuning wizard window.
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
    }

    /// <summary>
    /// Initializes with a view model and wires up completion events.
    /// </summary>
    /// <param name="viewModel">The tuning view model.</param>
    public CarTuningWindow(CarTuningViewModel viewModel) : this()
    {
        DataContext = viewModel;

        viewModel.TuningComplete += (_, _) => CloseWithCleanup(true);
        viewModel.TuningCancelled += (_, _) => CloseWithCleanup(false);

        Closing += async (_, e) =>
        {
            // If already handled, allow close
            if (_isClosingHandled)
                return;

            // Cancel the close, await cleanup, then close properly
            e.Cancel = true;
            await viewModel.OnClosing();
            _isClosingHandled = true;
            Close();
        };
    }

    /// <summary>
    /// Performs cleanup before closing the window.
    /// </summary>
    private async void CloseWithCleanup(bool? result)
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
