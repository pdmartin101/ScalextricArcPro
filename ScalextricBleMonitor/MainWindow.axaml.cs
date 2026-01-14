using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ScalextricBleMonitor.ViewModels;

namespace ScalextricBleMonitor;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private NotificationWindow? _notificationWindow;
    private GattServicesWindow? _gattServicesWindow;

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
        // Close child windows if open
        _notificationWindow?.Close();
        _notificationWindow = null;
        _gattServicesWindow?.Close();
        _gattServicesWindow = null;

        _viewModel.StopMonitoring();
        _viewModel.Dispose();
    }

    private void OnPowerToggleClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.TogglePower();
    }

    private void OnViewGattServicesClick(object? sender, RoutedEventArgs e)
    {
        // Only allow one GATT services window at a time
        if (_gattServicesWindow != null)
        {
            _gattServicesWindow.Activate();
            return;
        }

        _gattServicesWindow = new GattServicesWindow
        {
            DataContext = _viewModel
        };

        _viewModel.IsGattServicesWindowOpen = true;

        _gattServicesWindow.Closed += (_, _) =>
        {
            _gattServicesWindow = null;
        };

        _gattServicesWindow.Show(this);
    }

    private void OnViewNotificationsClick(object? sender, RoutedEventArgs e)
    {
        // Only allow one notification window at a time
        if (_notificationWindow != null)
        {
            _notificationWindow.Activate();
            return;
        }

        _notificationWindow = new NotificationWindow
        {
            DataContext = _viewModel
        };

        _viewModel.IsNotificationWindowOpen = true;

        _notificationWindow.Closed += (_, _) =>
        {
            _notificationWindow = null;
        };

        _notificationWindow.Show(this);
    }
}