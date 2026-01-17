using Avalonia.Platform.Storage;
using ScalextricRace.ViewModels;

namespace ScalextricRace.Services;

/// <summary>
/// Service for managing application windows and dialogs.
/// Abstracts window creation and lifecycle from ViewModels for better MVVM compliance.
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// Shows the car tuning window as a dialog.
    /// </summary>
    /// <param name="carViewModel">The car to tune.</param>
    /// <param name="bleService">The BLE service for track control.</param>
    /// <returns>True if tuning completed successfully, false if cancelled.</returns>
    Task<bool> ShowCarTuningDialogAsync(CarViewModel carViewModel, IBleService? bleService);

    /// <summary>
    /// Shows an image file picker dialog.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <returns>The selected file path, or null if cancelled.</returns>
    Task<string?> ShowImagePickerAsync(string title);

    /// <summary>
    /// Copies an image to the app's Images folder.
    /// </summary>
    /// <param name="sourcePath">The source image path.</param>
    /// <param name="entityId">The entity ID for the filename.</param>
    /// <param name="prefix">Optional prefix for the filename (e.g., "driver_").</param>
    /// <returns>The destination path, or the source path if copy failed.</returns>
    string CopyImageToAppFolder(string sourcePath, Guid entityId, string prefix = "");
}
