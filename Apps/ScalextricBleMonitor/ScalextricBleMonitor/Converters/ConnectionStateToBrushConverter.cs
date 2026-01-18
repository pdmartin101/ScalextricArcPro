using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ScalextricBleMonitor.Models;

namespace ScalextricBleMonitor.Converters;

/// <summary>
/// Converts ConnectionState enum to SolidColorBrush for status indicator display.
/// </summary>
public class ConnectionStateToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ConnectionState state)
            return new SolidColorBrush(Colors.Gray);

        return state switch
        {
            ConnectionState.GattConnected => new SolidColorBrush(Color.FromRgb(0, 150, 255)),   // Blue
            ConnectionState.Advertising => new SolidColorBrush(Color.FromRgb(0, 200, 83)),      // Green
            ConnectionState.Disconnected => new SolidColorBrush(Color.FromRgb(220, 53, 69)),    // Red
            _ => new SolidColorBrush(Colors.Gray)
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts ConnectionState to text brush (green when connected/advertising, gray when disconnected).
/// </summary>
public class ConnectionStateToTextBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ConnectionState state)
            return new SolidColorBrush(Colors.Gray);

        return state == ConnectionState.Disconnected
            ? new SolidColorBrush(Color.FromRgb(128, 128, 128))  // Gray
            : new SolidColorBrush(Color.FromRgb(0, 200, 83));    // Green
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
