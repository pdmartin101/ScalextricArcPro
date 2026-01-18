using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using Scalextric;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Avalonia implementation of IDispatcherService using Dispatcher.UIThread.
/// Provides UI thread marshalling for cross-thread operations.
/// </summary>
public class AvaloniaDispatcherService : IDispatcherService
{
    /// <summary>
    /// Posts an action to the UI thread.
    /// If already on UI thread, executes immediately; otherwise dispatches asynchronously.
    /// </summary>
    public void Post(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }

    /// <summary>
    /// Invokes an async action on the UI thread and waits for completion.
    /// </summary>
    public Task InvokeAsync(Func<Task> action)
    {
        return Dispatcher.UIThread.InvokeAsync(action);
    }

    /// <summary>
    /// Checks if currently on the UI thread.
    /// </summary>
    public bool CheckAccess()
    {
        return Dispatcher.UIThread.CheckAccess();
    }
}
