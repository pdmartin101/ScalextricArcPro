using System.Globalization;
using Avalonia.Media;
using ScalextricRace.Converters;
using ScalextricRace.Models;
using ScalextricRace.ViewModels;

namespace ScalextricRace.Tests;

/// <summary>
/// Unit tests for value converters.
/// </summary>
public class ConverterTests
{
    #region BoolToColorConverter Tests

    [Fact]
    public void BoolToColorConverter_True_ReturnsOrangeColor()
    {
        // Arrange
        var converter = BoolToColorConverter.Instance;

        // Act
        var result = converter.Convert(true, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<SolidColorBrush>(result);
        var brush = (SolidColorBrush)result!;
        Assert.Equal(Color.Parse("#FF9800"), brush.Color);
    }

    [Fact]
    public void BoolToColorConverter_False_ReturnsGreenColor()
    {
        // Arrange
        var converter = BoolToColorConverter.Instance;

        // Act
        var result = converter.Convert(false, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<SolidColorBrush>(result);
        var brush = (SolidColorBrush)result!;
        Assert.Equal(Color.Parse("#4CAF50"), brush.Color);
    }

    [Fact]
    public void BoolToColorConverter_NonBool_ReturnsBlack()
    {
        // Arrange
        var converter = BoolToColorConverter.Instance;

        // Act
        var result = converter.Convert("not a bool", typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Same(Brushes.Black, result);
    }

    [Fact]
    public void BoolToColorConverter_Null_ReturnsBlack()
    {
        // Arrange
        var converter = BoolToColorConverter.Instance;

        // Act
        var result = converter.Convert(null, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Same(Brushes.Black, result);
    }

    [Fact]
    public void BoolToColorConverter_ConvertBack_ThrowsNotSupported()
    {
        // Arrange
        var converter = BoolToColorConverter.Instance;

        // Act & Assert
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(Brushes.Green, typeof(bool), null, CultureInfo.InvariantCulture));
    }

    #endregion

    #region BoolToTestButtonColorConverter Tests

    [Fact]
    public void BoolToTestButtonColorConverter_True_ReturnsOrangeColor()
    {
        // Arrange
        var converter = BoolToTestButtonColorConverter.Instance;

        // Act
        var result = converter.Convert(true, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<SolidColorBrush>(result);
        var brush = (SolidColorBrush)result!;
        Assert.Equal(Color.Parse("#FF9800"), brush.Color);
    }

    [Fact]
    public void BoolToTestButtonColorConverter_False_ReturnsTransparent()
    {
        // Arrange
        var converter = BoolToTestButtonColorConverter.Instance;

        // Act
        var result = converter.Convert(false, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Same(Brushes.Transparent, result);
    }

    [Fact]
    public void BoolToTestButtonColorConverter_NonBool_ReturnsTransparent()
    {
        // Arrange
        var converter = BoolToTestButtonColorConverter.Instance;

        // Act
        var result = converter.Convert("not a bool", typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Same(Brushes.Transparent, result);
    }

    [Fact]
    public void BoolToTestButtonColorConverter_Null_ReturnsTransparent()
    {
        // Arrange
        var converter = BoolToTestButtonColorConverter.Instance;

        // Act
        var result = converter.Convert(null, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Same(Brushes.Transparent, result);
    }

    [Fact]
    public void BoolToTestButtonColorConverter_ConvertBack_ThrowsNotSupported()
    {
        // Arrange
        var converter = BoolToTestButtonColorConverter.Instance;

        // Act & Assert
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(Brushes.Green, typeof(bool), null, CultureInfo.InvariantCulture));
    }

    #endregion

    #region ConnectionStateToColorConverter Tests

    [Fact]
    public void ConnectionStateToColorConverter_Connected_ReturnsGreen()
    {
        // Arrange
        var converter = ConnectionStateToColorConverter.Instance;

        // Act
        var result = converter.Convert(ConnectionState.Connected, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Same(Brushes.Green, result);
    }

    [Fact]
    public void ConnectionStateToColorConverter_Connecting_ReturnsBlue()
    {
        // Arrange
        var converter = ConnectionStateToColorConverter.Instance;

        // Act
        var result = converter.Convert(ConnectionState.Connecting, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Same(Brushes.Blue, result);
    }

    [Fact]
    public void ConnectionStateToColorConverter_Disconnected_ReturnsRed()
    {
        // Arrange
        var converter = ConnectionStateToColorConverter.Instance;

        // Act
        var result = converter.Convert(ConnectionState.Disconnected, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Same(Brushes.Red, result);
    }

    [Fact]
    public void ConnectionStateToColorConverter_NonConnectionState_ReturnsRed()
    {
        // Arrange
        var converter = ConnectionStateToColorConverter.Instance;

        // Act
        var result = converter.Convert("not a state", typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Same(Brushes.Red, result);
    }

    [Fact]
    public void ConnectionStateToColorConverter_Null_ReturnsRed()
    {
        // Arrange
        var converter = ConnectionStateToColorConverter.Instance;

        // Act
        var result = converter.Convert(null, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Same(Brushes.Red, result);
    }

    [Fact]
    public void ConnectionStateToColorConverter_ConvertBack_ThrowsNotSupported()
    {
        // Arrange
        var converter = ConnectionStateToColorConverter.Instance;

        // Act & Assert
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(Brushes.Green, typeof(ConnectionState), null, CultureInfo.InvariantCulture));
    }

    #endregion

    #region RaceStageModeConverter Tests

    [Fact]
    public void RaceStageModeToLaps_LapsMode_ReturnsTrue()
    {
        // Arrange
        var converter = RaceStageModeConverter.ToLaps;

        // Act
        var result = converter.Convert(RaceStageMode.Laps, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.True((bool)result!);
    }

    [Fact]
    public void RaceStageModeToLaps_TimeMode_ReturnsFalse()
    {
        // Arrange
        var converter = RaceStageModeConverter.ToLaps;

        // Act
        var result = converter.Convert(RaceStageMode.Time, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.False((bool)result!);
    }

    [Fact]
    public void RaceStageModeToLaps_ConvertBackTrue_ReturnsLaps()
    {
        // Arrange
        var converter = RaceStageModeConverter.ToLaps;

        // Act
        var result = converter.ConvertBack(true, typeof(RaceStageMode), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(RaceStageMode.Laps, result);
    }

    [Fact]
    public void RaceStageModeToLaps_ConvertBackFalse_ReturnsDoNothing()
    {
        // Arrange
        var converter = RaceStageModeConverter.ToLaps;

        // Act
        var result = converter.ConvertBack(false, typeof(RaceStageMode), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(Avalonia.Data.BindingOperations.DoNothing, result);
    }

    [Fact]
    public void RaceStageModeToTime_TimeMode_ReturnsTrue()
    {
        // Arrange
        var converter = RaceStageModeConverter.ToTime;

        // Act
        var result = converter.Convert(RaceStageMode.Time, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.True((bool)result!);
    }

    [Fact]
    public void RaceStageModeToTime_LapsMode_ReturnsFalse()
    {
        // Arrange
        var converter = RaceStageModeConverter.ToTime;

        // Act
        var result = converter.Convert(RaceStageMode.Laps, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.False((bool)result!);
    }

    [Fact]
    public void RaceStageModeToTime_ConvertBackTrue_ReturnsTime()
    {
        // Arrange
        var converter = RaceStageModeConverter.ToTime;

        // Act
        var result = converter.ConvertBack(true, typeof(RaceStageMode), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(RaceStageMode.Time, result);
    }

    [Fact]
    public void RaceStageModeIsLaps_LapsMode_ReturnsTrue()
    {
        // Arrange
        var converter = RaceStageModeConverter.IsLaps;

        // Act
        var result = converter.Convert(RaceStageMode.Laps, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.True((bool)result!);
    }

    [Fact]
    public void RaceStageModeIsLaps_TimeMode_ReturnsFalse()
    {
        // Arrange
        var converter = RaceStageModeConverter.IsLaps;

        // Act
        var result = converter.Convert(RaceStageMode.Time, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.False((bool)result!);
    }

    [Fact]
    public void RaceStageModeIsLaps_ConvertBack_ThrowsNotSupported()
    {
        // Arrange
        var converter = RaceStageModeConverter.IsLaps;

        // Act & Assert
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(true, typeof(RaceStageMode), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void RaceStageModeIsTime_TimeMode_ReturnsTrue()
    {
        // Arrange
        var converter = RaceStageModeConverter.IsTime;

        // Act
        var result = converter.Convert(RaceStageMode.Time, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.True((bool)result!);
    }

    [Fact]
    public void RaceStageModeIsTime_LapsMode_ReturnsFalse()
    {
        // Arrange
        var converter = RaceStageModeConverter.IsTime;

        // Act
        var result = converter.Convert(RaceStageMode.Laps, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.False((bool)result!);
    }

    [Fact]
    public void RaceStageModeIsTime_ConvertBack_ThrowsNotSupported()
    {
        // Arrange
        var converter = RaceStageModeConverter.IsTime;

        // Act & Assert
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(true, typeof(RaceStageMode), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void RaceStageModeToLaps_NonEnum_ReturnsFalse()
    {
        // Arrange
        var converter = RaceStageModeConverter.ToLaps;

        // Act
        var result = converter.Convert("not an enum", typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.False((bool)result!);
    }

    [Fact]
    public void RaceStageModeToTime_Null_ReturnsFalse()
    {
        // Arrange
        var converter = RaceStageModeConverter.ToTime;

        // Act
        var result = converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.False((bool)result!);
    }

    #endregion
}
