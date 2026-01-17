using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ScalextricRace.ViewModels;
using ScalextricRace.Views;
using Serilog;

namespace ScalextricRace.Services;

/// <summary>
/// Manages application windows and dialogs.
/// Provides window abstraction for ViewModels following MVVM pattern.
/// </summary>
public class WindowService : IWindowService
{
    private Window? _owner;

    /// <inheritdoc />
    public void SetOwner(Window owner)
    {
        _owner = owner;
    }

    /// <inheritdoc />
    public async Task<bool> ShowCarTuningDialogAsync(CarViewModel carViewModel, IBleService? bleService)
    {
        if (_owner == null)
        {
            Log.Warning("WindowService.ShowCarTuningDialogAsync called before owner was set");
            return false;
        }

        var tuningViewModel = new CarTuningViewModel(carViewModel, bleService);
        var window = new CarTuningWindow(tuningViewModel);

        var result = await window.ShowDialog<bool?>(_owner);
        return result == true;
    }

    /// <inheritdoc />
    public async Task ShowRaceConfigDialogAsync(RaceViewModel raceViewModel)
    {
        if (_owner == null)
        {
            Log.Warning("WindowService.ShowRaceConfigDialogAsync called before owner was set");
            return;
        }

        var window = new RaceConfigWindow
        {
            DataContext = raceViewModel
        };

        await window.ShowDialog(_owner);
    }

    /// <inheritdoc />
    public async Task<string?> PickAndCopyImageAsync(string title, Guid entityId, string prefix = "")
    {
        if (_owner == null)
        {
            Log.Warning("WindowService.PickAndCopyImageAsync called before owner was set");
            return null;
        }

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp"]
                }
            ]
        };

        var result = await _owner.StorageProvider.OpenFilePickerAsync(options);

        if (result.Count == 0)
        {
            return null;
        }

        var sourcePath = result[0].Path.LocalPath;
        return CopyImageToAppFolder(sourcePath, entityId, prefix);
    }

    /// <summary>
    /// Copies an image to the app's Images folder.
    /// </summary>
    private static string? CopyImageToAppFolder(string sourcePath, Guid entityId, string prefix)
    {
        try
        {
            // Ensure Images folder exists
            var imagesFolder = AppSettings.ImagesFolder;
            if (!Directory.Exists(imagesFolder))
            {
                Directory.CreateDirectory(imagesFolder);
            }

            // Generate unique filename using entity ID and original extension
            var extension = Path.GetExtension(sourcePath);
            var destFileName = $"{prefix}{entityId}{extension}";
            var destPath = Path.Combine(imagesFolder, destFileName);

            // Copy the file (overwrite if exists)
            File.Copy(sourcePath, destPath, overwrite: true);

            return destPath;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to copy image for entity {EntityId}", entityId);
            return null;
        }
    }
}
