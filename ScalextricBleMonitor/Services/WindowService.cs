using System;
using Avalonia.Controls;
using ScalextricBleMonitor.Views;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Manages application child windows (GATT Services, Notifications, Ghost Control).
/// Ensures single-instance windows and handles lifecycle events.
/// </summary>
public class WindowService : IWindowService
{
    private readonly Window _owner;
    private readonly Func<object> _getDataContext;
    private NotificationWindow? _notificationWindow;
    private GattServicesWindow? _gattServicesWindow;
    private GhostControlWindow? _ghostControlWindow;

    public event EventHandler? GattServicesWindowClosed;
    public event EventHandler? NotificationWindowClosed;
    public event EventHandler? GhostControlWindowClosed;

    /// <summary>
    /// Creates a new WindowService.
    /// </summary>
    /// <param name="owner">The owner window for child windows.</param>
    /// <param name="getDataContext">Function to get the DataContext for child windows.</param>
    public WindowService(Window owner, Func<object> getDataContext)
    {
        _owner = owner;
        _getDataContext = getDataContext;
    }

    public void ShowGattServicesWindow()
    {
        if (_gattServicesWindow != null)
        {
            _gattServicesWindow.Activate();
            return;
        }

        _gattServicesWindow = new GattServicesWindow
        {
            DataContext = _getDataContext()
        };

        _gattServicesWindow.Closed += OnGattServicesWindowClosed;
        _gattServicesWindow.Show(_owner);
    }

    public void ShowNotificationWindow()
    {
        if (_notificationWindow != null)
        {
            _notificationWindow.Activate();
            return;
        }

        _notificationWindow = new NotificationWindow
        {
            DataContext = _getDataContext()
        };

        _notificationWindow.Closed += OnNotificationWindowClosed;
        _notificationWindow.Show(_owner);
    }

    public void ShowGhostControlWindow()
    {
        if (_ghostControlWindow != null)
        {
            _ghostControlWindow.Activate();
            return;
        }

        _ghostControlWindow = new GhostControlWindow
        {
            DataContext = _getDataContext()
        };

        _ghostControlWindow.Closed += OnGhostControlWindowClosed;
        _ghostControlWindow.Show(_owner);
    }

    public void CloseAllWindows()
    {
        _notificationWindow?.Close();
        _notificationWindow = null;
        _gattServicesWindow?.Close();
        _gattServicesWindow = null;
        _ghostControlWindow?.Close();
        _ghostControlWindow = null;
    }

    private void OnGattServicesWindowClosed(object? sender, EventArgs e)
    {
        _gattServicesWindow = null;
        GattServicesWindowClosed?.Invoke(this, EventArgs.Empty);
    }

    private void OnNotificationWindowClosed(object? sender, EventArgs e)
    {
        _notificationWindow = null;
        NotificationWindowClosed?.Invoke(this, EventArgs.Empty);
    }

    private void OnGhostControlWindowClosed(object? sender, EventArgs e)
    {
        _ghostControlWindow = null;
        GhostControlWindowClosed?.Invoke(this, EventArgs.Empty);
    }
}
