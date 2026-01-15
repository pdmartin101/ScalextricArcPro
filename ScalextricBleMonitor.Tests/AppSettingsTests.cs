using ScalextricBleMonitor.Services;

namespace ScalextricBleMonitor.Tests;

public class AppSettingsTests
{
    [Fact]
    public void DefaultSettings_HasCorrectPowerLevel()
    {
        var settings = new AppSettings();

        Assert.Equal(63, settings.PowerLevel);
    }

    [Fact]
    public void DefaultSettings_HasCorrectSlotPowerLevels()
    {
        var settings = new AppSettings();

        Assert.Equal(6, settings.SlotPowerLevels.Length);
        Assert.All(settings.SlotPowerLevels, level => Assert.Equal(63, level));
    }

    [Fact]
    public void DefaultSettings_HasPerSlotPowerEnabled()
    {
        var settings = new AppSettings();

        Assert.True(settings.UsePerSlotPower);
    }

    [Fact]
    public void DefaultSettings_HasGhostModesDisabled()
    {
        var settings = new AppSettings();

        Assert.Equal(6, settings.SlotGhostModes.Length);
        Assert.All(settings.SlotGhostModes, mode => Assert.False(mode));
    }

    [Fact]
    public void Load_ReturnsValidSettings()
    {
        // Load() should return a valid AppSettings instance
        // It will either load from file or return defaults
        var settings = AppSettings.Load();

        Assert.NotNull(settings);
        // PowerLevel should be within valid range (0-63)
        Assert.InRange(settings.PowerLevel, 0, 63);
        // Should have 6 slots
        Assert.Equal(6, settings.SlotPowerLevels.Length);
        Assert.Equal(6, settings.SlotGhostModes.Length);
    }

    [Fact]
    public void PowerLevel_CanBeModified()
    {
        var settings = new AppSettings();

        settings.PowerLevel = 30;

        Assert.Equal(30, settings.PowerLevel);
    }

    [Fact]
    public void SlotPowerLevels_CanBeModified()
    {
        var settings = new AppSettings();

        settings.SlotPowerLevels[2] = 45;

        Assert.Equal(45, settings.SlotPowerLevels[2]);
    }

    [Fact]
    public void SlotGhostModes_CanBeModified()
    {
        var settings = new AppSettings();

        settings.SlotGhostModes[0] = true;
        settings.SlotGhostModes[3] = true;

        Assert.True(settings.SlotGhostModes[0]);
        Assert.False(settings.SlotGhostModes[1]);
        Assert.False(settings.SlotGhostModes[2]);
        Assert.True(settings.SlotGhostModes[3]);
        Assert.False(settings.SlotGhostModes[4]);
        Assert.False(settings.SlotGhostModes[5]);
    }

    [Fact]
    public void UsePerSlotPower_CanBeModified()
    {
        var settings = new AppSettings();

        settings.UsePerSlotPower = false;

        Assert.False(settings.UsePerSlotPower);
    }
}
