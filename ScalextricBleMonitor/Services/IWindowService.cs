using System;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Service for managing application windows.
/// Abstracts window creation and lifecycle from ViewModels for better MVVM compliance.
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// Shows the GATT Services window. If already open, activates it.
    /// </summary>
    void ShowGattServicesWindow();

    /// <summary>
    /// Shows the Notification window. If already open, activates it.
    /// </summary>
    void ShowNotificationWindow();

    /// <summary>
    /// Shows the Ghost Control window. If already open, activates it.
    /// </summary>
    void ShowGhostControlWindow();

    /// <summary>
    /// Closes all child windows.
    /// </summary>
    void CloseAllWindows();

    /// <summary>
    /// Raised when the GATT Services window is closed.
    /// </summary>
    event EventHandler? GattServicesWindowClosed;

    /// <summary>
    /// Raised when the Notification window is closed.
    /// </summary>
    event EventHandler? NotificationWindowClosed;

    /// <summary>
    /// Raised when the Ghost Control window is closed.
    /// </summary>
    event EventHandler? GhostControlWindowClosed;
}
