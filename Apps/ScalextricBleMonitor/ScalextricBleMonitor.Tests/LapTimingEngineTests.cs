using Scalextric;

namespace ScalextricBleMonitor.Tests;

public class LapTimingEngineTests
{
    [Fact]
    public void UpdateTimestamps_ZeroTimestamp_ReturnsNoChange()
    {
        var engine = new LapTimingEngine();

        var result = engine.UpdateTimestamps(0, 0);

        Assert.False(result.LapCompleted);
        Assert.Equal(0, engine.CurrentLap);
    }

    [Fact]
    public void UpdateTimestamps_FirstTimestamp_StartsLap1()
    {
        var engine = new LapTimingEngine();

        var result = engine.UpdateTimestamps(1000, 0);

        Assert.True(result.LapCompleted);  // First crossing starts lap 1
        Assert.Equal(1, engine.CurrentLap);
        Assert.Equal(1, result.CrossedLane);
        Assert.Equal(0, result.LapTimeSeconds);  // No lap time yet
    }

    [Fact]
    public void UpdateTimestamps_SecondCrossing_CompletesLap1()
    {
        var engine = new LapTimingEngine();

        // First crossing starts lap 1
        engine.UpdateTimestamps(1000, 0);

        // Second crossing completes lap 1, starts lap 2
        var result = engine.UpdateTimestamps(2000, 0);

        Assert.True(result.LapCompleted);
        Assert.Equal(2, result.CurrentLap);
        Assert.Equal(2, engine.CurrentLap);
        Assert.Equal(10.0, result.LapTimeSeconds); // (2000 - 1000) / 100 = 10 seconds
    }

    [Fact]
    public void UpdateTimestamps_ThirdCrossing_CompletesLap2()
    {
        var engine = new LapTimingEngine();

        // First crossing starts lap 1
        engine.UpdateTimestamps(1000, 0);
        // Second crossing completes lap 1, starts lap 2
        engine.UpdateTimestamps(2000, 0);
        // Third crossing completes lap 2, starts lap 3
        var result = engine.UpdateTimestamps(3000, 0);

        Assert.True(result.LapCompleted);
        Assert.Equal(3, result.CurrentLap);
        Assert.Equal(10.0, result.LapTimeSeconds); // (3000 - 2000) / 100 = 10 seconds
        Assert.Equal(10.0, result.BestLapTimeSeconds);
        Assert.False(result.IsNewBestLap);  // Same time as lap 1, not a new best
    }

    [Fact]
    public void UpdateTimestamps_FasterLap_UpdatesBestTime()
    {
        var engine = new LapTimingEngine();

        // Setup: start lap 1, complete lap 1 (10s), complete lap 2 (10s)
        engine.UpdateTimestamps(1000, 0);  // Start lap 1
        engine.UpdateTimestamps(2000, 0);  // Complete lap 1 (10s), start lap 2
        engine.UpdateTimestamps(3000, 0);  // Complete lap 2 (10s), start lap 3

        // Complete lap 3 in 8 seconds (faster)
        var result = engine.UpdateTimestamps(3800, 0);

        Assert.Equal(8.0, result.LapTimeSeconds);
        Assert.Equal(8.0, result.BestLapTimeSeconds);
        Assert.True(result.IsNewBestLap);
    }

    [Fact]
    public void UpdateTimestamps_SlowerLap_KeepsBestTime()
    {
        var engine = new LapTimingEngine();

        // Setup: start lap 1, complete lap 1 (10s), complete lap 2 (10s)
        engine.UpdateTimestamps(1000, 0);  // Start lap 1
        engine.UpdateTimestamps(2000, 0);  // Complete lap 1 (10s), start lap 2
        engine.UpdateTimestamps(3000, 0);  // Complete lap 2 (10s), start lap 3

        // Complete lap 3 in 12 seconds (slower)
        var result = engine.UpdateTimestamps(4200, 0);

        Assert.Equal(12.0, result.LapTimeSeconds);
        Assert.Equal(10.0, result.BestLapTimeSeconds); // Best is still 10s from lap 1
        Assert.False(result.IsNewBestLap);
    }

    [Fact]
    public void UpdateTimestamps_Lane2Crossing_DetectsCorrectLane()
    {
        var engine = new LapTimingEngine();

        // First crossing on lane 1 - starts lap 1
        engine.UpdateTimestamps(1000, 0);

        // Second crossing on lane 2 (higher timestamp) - completes lap 1
        var result = engine.UpdateTimestamps(1000, 2000);

        Assert.True(result.LapCompleted);
        Assert.Equal(2, result.CrossedLane);
        Assert.Equal(2, engine.CurrentLane);
    }

    [Fact]
    public void UpdateTimestamps_Lane1Crossing_DetectsCorrectLane()
    {
        var engine = new LapTimingEngine();

        // First crossing - starts lap 1
        engine.UpdateTimestamps(1000, 500);

        // Second crossing on lane 1 (lane 1 has higher timestamp) - completes lap 1
        var result = engine.UpdateTimestamps(2000, 500);

        Assert.True(result.LapCompleted);
        Assert.Equal(1, result.CrossedLane);
        Assert.Equal(1, engine.CurrentLane);
    }

    [Fact]
    public void UpdateTimestamps_SameTimestamp_ReturnsNoChange()
    {
        var engine = new LapTimingEngine();

        // First crossing - starts lap 1
        engine.UpdateTimestamps(1000, 500);

        // Same max timestamp (no change - still on lap 1)
        var result = engine.UpdateTimestamps(1000, 500);

        Assert.False(result.LapCompleted);
        Assert.Equal(1, engine.CurrentLap);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var engine = new LapTimingEngine();

        // Build up some state
        engine.UpdateTimestamps(1000, 0);  // Start lap 1
        engine.UpdateTimestamps(2000, 0);  // Complete lap 1
        engine.UpdateTimestamps(3000, 0);  // Complete lap 2

        // Reset
        engine.Reset();

        Assert.Equal(0, engine.CurrentLap);
        Assert.Equal(0, engine.CurrentLane);
        Assert.Equal(0, engine.LastLapTimeSeconds);
        Assert.Equal(0, engine.BestLapTimeSeconds);

        // After reset, next timestamp should start lap 1 again
        var result = engine.UpdateTimestamps(5000, 0);
        Assert.True(result.LapCompleted);
        Assert.Equal(1, engine.CurrentLap);
    }

    [Fact]
    public void UpdateTimestamps_TimestampOverflow_CalculatesCorrectLapTime()
    {
        var engine = new LapTimingEngine();

        // Setup near uint max value - first crossing starts lap 1
        engine.UpdateTimestamps(uint.MaxValue - 500, 0);
        // Second crossing completes lap 1
        engine.UpdateTimestamps(uint.MaxValue - 400, 0);

        // Overflow scenario: timestamp wraps around - completes lap 2
        var result = engine.UpdateTimestamps(100, 0);

        Assert.True(result.LapCompleted);
        // Time difference should be 501 (400 remaining + 100 after wrap + 1 for the wrap)
        Assert.Equal(5.01, result.LapTimeSeconds, 2); // ~5 seconds with small precision tolerance
    }

    [Fact]
    public void NoChange_ReturnsCorrectState()
    {
        var engine = new LapTimingEngine();

        // Build up state
        engine.UpdateTimestamps(1000, 0);  // Start lap 1
        engine.UpdateTimestamps(2000, 0);  // Complete lap 1 (10s), start lap 2
        engine.UpdateTimestamps(3000, 0);  // Complete lap 2 (10s), start lap 3

        // Get a no-change result
        var result = engine.UpdateTimestamps(3000, 0);

        Assert.False(result.LapCompleted);
        Assert.Equal(3, result.CurrentLap);
        Assert.Equal(1, result.CrossedLane);
        Assert.Equal(10.0, result.BestLapTimeSeconds);
    }
}
