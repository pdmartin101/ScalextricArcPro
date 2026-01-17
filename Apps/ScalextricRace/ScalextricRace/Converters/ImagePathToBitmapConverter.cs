using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Serilog;

namespace ScalextricRace.Converters;

/// <summary>
/// Converts an image file path to a Bitmap for display.
/// Caches loaded bitmaps to improve performance.
/// This moves the UI-specific Bitmap type out of ViewModels for MVVM purity.
/// </summary>
public class ImagePathToBitmapConverter : IValueConverter
{
    /// <summary>
    /// Singleton instance for use in XAML.
    /// </summary>
    public static readonly ImagePathToBitmapConverter Instance = new();

    // Simple cache: path -> bitmap
    private readonly Dictionary<string, Bitmap> _cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (!File.Exists(path))
        {
            return null;
        }

        // Check cache
        if (_cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        try
        {
            var bitmap = new Bitmap(path);
            _cache[path] = bitmap;
            return bitmap;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load image from {ImagePath}", path);
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Clears the bitmap cache. Call when memory needs to be freed.
    /// </summary>
    public void ClearCache()
    {
        foreach (var bitmap in _cache.Values)
        {
            bitmap.Dispose();
        }
        _cache.Clear();
    }

    /// <summary>
    /// Removes a specific path from the cache (e.g., when image is updated).
    /// </summary>
    public void InvalidatePath(string path)
    {
        if (_cache.TryGetValue(path, out var bitmap))
        {
            bitmap.Dispose();
            _cache.Remove(path);
        }
    }
}
