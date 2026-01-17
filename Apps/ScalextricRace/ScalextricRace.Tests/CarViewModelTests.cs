using ScalextricRace.Models;
using ScalextricRace.ViewModels;

namespace ScalextricRace.Tests;

/// <summary>
/// Unit tests for CarViewModel.
/// </summary>
public class CarViewModelTests
{
    [Fact]
    public void Constructor_InitializesFromModel()
    {
        // Arrange
        var car = new Car("Test Car")
        {
            DefaultPower = 50,
            GhostMaxPower = 40,
            MinPower = 10
        };

        // Act
        var viewModel = new CarViewModel(car);

        // Assert
        Assert.Equal("Test Car", viewModel.Name);
        Assert.Equal(50, viewModel.DefaultPower);
        Assert.Equal(40, viewModel.GhostMaxPower);
        Assert.Equal(10, viewModel.MinPower);
        Assert.Equal(car.Id, viewModel.Id);
    }

    [Fact]
    public void Name_ChangesAreSyncedToModel()
    {
        // Arrange
        var car = new Car("Original Name");
        var viewModel = new CarViewModel(car);

        // Act
        viewModel.Name = "New Name";

        // Assert
        Assert.Equal("New Name", car.Name);
    }

    [Fact]
    public void DefaultPower_ClampsToValidRange()
    {
        // Arrange
        var car = new Car("Test Car");
        var viewModel = new CarViewModel(car);

        // Act
        viewModel.DefaultPower = 100; // Above max

        // Assert
        Assert.Equal(63, car.DefaultPower);
    }

    [Fact]
    public void DefaultPower_ClampsToMinimum()
    {
        // Arrange
        var car = new Car("Test Car");
        var viewModel = new CarViewModel(car);

        // Act
        viewModel.DefaultPower = -10; // Below min

        // Assert
        Assert.Equal(0, car.DefaultPower);
    }

    [Fact]
    public void IsDefault_PreventsDelete()
    {
        // Arrange
        var car = new Car("Default Car");
        var viewModel = new CarViewModel(car, isDefault: true);

        // Assert
        Assert.True(viewModel.IsDefault);
        Assert.False(viewModel.CanDelete);
    }

    [Fact]
    public void Changed_RaisedOnPropertyChange()
    {
        // Arrange
        var car = new Car("Test Car");
        var viewModel = new CarViewModel(car);
        var eventRaised = false;
        viewModel.Changed += (_, _) => eventRaised = true;

        // Act
        viewModel.Name = "New Name";

        // Assert
        Assert.True(eventRaised);
    }

    [Fact]
    public void GetModel_ReturnsUnderlyingCar()
    {
        // Arrange
        var car = new Car("Test Car");
        var viewModel = new CarViewModel(car);

        // Act
        var result = viewModel.GetModel();

        // Assert
        Assert.Same(car, result);
    }

    [Fact]
    public void HasImage_FalseWhenNoImagePath()
    {
        // Arrange
        var car = new Car("Test Car") { ImagePath = null };
        var viewModel = new CarViewModel(car);

        // Assert
        Assert.False(viewModel.HasImage);
    }

    [Fact]
    public void HasImage_FalseWhenEmptyImagePath()
    {
        // Arrange
        var car = new Car("Test Car") { ImagePath = "" };
        var viewModel = new CarViewModel(car);

        // Assert
        Assert.False(viewModel.HasImage);
    }
}
