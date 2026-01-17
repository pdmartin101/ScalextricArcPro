using ScalextricRace.Models;
using ScalextricRace.ViewModels;

namespace ScalextricRace.Tests;

/// <summary>
/// Unit tests for DriverViewModel.
/// </summary>
public class DriverViewModelTests
{
    [Fact]
    public void Constructor_InitializesFromModel()
    {
        // Arrange
        var driver = new Driver("Test Driver")
        {
            PowerLimit = 40
        };

        // Act
        var viewModel = new DriverViewModel(driver);

        // Assert
        Assert.Equal("Test Driver", viewModel.Name);
        Assert.Equal(40, viewModel.PowerLimit);
        Assert.Equal(driver.Id, viewModel.Id);
    }

    [Fact]
    public void Name_ChangesAreSyncedToModel()
    {
        // Arrange
        var driver = new Driver("Original Name");
        var viewModel = new DriverViewModel(driver);

        // Act
        viewModel.Name = "New Name";

        // Assert
        Assert.Equal("New Name", driver.Name);
    }

    [Fact]
    public void PowerLimit_ClampsToValidRange()
    {
        // Arrange
        var driver = new Driver("Test Driver");
        var viewModel = new DriverViewModel(driver);

        // Act
        viewModel.PowerLimit = 100; // Above max

        // Assert
        Assert.Equal(63, driver.PowerLimit);
    }

    [Fact]
    public void PowerLimit_NullMeansNoLimit()
    {
        // Arrange
        var driver = new Driver("Test Driver") { PowerLimit = null };
        var viewModel = new DriverViewModel(driver);

        // Assert
        Assert.Null(viewModel.PowerLimit);
        Assert.False(viewModel.HasPowerLimit);
    }

    [Fact]
    public void HasPowerLimit_TrueWhenLessThan63()
    {
        // Arrange
        var driver = new Driver("Test Driver") { PowerLimit = 40 };
        var viewModel = new DriverViewModel(driver);

        // Assert
        Assert.True(viewModel.HasPowerLimit);
    }

    [Fact]
    public void HasPowerLimit_FalseWhen63()
    {
        // Arrange
        var driver = new Driver("Test Driver") { PowerLimit = 63 };
        var viewModel = new DriverViewModel(driver);

        // Assert
        Assert.False(viewModel.HasPowerLimit);
    }

    [Fact]
    public void IsDefault_PreventsDelete()
    {
        // Arrange
        var driver = new Driver("Default Driver");
        var viewModel = new DriverViewModel(driver, isDefault: true);

        // Assert
        Assert.True(viewModel.IsDefault);
        Assert.False(viewModel.CanDelete);
    }

    [Fact]
    public void Changed_RaisedOnPropertyChange()
    {
        // Arrange
        var driver = new Driver("Test Driver");
        var viewModel = new DriverViewModel(driver);
        var eventRaised = false;
        viewModel.Changed += (_, _) => eventRaised = true;

        // Act
        viewModel.Name = "New Name";

        // Assert
        Assert.True(eventRaised);
    }

    [Fact]
    public void GetModel_ReturnsUnderlyingDriver()
    {
        // Arrange
        var driver = new Driver("Test Driver");
        var viewModel = new DriverViewModel(driver);

        // Act
        var result = viewModel.GetModel();

        // Assert
        Assert.Same(driver, result);
    }
}
