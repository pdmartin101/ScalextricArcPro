using Avalonia.Controls;
using ScalextricRace.ViewModels;

namespace ScalextricRace.Views;

/// <summary>
/// Car tuning wizard window.
/// </summary>
public partial class CarTuningWindow : Window
{
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

        viewModel.TuningComplete += (_, _) => Close(true);
        viewModel.TuningCancelled += (_, _) => Close(false);

        Closing += async (_, e) =>
        {
            // Ensure power is off when window closes
            if (viewModel != null)
            {
                await viewModel.OnClosing();
            }
        };
    }
}
