using System;

namespace Scalextric;

/// <summary>
/// Base class for objects that implement IDisposable with a standard disposal pattern.
/// Provides the dispose pattern infrastructure for derived classes.
/// Note: This is a simple base class; if you need property change notifications,
/// inherit from CommunityToolkit.Mvvm.ComponentModel.ObservableObject and implement IDisposable separately.
/// </summary>
public abstract class DisposableBase : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets a value indicating whether this instance has been disposed.
    /// </summary>
    protected bool IsDisposed => _disposed;

    /// <summary>
    /// Releases all resources used by the object.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        Dispose(true);
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the object and optionally releases the managed resources.
    /// Override this method to implement custom disposal logic (unsubscribe from events, dispose child objects, etc.).
    /// </summary>
    /// <param name="disposing">
    /// true to release both managed and unmanaged resources;
    /// false to release only unmanaged resources (called from finalizer).
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        // Override in derived classes to implement disposal logic
        // Example:
        // if (disposing)
        // {
        //     // Unsubscribe from events
        //     // Dispose managed resources
        // }
    }
}
