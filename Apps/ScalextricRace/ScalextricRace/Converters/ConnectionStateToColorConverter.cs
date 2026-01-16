using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ScalextricRace.Models;

namespace ScalextricRace.Converters;

/// <summary>
/// Converts ConnectionState to a brush color for the status indicator.
/// Red = Disconnected, Blue = Connecting, Green = Connected.
/// </summary>
public class ConnectionStateToColorConverter : IValueConverter
{
    /// <summary>
    /// Singleton instance for use in XAML.
    /// </summary>
    public static readonly ConnectionStateToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ConnectionState state)
        {
            return state switch
            {
                ConnectionState.Connected => Brushes.Green,
                ConnectionState.Connecting => Brushes.Blue,
                _ => Brushes.Red
            };
        }

        return Brushes.Red;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
