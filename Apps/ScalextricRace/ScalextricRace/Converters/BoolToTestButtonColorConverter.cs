using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ScalextricRace.Converters;

/// <summary>
/// Converts test mode active state to button background color.
/// True (active) = orange/warning, False (inactive) = transparent (default button).
/// </summary>
public class BoolToTestButtonColorConverter : IValueConverter
{
    /// <summary>
    /// Singleton instance for use in XAML.
    /// </summary>
    public static readonly BoolToTestButtonColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive && isActive)
        {
            // Active = orange to indicate testing in progress
            return new SolidColorBrush(Color.Parse("#FF9800"));
        }

        // Inactive = transparent (uses default button style)
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
