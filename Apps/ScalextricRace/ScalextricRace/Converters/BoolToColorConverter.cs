using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ScalextricRace.Converters;

/// <summary>
/// Converts bool to a color.
/// True = default text color (for power limit value), False = green (for "No Limit").
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    /// <summary>
    /// Singleton instance for use in XAML.
    /// </summary>
    public static readonly BoolToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool hasLimit)
        {
            // Has power limit = orange/warning color, No limit = green
            return hasLimit ? new SolidColorBrush(Color.Parse("#FF9800")) : new SolidColorBrush(Color.Parse("#4CAF50"));
        }

        return Brushes.Black;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
