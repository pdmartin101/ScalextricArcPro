using System;
using Avalonia.Controls;
using ScalextricBleMonitor.Services;
using ScalextricBleMonitor.ViewModels;

namespace ScalextricBleMonitor.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IWindowService _windowService;

    public MainWindow()
    {
        InitializeComponent();

        // Create and set the view model
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // Create and set the window service
        _windowService = new WindowService(this, () => _viewModel);
        _viewModel.SetWindowService(_windowService);

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
        // Close child windows via service
        _windowService.CloseAllWindows();

        _viewModel.StopMonitoring();
        _viewModel.Dispose();
    }
}
