using Avalonia.Controls;
using ScalextricRace.ViewModels;

namespace ScalextricRace.Services;

/// <summary>
/// Service for managing application windows and dialogs.
/// Abstracts window creation and lifecycle from ViewModels for better MVVM compliance.
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// Sets the owner window for dialogs.
    /// Called by the View during initialization.
    /// </summary>
    /// <param name="owner">The owner window.</param>
    void SetOwner(Window owner);

    /// <summary>
    /// Shows the car tuning window as a dialog.
    /// </summary>
    /// <param name="carViewModel">The car to tune.</param>
    /// <param name="bleService">The BLE service for track control.</param>
    /// <returns>True if tuning completed successfully, false if cancelled.</returns>
    Task<bool> ShowCarTuningDialogAsync(CarViewModel carViewModel, IBleService? bleService);

    /// <summary>
    /// Shows an image file picker and copies the selected image to the app folder.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="entityId">The entity ID for the filename.</param>
    /// <param name="prefix">Optional prefix for the filename (e.g., "driver_").</param>
    /// <returns>The destination image path, or null if cancelled or failed.</returns>
    Task<string?> PickAndCopyImageAsync(string title, Guid entityId, string prefix = "");

    /// <summary>
    /// Shows the race configuration editing window as a dialog.
    /// </summary>
    /// <param name="raceViewModel">The race to edit.</param>
    Task ShowRaceConfigDialogAsync(RaceViewModel raceViewModel);
}
