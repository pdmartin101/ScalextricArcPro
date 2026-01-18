namespace Scalextric;

/// <summary>
/// Abstraction for UI thread dispatching to enable testability of ViewModels.
/// Provides a mockable interface for cross-thread marshalling from background tasks.
/// </summary>
public interface IDispatcherService
{
    /// <summary>
    /// Posts an action to the UI thread for execution.
    /// If already on the UI thread, executes immediately; otherwise dispatches asynchronously.
    /// </summary>
    /// <param name="action">The action to execute on the UI thread.</param>
    void Post(Action action);

    /// <summary>
    /// Invokes an async action on the UI thread and waits for completion.
    /// </summary>
    /// <param name="action">The async action to execute on the UI thread.</param>
    /// <returns>A task representing the async operation.</returns>
    Task InvokeAsync(Func<Task> action);

    /// <summary>
    /// Checks if the current thread is the UI thread.
    /// </summary>
    /// <returns>True if on the UI thread, false otherwise.</returns>
    bool CheckAccess();
}
