using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ScalextricBleMonitor.Converters;

/// <summary>
/// Converts ghost mode state to slider tooltip text.
/// </summary>
public class GhostModeTooltipConverter : IValueConverter
{
    public static readonly GhostModeTooltipConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isGhostMode && isGhostMode)
        {
            return "Ghost throttle index (0-63): Direct motor control without controller";
        }
        return "Power level for this controller (0-63)";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
