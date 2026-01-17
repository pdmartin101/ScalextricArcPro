namespace Scalextric;

/// <summary>
/// Delegate for building power commands based on current controller states.
/// </summary>
public delegate byte[] PowerCommandBuilder();

/// <summary>
/// Service responsible for managing the power heartbeat loop that keeps track power enabled.
/// The powerbase requires periodic power commands (every ~200ms) to maintain track power.
/// </summary>
public interface IPowerHeartbeatService : IDisposable
{
    /// <summary>
    /// Gets whether the power heartbeat is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Event raised when the heartbeat encounters an error.
    /// </summary>
    event EventHandler<string>? HeartbeatError;

    /// <summary>
    /// Starts the power heartbeat loop.
    /// </summary>
    /// <param name="commandBuilder">Delegate that builds the power command based on current controller states.</param>
    void Start(PowerCommandBuilder commandBuilder);

    /// <summary>
    /// Stops the power heartbeat loop.
    /// </summary>
    void Stop();

    /// <summary>
    /// Sends power-off sequence (clear ghost commands followed by power-off commands).
    /// Used when disabling power or during shutdown.
    /// </summary>
    Task SendPowerOffSequenceAsync();
}
