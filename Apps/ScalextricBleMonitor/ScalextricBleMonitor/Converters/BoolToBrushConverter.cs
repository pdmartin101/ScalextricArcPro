using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ScalextricBleMonitor.Converters;

/// <summary>
/// Converts bool to brush for button indicators.
/// </summary>
public class BoolToBrushConverter : IValueConverter
{
    private static readonly ISolidColorBrush BrakeActiveColor = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
    private static readonly ISolidColorBrush BrakeInactiveColor = new SolidColorBrush(Color.FromRgb(183, 28, 28)); // Dark red
    private static readonly ISolidColorBrush LaneChangeActiveColor = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Blue
    private static readonly ISolidColorBrush LaneChangeInactiveColor = new SolidColorBrush(Color.FromRgb(21, 101, 192)); // Dark blue

    public static readonly BoolToBrushConverter BrakeInstance = new(true);
    public static readonly BoolToBrushConverter LaneChangeInstance = new(false);

    private readonly bool _isBrake;

    public BoolToBrushConverter(bool isBrake = true)
    {
        _isBrake = isBrake;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isPressed)
        {
            if (_isBrake)
            {
                return isPressed ? BrakeActiveColor : BrakeInactiveColor;
            }
            else
            {
                return isPressed ? LaneChangeActiveColor : LaneChangeInactiveColor;
            }
        }
        return _isBrake ? BrakeInactiveColor : LaneChangeInactiveColor;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
