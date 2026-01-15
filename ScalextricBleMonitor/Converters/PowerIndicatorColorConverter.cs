using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ScalextricBleMonitor.Converters;

/// <summary>
/// Converts power enabled state to indicator color.
/// </summary>
public class PowerIndicatorColorConverter : IValueConverter
{
    public static readonly PowerIndicatorColorConverter Instance = new();

    private static readonly Color PowerOnColor = Color.FromRgb(76, 175, 80);   // Green
    private static readonly Color PowerOffColor = Color.FromRgb(158, 158, 158); // Gray

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isPowerOn)
        {
            return isPowerOn ? PowerOnColor : PowerOffColor;
        }
        return PowerOffColor;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
