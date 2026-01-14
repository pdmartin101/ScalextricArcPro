using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ScalextricBleMonitor.ViewModels;

namespace ScalextricBleMonitor;

public partial class NotificationWindow : Window
{
    public NotificationWindow()
    {
        InitializeComponent();
    }

    private void OnClearLogClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ClearNotificationLog();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Notify the main view model that this window was closed
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.OnNotificationWindowClosed();
        }
    }
}
