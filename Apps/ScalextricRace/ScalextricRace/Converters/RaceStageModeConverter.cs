using System.Globalization;
using Avalonia.Data.Converters;
using ScalextricRace.Models;

namespace ScalextricRace.Converters;

/// <summary>
/// Converters for RaceStageMode enum to support RadioButton binding.
/// </summary>
public static class RaceStageModeConverter
{
    /// <summary>
    /// Converter that returns true when mode is Laps.
    /// </summary>
    public static readonly IValueConverter ToLaps = new RaceStageModeToLapsConverter();

    /// <summary>
    /// Converter that returns true when mode is Time.
    /// </summary>
    public static readonly IValueConverter ToTime = new RaceStageModeToTimeConverter();

    /// <summary>
    /// Converter that returns true when mode is Laps (for visibility).
    /// </summary>
    public static readonly IValueConverter IsLaps = new RaceStageModeIsLapsConverter();

    /// <summary>
    /// Converter that returns true when mode is Time (for visibility).
    /// </summary>
    public static readonly IValueConverter IsTime = new RaceStageModeIsTimeConverter();

    private class RaceStageModeToLapsConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is RaceStageMode mode && mode == RaceStageMode.Laps;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is true ? RaceStageMode.Laps : Avalonia.Data.BindingOperations.DoNothing;
        }
    }

    private class RaceStageModeToTimeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is RaceStageMode mode && mode == RaceStageMode.Time;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is true ? RaceStageMode.Time : Avalonia.Data.BindingOperations.DoNothing;
        }
    }

    private class RaceStageModeIsLapsConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is RaceStageMode mode && mode == RaceStageMode.Laps;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    private class RaceStageModeIsTimeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is RaceStageMode mode && mode == RaceStageMode.Time;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
