using System;
using System.Threading;
using System.Threading.Tasks;
using ScalextricBle;
using Serilog;

namespace ScalextricBleMonitor.Services;

/// <summary>
/// Service responsible for managing the power heartbeat loop that keeps track power enabled.
/// The powerbase requires periodic power commands (every ~200ms) to maintain track power.
/// </summary>
public class PowerHeartbeatService : IPowerHeartbeatService
{
    private readonly IBleMonitorService _bleService;
    private CancellationTokenSource? _heartbeatCts;
    private bool _disposed;

    // Delay between BLE write operations to avoid flooding the connection
    private const int BleWriteDelayMs = 50;

    // Interval for sending power heartbeat commands (ms)
    private const int PowerHeartbeatIntervalMs = 200;

    /// <inheritdoc/>
    public bool IsRunning => _heartbeatCts != null && !_heartbeatCts.IsCancellationRequested;

    /// <inheritdoc/>
    public event EventHandler<string>? HeartbeatError;

    /// <summary>
    /// Creates a new PowerHeartbeatService with the specified BLE service.
    /// </summary>
    /// <param name="bleService">The BLE service for sending commands.</param>
    public PowerHeartbeatService(IBleMonitorService bleService)
    {
        _bleService = bleService;
    }

    /// <inheritdoc/>
    public void Start(PowerCommandBuilder commandBuilder)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PowerHeartbeatService));

        // Cancel any existing heartbeat
        Stop();

        _heartbeatCts = new CancellationTokenSource();
        var token = _heartbeatCts.Token;

        // Start the heartbeat loop on a background thread
        Task.Run(async () => await HeartbeatLoopAsync(commandBuilder, token), token);

        Log.Information("Power heartbeat started");
    }

    /// <inheritdoc/>
    public void Stop()
    {
        if (_heartbeatCts != null)
        {
            _heartbeatCts.Cancel();
            _heartbeatCts.Dispose();
            _heartbeatCts = null;
            Log.Information("Power heartbeat stopped");
        }
    }

    /// <inheritdoc/>
    public async Task SendPowerOffSequenceAsync()
    {
        // First, send PowerOnRacing commands with all slots at power 0 and ghost mode OFF
        // This clears any latched ghost mode state from a previous session
        var clearGhostCommand = BuildClearGhostCommand();
        for (int i = 0; i < 3; i++)
        {
            await _bleService.WriteCharacteristicAwaitAsync(ScalextricProtocol.Characteristics.Command, clearGhostCommand);
            await Task.Delay(BleWriteDelayMs);
        }

        // Now send the actual power-off commands
        var powerOffCommand = BuildPowerOffCommand();
        for (int i = 0; i < 3; i++)
        {
            await _bleService.WriteCharacteristicAwaitAsync(ScalextricProtocol.Characteristics.Command, powerOffCommand);
            await Task.Delay(BleWriteDelayMs);
        }
    }

    /// <summary>
    /// Continuously sends power commands to keep the track powered.
    /// The powerbase requires periodic commands to maintain power.
    /// </summary>
    private async Task HeartbeatLoopAsync(PowerCommandBuilder commandBuilder, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var command = commandBuilder();
                var success = await _bleService.WriteCharacteristicAwaitAsync(
                    ScalextricProtocol.Characteristics.Command, command);

                if (!success)
                {
                    // Write failed - connection may be lost
                    OnHeartbeatError("Power command failed - connection lost?");
                    break;
                }

                await Task.Delay(PowerHeartbeatIntervalMs, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation, ignore
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in power heartbeat loop");
            OnHeartbeatError($"Power heartbeat error: {ex.Message}");
        }
    }

    private void OnHeartbeatError(string message)
    {
        HeartbeatError?.Invoke(this, message);
    }

    /// <summary>
    /// Builds a command that keeps power on but clears ghost mode on all slots with power 0.
    /// This is used to transition out of ghost mode before cutting power.
    /// </summary>
    private static byte[] BuildClearGhostCommand()
    {
        return BuildCommandWithAllSlotsZeroed(ScalextricProtocol.CommandType.PowerOnRacing);
    }

    /// <summary>
    /// Builds a power-off command that clears ghost mode on all slots.
    /// </summary>
    private static byte[] BuildPowerOffCommand()
    {
        return BuildCommandWithAllSlotsZeroed(ScalextricProtocol.CommandType.NoPowerTimerStopped);
    }

    /// <summary>
    /// Helper method to build a command with all slots set to power 0 and ghost mode disabled.
    /// </summary>
    private static byte[] BuildCommandWithAllSlotsZeroed(ScalextricProtocol.CommandType commandType)
    {
        var builder = new ScalextricProtocol.CommandBuilder
        {
            Type = commandType
        };

        for (int i = 1; i <= 6; i++)
        {
            var slot = builder.GetSlot(i);
            slot.PowerMultiplier = 0;
            slot.GhostMode = false;
        }

        return builder.Build();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        GC.SuppressFinalize(this);
    }
}
