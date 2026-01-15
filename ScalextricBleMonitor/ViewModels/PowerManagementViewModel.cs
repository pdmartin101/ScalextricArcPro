using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ScalextricBleMonitor.Services;

namespace ScalextricBleMonitor.ViewModels;

/// <summary>
/// Manages track power control for the Scalextric powerbase.
/// Handles power enable/disable, per-slot power levels, ghost mode, and throttle profiles.
/// </summary>
public partial class PowerManagementViewModel : ObservableObject
{
    private readonly IBleMonitorService _bleMonitorService;
    private readonly ObservableCollection<ControllerViewModel> _controllers;
    private readonly Action<string> _setStatusText;
    private readonly Func<bool> _isGattConnected;
    private CancellationTokenSource? _powerHeartbeatCts;

    // Delay between BLE write operations to avoid flooding the connection
    private const int BleWriteDelayMs = 50;

    // Interval for sending power heartbeat commands (ms)
    private const int PowerHeartbeatIntervalMs = 200;

    /// <summary>
    /// Indicates whether track power is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isPowerEnabled;

    /// <summary>
    /// When true, use individual per-slot power levels. When false, use global PowerLevel for all slots.
    /// </summary>
    [ObservableProperty]
    private bool _usePerSlotPower = true;

    /// <summary>
    /// Global power level for all slots (0-63). Only used when UsePerSlotPower is false.
    /// </summary>
    [ObservableProperty]
    private int _powerLevel = 63;

    public PowerManagementViewModel(
        IBleMonitorService bleMonitorService,
        ObservableCollection<ControllerViewModel> controllers,
        Action<string> setStatusText,
        Func<bool> isGattConnected)
    {
        _bleMonitorService = bleMonitorService;
        _controllers = controllers;
        _setStatusText = setStatusText;
        _isGattConnected = isGattConnected;
    }

    partial void OnPowerLevelChanged(int value)
    {
        // Update status text when power level changes while power is enabled
        if (IsPowerEnabled)
        {
            _setStatusText($"Power enabled at level {value}");
        }
    }

    /// <summary>
    /// Enables track power with the current power level.
    /// </summary>
    public void EnablePower()
    {
        if (!_isGattConnected()) return;

        // Run the async enable operation without blocking
        RunFireAndForget(EnablePowerAsync, "EnablePower");
    }

    private async Task EnablePowerAsync()
    {
        _setStatusText("Writing throttle profiles...");

        // First write the throttle profiles for all slots sequentially
        var profilesWritten = await WriteThrottleProfilesAsync();

        if (!profilesWritten)
        {
            _setStatusText("Failed to write throttle profiles");
            return;
        }

        // Small delay before starting power
        await Task.Delay(BleWriteDelayMs);

        // Start the power heartbeat
        IsPowerEnabled = true;
        _setStatusText($"Power enabled at level {PowerLevel}");

        // Cancel any existing heartbeat
        _powerHeartbeatCts?.Cancel();
        _powerHeartbeatCts = new CancellationTokenSource();

        // Start continuous power command sending
        var token = _powerHeartbeatCts.Token;
        RunFireAndForget(() => PowerHeartbeatLoopAsync(token), "PowerHeartbeatLoop");
    }

    /// <summary>
    /// Continuously sends power commands to keep the track powered.
    /// The powerbase requires periodic commands to maintain power.
    /// </summary>
    private async Task PowerHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _isGattConnected() && IsPowerEnabled)
            {
                var command = BuildPowerCommand();
                var success = await _bleMonitorService.WriteCharacteristicAwaitAsync(
                    ScalextricProtocol.Characteristics.Command, command);

                if (!success)
                {
                    // Write failed - connection may be lost
                    Dispatcher.UIThread.Post(() =>
                    {
                        _setStatusText("Power command failed - connection lost?");
                    });
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
            Dispatcher.UIThread.Post(() =>
            {
                _setStatusText($"Power heartbeat error: {ex.Message}");
            });
        }
    }

    /// <summary>
    /// Builds a power command using per-controller power levels and ghost mode settings.
    /// </summary>
    private byte[] BuildPowerCommand()
    {
        var builder = new ScalextricProtocol.CommandBuilder
        {
            Type = ScalextricProtocol.CommandType.PowerOnRacing
        };

        // Always set per-slot settings to handle ghost mode correctly
        for (int i = 0; i < _controllers.Count; i++)
        {
            var controller = _controllers[i];
            var slot = builder.GetSlot(i + 1);

            // Set power level (either per-slot or global)
            slot.PowerMultiplier = (byte)(UsePerSlotPower ? controller.PowerLevel : PowerLevel);

            // Set ghost mode flag - in ghost mode, PowerMultiplier becomes direct throttle index
            slot.GhostMode = controller.IsGhostMode;
        }

        return builder.Build();
    }

    /// <summary>
    /// Disables track power.
    /// </summary>
    public void DisablePower()
    {
        if (!_isGattConnected()) return;

        // Stop the heartbeat first
        StopPowerHeartbeat();

        RunFireAndForget(DisablePowerAsync, "DisablePower");
    }

    private async Task DisablePowerAsync()
    {
        _setStatusText("Sending power off command...");

        await SendPowerOffSequenceAsync();

        IsPowerEnabled = false;
        _setStatusText("Power disabled");
    }

    /// <summary>
    /// Sends the power-off sequence: clear ghost commands followed by power-off commands.
    /// This shared method is used by both initial power-off and disable power operations.
    /// </summary>
    public async Task SendPowerOffSequenceAsync()
    {
        // First, send PowerOnRacing commands with all slots at power 0 and ghost mode OFF
        // This clears any latched ghost mode state from a previous session
        var clearGhostCommand = BuildClearGhostCommand();
        for (int i = 0; i < 3; i++)
        {
            await _bleMonitorService.WriteCharacteristicAwaitAsync(ScalextricProtocol.Characteristics.Command, clearGhostCommand);
            await Task.Delay(BleWriteDelayMs);
        }

        // Now send the actual power-off commands
        var powerOffCommand = BuildPowerOffCommand();
        for (int i = 0; i < 3; i++)
        {
            await _bleMonitorService.WriteCharacteristicAwaitAsync(ScalextricProtocol.Characteristics.Command, powerOffCommand);
            await Task.Delay(BleWriteDelayMs);
        }
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
    /// Reduces duplication between BuildClearGhostCommand and BuildPowerOffCommand.
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

    /// <summary>
    /// Toggles track power on/off.
    /// </summary>
    public void TogglePower()
    {
        if (IsPowerEnabled)
            DisablePower();
        else
            EnablePower();
    }

    /// <summary>
    /// Writes linear throttle profiles to all 6 slots sequentially with delays.
    /// Each slot requires 6 blocks of 17 bytes (block index + 16 throttle values).
    /// </summary>
    private async Task<bool> WriteThrottleProfilesAsync()
    {
        if (!_isGattConnected()) return false;

        // Get the throttle curve blocks (6 blocks of 17 bytes each)
        var blocks = ScalextricProtocol.ThrottleProfile.CreateLinearBlocks();

        for (int slot = 1; slot <= 6; slot++)
        {
            var uuid = ScalextricProtocol.Characteristics.GetThrottleProfileForSlot(slot);

            // Write all 6 blocks for this slot
            for (int blockIndex = 0; blockIndex < ScalextricProtocol.ThrottleProfile.BlockCount; blockIndex++)
            {
                _setStatusText($"Writing throttle profile slot {slot}, block {blockIndex + 1}/6...");

                var success = await _bleMonitorService.WriteCharacteristicAwaitAsync(uuid, blocks[blockIndex]);

                if (!success)
                {
                    _setStatusText($"Failed to write throttle profile for slot {slot}, block {blockIndex}");
                    return false;
                }

                // Delay between writes to avoid flooding the BLE connection
                await Task.Delay(BleWriteDelayMs);
            }
        }

        _setStatusText("Throttle profiles written successfully");
        return true;
    }

    /// <summary>
    /// Stops the power heartbeat when connection is lost.
    /// </summary>
    public void StopPowerHeartbeat()
    {
        _powerHeartbeatCts?.Cancel();
        _powerHeartbeatCts?.Dispose();
        _powerHeartbeatCts = null;
        IsPowerEnabled = false;
    }

    /// <summary>
    /// Sends power-off commands during shutdown to stop ghost cars.
    /// Uses best-effort approach: sends commands synchronously but with short timeout
    /// to avoid blocking the UI thread during shutdown.
    /// </summary>
    public void SendShutdownPowerOff()
    {
        try
        {
            // Build commands upfront
            var clearGhostCommand = BuildClearGhostCommand();
            var powerOffCommand = BuildPowerOffCommand();

            // Best-effort: send one clear ghost and one power-off command
            // We don't wait for responses - just fire and let the BLE service handle it
            // This is acceptable during shutdown since we're disposing anyway
            _bleMonitorService.WriteCharacteristicAwaitAsync(
                ScalextricProtocol.Characteristics.Command, clearGhostCommand)
                .Wait(100); // Very short wait, just enough to queue the write

            _bleMonitorService.WriteCharacteristicAwaitAsync(
                ScalextricProtocol.Characteristics.Command, powerOffCommand)
                .Wait(100);
        }
        catch
        {
            // Ignore errors during shutdown - we're disposing anyway
        }
    }

    /// <summary>
    /// Safely runs an async task without awaiting, handling any exceptions.
    /// This replaces the fire-and-forget pattern `_ = AsyncMethod()` to ensure errors are not silently swallowed.
    /// </summary>
    private void RunFireAndForget(Func<Task> asyncAction, string operationName)
    {
        Task.Run(async () =>
        {
            try
            {
                await asyncAction();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in {operationName}: {ex.Message}");
                Dispatcher.UIThread.Post(() => _setStatusText($"Error: {ex.Message}"));
            }
        });
    }
}
