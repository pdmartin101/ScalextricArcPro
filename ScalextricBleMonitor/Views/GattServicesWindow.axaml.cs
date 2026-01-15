using Avalonia.Controls;
using Avalonia.Interactivity;
using ScalextricBleMonitor.ViewModels;

namespace ScalextricBleMonitor.Views;

public partial class GattServicesWindow : Window
{
    public GattServicesWindow()
    {
        InitializeComponent();
    }

    private void OnReadCharacteristicClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button &&
            button.DataContext is CharacteristicViewModel characteristic &&
            DataContext is MainViewModel viewModel)
        {
            viewModel.ReadCharacteristic(characteristic.ServiceUuid, characteristic.Uuid);
        }
    }
}
