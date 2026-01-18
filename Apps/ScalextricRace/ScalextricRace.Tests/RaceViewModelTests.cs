using ScalextricRace.Models;
using ScalextricRace.ViewModels;

namespace ScalextricRace.Tests;

/// <summary>
/// Unit tests for RaceViewModel.
/// </summary>
public class RaceViewModelTests
{
    [Fact]
    public void Constructor_InitializesFromModel()
    {
        // Arrange
        var race = new Race { Name = "Test Race" };
        race.FreePractice.LapCount = 10;

        // Act
        var viewModel = new RaceViewModel(race);

        // Assert
        Assert.Equal("Test Race", viewModel.Name);
        Assert.Equal(race.Id, viewModel.Id);
        Assert.True(viewModel.FreePracticeEnabled);
        Assert.Equal(10, viewModel.FreePracticeLapCount);
    }

    [Fact]
    public void Name_ChangesAreSyncedToModel()
    {
        // Arrange
        var race = new Race { Name = "Original Name" };
        var viewModel = new RaceViewModel(race);

        // Act
        viewModel.Name = "New Name";

        // Assert
        Assert.Equal("New Name", race.Name);
    }

    [Fact]
    public void FreePracticeEnabled_SyncsToModel()
    {
        // Arrange
        var race = new Race();
        var viewModel = new RaceViewModel(race);

        // Act
        viewModel.FreePracticeEnabled = false;

        // Assert
        Assert.False(race.FreePractice.Enabled);
    }

    [Fact]
    public void FreePracticeLapCount_SyncsToModel()
    {
        // Arrange
        var race = new Race();
        var viewModel = new RaceViewModel(race);

        // Act
        viewModel.FreePracticeLapCount = 15;

        // Assert
        Assert.Equal(15, race.FreePractice.LapCount);
    }

    [Fact]
    public void FreePracticeMode_SyncsToModel()
    {
        // Arrange
        var race = new Race();
        var viewModel = new RaceViewModel(race);

        // Act
        viewModel.FreePracticeMode = RaceStageMode.Time;

        // Assert
        Assert.Equal(RaceStageMode.Time, race.FreePractice.Mode);
    }

    [Fact]
    public void FreePracticeDisplay_ShowsLaps()
    {
        // Arrange
        var race = new Race();
        race.FreePractice.LapCount = 5;
        var viewModel = new RaceViewModel(race);

        // Assert
        Assert.Equal("5 laps", viewModel.FreePracticeDisplay);
    }

    [Fact]
    public void FreePracticeDisplay_ShowsMinutes()
    {
        // Arrange
        var race = new Race();
        race.FreePractice.Mode = RaceStageMode.Time;
        race.FreePractice.TimeMinutes = 10;
        var viewModel = new RaceViewModel(race);

        // Assert
        Assert.Equal("10 min", viewModel.FreePracticeDisplay);
    }

    [Fact]
    public void FreePracticeDisplay_ShowsSingularLap()
    {
        // Arrange
        var race = new Race();
        race.FreePractice.LapCount = 1;
        var viewModel = new RaceViewModel(race);

        // Assert
        Assert.Equal("1 lap", viewModel.FreePracticeDisplay);
    }

    [Fact]
    public void IsDefault_PreventsDelete()
    {
        // Arrange
        var race = Race.CreateDefault();
        var viewModel = new RaceViewModel(race, isDefault: true);

        // Assert
        Assert.True(viewModel.IsDefault);
        Assert.False(viewModel.CanDelete);
    }

    [Fact]
    public void NonDefault_CanDelete()
    {
        // Arrange
        var race = new Race();
        var viewModel = new RaceViewModel(race, isDefault: false);

        // Assert
        Assert.False(viewModel.IsDefault);
        Assert.True(viewModel.CanDelete);
    }

    [Fact]
    public void OnPropertyValueChanged_CalledOnPropertyChange()
    {
        // Arrange
        var race = new Race();
        var viewModel = new RaceViewModel(race);
        var callbackInvoked = false;
        viewModel.OnPropertyValueChanged = _ => callbackInvoked = true;

        // Act
        viewModel.Name = "New Name";

        // Assert
        Assert.True(callbackInvoked);
    }

    [Fact]
    public void OnPropertyValueChanged_CalledOnStageChange()
    {
        // Arrange
        var race = new Race();
        var viewModel = new RaceViewModel(race);
        var callbackInvoked = false;
        viewModel.OnPropertyValueChanged = _ => callbackInvoked = true;

        // Act
        viewModel.FreePracticeEnabled = false;

        // Assert
        Assert.True(callbackInvoked);
    }

    [Fact]
    public void GetModel_ReturnsUnderlyingRace()
    {
        // Arrange
        var race = new Race();
        var viewModel = new RaceViewModel(race);

        // Act
        var result = viewModel.GetModel();

        // Assert
        Assert.Same(race, result);
    }

    [Fact]
    public void LapCount_ClampsToMinimumOne()
    {
        // Arrange
        var race = new Race();
        var viewModel = new RaceViewModel(race);

        // Act
        viewModel.FreePracticeLapCount = 0;

        // Assert
        Assert.Equal(1, race.FreePractice.LapCount);
    }

    [Fact]
    public void TimeMinutes_ClampsToMinimumOne()
    {
        // Arrange
        var race = new Race();
        var viewModel = new RaceViewModel(race);

        // Act
        viewModel.FreePracticeTimeMinutes = 0;

        // Assert
        Assert.Equal(1, race.FreePractice.TimeMinutes);
    }
}
