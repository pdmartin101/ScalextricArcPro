using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Scalextric;

namespace ScalextricBleMonitor.Converters;

/// <summary>
/// Converts throttle value (0-63) to a scale factor (0.0-1.0) for ScaleTransform.
/// </summary>
public class ThrottleToScaleConverter : IValueConverter
{
    public static readonly ThrottleToScaleConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int throttle)
        {
            // Scale 0-MaxPowerLevel to 0.0-1.0
            return throttle / (double)ScalextricProtocol.MaxPowerLevel;
        }
        return 0.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
