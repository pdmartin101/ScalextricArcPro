using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ScalextricBleMonitor.ViewModels;

namespace ScalextricBleMonitor;

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

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Notify the main view model that this window was closed
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.OnGattServicesWindowClosed();
        }
    }
}
