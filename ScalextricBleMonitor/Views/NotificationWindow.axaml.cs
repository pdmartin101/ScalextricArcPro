using Avalonia.Controls;
using Avalonia.Interactivity;
using ScalextricBleMonitor.ViewModels;

namespace ScalextricBleMonitor.Views;

public partial class NotificationWindow : Window
{
    public NotificationWindow()
    {
        InitializeComponent();
    }

    private void OnFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && CharacteristicFilter.SelectedIndex >= 0)
        {
            viewModel.NotificationCharacteristicFilter = CharacteristicFilter.SelectedIndex;
        }
    }

    private void OnPauseChanged(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.IsNotificationLogPaused = PauseCheckbox.IsChecked ?? false;
        }
    }
}
