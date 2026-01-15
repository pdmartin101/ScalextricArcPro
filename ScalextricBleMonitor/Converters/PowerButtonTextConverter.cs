using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ScalextricBleMonitor.Converters;

/// <summary>
/// Converts power enabled state to button text.
/// </summary>
public class PowerButtonTextConverter : IValueConverter
{
    public static readonly PowerButtonTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isEnabled)
        {
            return isEnabled ? "POWER OFF" : "POWER ON";
        }
        return "POWER ON";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
