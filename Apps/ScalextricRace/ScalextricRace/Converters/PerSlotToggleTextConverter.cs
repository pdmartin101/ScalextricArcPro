using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ScalextricRace.Converters;

/// <summary>
/// Converts UsePerSlotPower bool to toggle button text.
/// </summary>
public class PerSlotToggleTextConverter : IValueConverter
{
    /// <summary>
    /// Singleton instance for use in XAML.
    /// </summary>
    public static readonly PerSlotToggleTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool usePerSlot)
        {
            return usePerSlot ? "Per-Slot" : "Global";
        }
        return "Per-Slot";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
