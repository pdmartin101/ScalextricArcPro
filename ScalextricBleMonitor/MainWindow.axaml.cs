using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ScalextricBleMonitor.ViewModels;

namespace ScalextricBleMonitor;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        // Create and set the view model
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // Start monitoring when window is opened
        Opened += OnWindowOpened;

        // Clean up when window is closing
        Closing += OnWindowClosing;
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        _viewModel.StartMonitoring();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        _viewModel.StopMonitoring();
        _viewModel.Dispose();
    }

    private void OnReadCharacteristicClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is CharacteristicViewModel characteristic)
        {
            _viewModel.ReadCharacteristic(characteristic.ServiceUuid, characteristic.Uuid);
        }
    }

    private void OnPowerToggleClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.TogglePower();
    }
}