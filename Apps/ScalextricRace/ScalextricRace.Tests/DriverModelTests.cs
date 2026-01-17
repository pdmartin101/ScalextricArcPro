using ScalextricRace.Models;

namespace ScalextricRace.Tests;

/// <summary>
/// Unit tests for Driver model.
/// </summary>
public class DriverModelTests
{
    [Fact]
    public void Constructor_SetsNameAndDefaults()
    {
        // Act
        var driver = new Driver("Test Driver");

        // Assert
        Assert.Equal("Test Driver", driver.Name);
        Assert.NotEqual(Guid.Empty, driver.Id);
        Assert.Null(driver.PowerPercentage);
    }

    [Fact]
    public void CreateDefault_CreatesDriverWithDefaultId()
    {
        // Act
        var driver = Driver.CreateDefault();

        // Assert
        Assert.Equal(Driver.DefaultDriverId, driver.Id);
        Assert.Equal("Default Driver", driver.Name);
    }

    [Fact]
    public void DefaultDriverId_IsConsistent()
    {
        // The default driver ID should be the same every time
        var id1 = Driver.DefaultDriverId;
        var id2 = Driver.DefaultDriverId;

        Assert.Equal(id1, id2);
    }

    [Fact]
    public void PowerPercentage_CanBeSetToValue()
    {
        // Arrange
        var driver = new Driver("Test Driver");

        // Act
        driver.PowerPercentage = 75;

        // Assert
        Assert.Equal(75, driver.PowerPercentage);
    }

    [Fact]
    public void PowerPercentage_CanBeSetToNull()
    {
        // Arrange
        var driver = new Driver("Test Driver") { PowerPercentage = 75 };

        // Act
        driver.PowerPercentage = null;

        // Assert
        Assert.Null(driver.PowerPercentage);
    }
}
