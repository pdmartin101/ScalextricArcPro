using System;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
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

        // Get the view model from DI container (falls back to direct instantiation if not available)
        _viewModel = App.Services?.GetService<MainViewModel>() ?? new MainViewModel();
        DataContext = _viewModel;

        // Create and set the window service (not in DI as it needs Window reference)
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
