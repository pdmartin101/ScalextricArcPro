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
            PowerPercentage = 75
        };

        // Act
        var viewModel = new DriverViewModel(driver);

        // Assert
        Assert.Equal("Test Driver", viewModel.Name);
        Assert.Equal(75, viewModel.PowerPercentage);
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
    public void PowerPercentage_ClampsToMaximum()
    {
        // Arrange
        var driver = new Driver("Test Driver");
        var viewModel = new DriverViewModel(driver);

        // Act
        viewModel.PowerPercentage = 150; // Above max (100%)

        // Assert
        Assert.Equal(100, driver.PowerPercentage);
    }

    [Fact]
    public void PowerPercentage_ClampsToMinimum()
    {
        // Arrange
        var driver = new Driver("Test Driver");
        var viewModel = new DriverViewModel(driver);

        // Act
        viewModel.PowerPercentage = 25; // Below min (50%)

        // Assert
        Assert.Equal(50, driver.PowerPercentage);
    }

    [Fact]
    public void PowerPercentage_NullMeans100Percent()
    {
        // Arrange
        var driver = new Driver("Test Driver") { PowerPercentage = null };
        var viewModel = new DriverViewModel(driver);

        // Assert
        Assert.Null(viewModel.PowerPercentage);
        Assert.False(viewModel.HasPowerRestriction);
    }

    [Fact]
    public void HasPowerRestriction_TrueWhenLessThan100()
    {
        // Arrange
        var driver = new Driver("Test Driver") { PowerPercentage = 75 };
        var viewModel = new DriverViewModel(driver);

        // Assert
        Assert.True(viewModel.HasPowerRestriction);
    }

    [Fact]
    public void HasPowerRestriction_FalseWhen100()
    {
        // Arrange
        var driver = new Driver("Test Driver") { PowerPercentage = 100 };
        var viewModel = new DriverViewModel(driver);

        // Assert
        Assert.False(viewModel.HasPowerRestriction);
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
    public void OnPropertyValueChanged_CalledOnPropertyChange()
    {
        // Arrange
        var driver = new Driver("Test Driver");
        var viewModel = new DriverViewModel(driver);
        var callbackInvoked = false;
        viewModel.OnPropertyValueChanged = _ => callbackInvoked = true;

        // Act
        viewModel.Name = "New Name";

        // Assert
        Assert.True(callbackInvoked);
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
