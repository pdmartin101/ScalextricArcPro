using ScalextricRace.Models;

namespace ScalextricRace.Tests;

/// <summary>
/// Unit tests for Car model.
/// </summary>
public class CarModelTests
{
    [Fact]
    public void Constructor_SetsNameAndDefaults()
    {
        // Act
        var car = new Car("Test Car");

        // Assert
        Assert.Equal("Test Car", car.Name);
        Assert.NotEqual(Guid.Empty, car.Id);
        Assert.Equal(63, car.DefaultPower);
        Assert.Equal(45, car.GhostMaxPower);
        Assert.Equal(10, car.MinPower);
    }

    [Fact]
    public void CreateDefault_CreatesCarWithDefaultId()
    {
        // Act
        var car = Car.CreateDefault();

        // Assert
        Assert.Equal(Car.DefaultCarId, car.Id);
        Assert.Equal("Default Car", car.Name);
    }

    [Fact]
    public void DefaultCarId_IsConsistent()
    {
        // The default car ID should be the same every time
        var id1 = Car.DefaultCarId;
        var id2 = Car.DefaultCarId;

        Assert.Equal(id1, id2);
    }
}
