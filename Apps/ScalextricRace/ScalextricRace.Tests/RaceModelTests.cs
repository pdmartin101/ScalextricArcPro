using ScalextricRace.Models;

namespace ScalextricRace.Tests;

/// <summary>
/// Unit tests for Race model.
/// </summary>
public class RaceModelTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        // Act
        var race = new Race();

        // Assert
        Assert.Equal("New Race", race.Name);
        Assert.NotEqual(Guid.Empty, race.Id);
        Assert.Null(race.ImagePath);
        Assert.NotNull(race.FreePractice);
        Assert.NotNull(race.Qualifying);
        Assert.NotNull(race.RaceStage);
    }

    [Fact]
    public void CreateDefault_CreatesRaceWithDefaultId()
    {
        // Act
        var race = Race.CreateDefault();

        // Assert
        Assert.Equal(Race.DefaultRaceId, race.Id);
        Assert.Equal("Standard Race", race.Name);
    }

    [Fact]
    public void DefaultRaceId_IsConsistent()
    {
        // The default race ID should be the same every time
        var id1 = Race.DefaultRaceId;
        var id2 = Race.DefaultRaceId;

        Assert.Equal(id1, id2);
    }

    [Fact]
    public void FreePractice_HasDefaultValues()
    {
        // Arrange
        var race = new Race();

        // Assert
        Assert.True(race.FreePractice.Enabled);
        Assert.Equal(RaceStageMode.Laps, race.FreePractice.Mode);
        Assert.Equal(5, race.FreePractice.LapCount);
        Assert.Equal(5, race.FreePractice.TimeMinutes);
    }

    [Fact]
    public void Qualifying_HasDefaultValues()
    {
        // Arrange
        var race = new Race();

        // Assert
        Assert.True(race.Qualifying.Enabled);
        Assert.Equal(RaceStageMode.Laps, race.Qualifying.Mode);
        Assert.Equal(3, race.Qualifying.LapCount);
        Assert.Equal(3, race.Qualifying.TimeMinutes);
    }

    [Fact]
    public void RaceStage_HasDefaultValues()
    {
        // Arrange
        var race = new Race();

        // Assert
        Assert.True(race.RaceStage.Enabled);
        Assert.Equal(RaceStageMode.Laps, race.RaceStage.Mode);
        Assert.Equal(10, race.RaceStage.LapCount);
        Assert.Equal(10, race.RaceStage.TimeMinutes);
    }

    [Fact]
    public void Stage_CanBeDisabled()
    {
        // Arrange
        var race = new Race();

        // Act
        race.FreePractice.Enabled = false;

        // Assert
        Assert.False(race.FreePractice.Enabled);
    }

    [Fact]
    public void Stage_CanChangeModeToTime()
    {
        // Arrange
        var race = new Race();

        // Act
        race.RaceStage.Mode = RaceStageMode.Time;

        // Assert
        Assert.Equal(RaceStageMode.Time, race.RaceStage.Mode);
    }
}
