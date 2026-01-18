using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scalextric;
using ScalextricBleMonitor.Services;
using Serilog;

namespace ScalextricBleMonitor.ViewModels;

/// <summary>
/// Manages track power control, throttle profiles, and power heartbeat.
/// </summary>
public partial class PowerControlViewModel : ObservableObject
{
    private readonly Scalextric.IBleService _bleService;
    private readonly IPowerHeartbeatService _powerHeartbeatService;
    private readonly ITimingCalibrationService _timingCalibrationService;
    private readonly IDispatcherService _dispatcher;
    private readonly AppSettings _settings;

    // Delay between BLE write operations to avoid flooding the connection
    private const int BleWriteDelayMs = 50;

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
    private int _powerLevel = ScalextricProtocol.MaxPowerLevel;

    /// <summary>
    /// Status text for power operations.
    /// </summary>
    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>
    /// Event raised when power is enabled and playback states should be updated.
    /// </summary>
    public event EventHandler? PowerEnabled;

    /// <summary>
    /// Event raised when power is disabled.
    /// </summary>
    public event EventHandler? PowerDisabled;

    /// <summary>
    /// Delegate for building power commands with ghost mode support.
    /// </summary>
    public PowerCommandBuilder? PowerCommandBuilder { get; set; }

    partial void OnPowerLevelChanged(int value)
    {
        // Update status text when power level changes while power is enabled
        if (IsPowerEnabled)
        {
            StatusText = $"Power enabled at level {value}";
        }
    }

    /// <summary>
    /// Initializes a new instance of the PowerControlViewModel.
    /// </summary>
    public PowerControlViewModel(
        Scalextric.IBleService bleService,
        IPowerHeartbeatService powerHeartbeatService,
        ITimingCalibrationService timingCalibrationService,
        IDispatcherService dispatcher,
        AppSettings settings)
    {
        _bleService = bleService;
        _powerHeartbeatService = powerHeartbeatService;
        _timingCalibrationService = timingCalibrationService;
        _dispatcher = dispatcher;
        _settings = settings;

        // Subscribe to power heartbeat errors
        _powerHeartbeatService.HeartbeatError += OnPowerHeartbeatError;

        // Initialize from persisted settings
        _powerLevel = _settings.PowerLevel;
        _usePerSlotPower = _settings.UsePerSlotPower;
    }

    private void OnPowerHeartbeatError(object? sender, string message)
    {
        _dispatcher.Post(() =>
        {
            StatusText = message;
            IsPowerEnabled = false;
        });
    }

    /// <summary>
    /// Enables track power with the current power level.
    /// </summary>
    /// <param name="isGattConnected">Whether GATT is connected.</param>
    public void EnablePower(bool isGattConnected)
    {
        if (!isGattConnected) return;

        // Run the async enable operation without blocking
        RunFireAndForget(EnablePowerAsync, "EnablePower");
    }

    private async Task EnablePowerAsync()
    {
        StatusText = "Writing throttle profiles...";

        // First write the throttle profiles for all slots sequentially
        var profilesWritten = await WriteThrottleProfilesAsync();

        if (!profilesWritten)
        {
            StatusText = "Failed to write throttle profiles";
            return;
        }

        // Small delay before starting power
        await Task.Delay(BleWriteDelayMs);

        // Reset timing calibration - wait for first slot notification after power-on
        _timingCalibrationService.Reset();

        // Start the power heartbeat
        IsPowerEnabled = true;
        StatusText = $"Power enabled at level {PowerLevel}";

        // Notify that power is enabled (for ghost playback state updates)
        PowerEnabled?.Invoke(this, EventArgs.Empty);

        // Start continuous power command sending using the heartbeat service
        if (PowerCommandBuilder != null)
        {
            _powerHeartbeatService.Start(PowerCommandBuilder);
        }
    }

    /// <summary>
    /// Disables track power.
    /// </summary>
    /// <param name="isGattConnected">Whether GATT is connected.</param>
    public void DisablePower(bool isGattConnected)
    {
        if (!isGattConnected) return;

        // Stop the heartbeat first
        _powerHeartbeatService.Stop();

        RunFireAndForget(DisablePowerAsync, "DisablePower");
    }

    private async Task DisablePowerAsync()
    {
        StatusText = "Sending power off command...";

        // Notify that power is being disabled
        PowerDisabled?.Invoke(this, EventArgs.Empty);

        await _powerHeartbeatService.SendPowerOffSequenceAsync();

        IsPowerEnabled = false;
        StatusText = "Power disabled";
    }

    /// <summary>
    /// Handles loss of GATT connection.
    /// </summary>
    public void OnGattDisconnected()
    {
        _powerHeartbeatService.Stop();
        IsPowerEnabled = false;
    }

    /// <summary>
    /// Sends initial power-off when GATT connection is established.
    /// </summary>
    public void SendInitialPowerOff()
    {
        RunFireAndForget(async () =>
        {
            // Small delay to ensure connection is stable
            await Task.Delay(100);
            await _powerHeartbeatService.SendPowerOffSequenceAsync();
        }, "SendInitialPowerOff");
    }

    /// <summary>
    /// Sends power-off sequence during shutdown (synchronous, best-effort).
    /// </summary>
    public void SendShutdownPowerOff()
    {
        try
        {
            // Best-effort: send power-off sequence
            _powerHeartbeatService.SendPowerOffSequenceAsync().Wait(200);
        }
        catch
        {
            // Ignore errors during shutdown
        }
    }

    /// <summary>
    /// Writes throttle profiles to all 6 slots sequentially with delays.
    /// </summary>
    /// <param name="getThrottleProfile">Function to get throttle profile for a slot (0-based index).</param>
    public async Task<bool> WriteThrottleProfilesAsync(Func<int, ThrottleProfileType>? getThrottleProfile = null)
    {
        for (int slot = 1; slot <= 6; slot++)
        {
            var profileType = getThrottleProfile?.Invoke(slot - 1) ?? ThrottleProfileType.Linear;

            // Get the throttle curve blocks for this slot's profile type
            var blocks = ThrottleProfileHelper.CreateBlocks(profileType);
            var uuid = ScalextricProtocol.Characteristics.GetThrottleProfileForSlot(slot);

            // Write all 6 blocks for this slot
            for (int blockIndex = 0; blockIndex < ScalextricProtocol.ThrottleProfile.BlockCount; blockIndex++)
            {
                StatusText = $"Writing throttle profile slot {slot} ({profileType}), block {blockIndex + 1}/6...";

                var success = await _bleService.WriteCharacteristicAsync(uuid, blocks[blockIndex]);

                if (!success)
                {
                    StatusText = $"Failed to write throttle profile for slot {slot}, block {blockIndex}";
                    return false;
                }

                // Delay between writes to avoid flooding the BLE connection
                await Task.Delay(BleWriteDelayMs);
            }
        }

        StatusText = "Throttle profiles written successfully";
        return true;
    }

    /// <summary>
    /// Saves power settings to persistent storage.
    /// </summary>
    public void SaveSettings()
    {
        _settings.PowerLevel = PowerLevel;
        _settings.UsePerSlotPower = UsePerSlotPower;
        _settings.Save();
    }

    /// <summary>
    /// Safely runs an async task without awaiting, handling any exceptions.
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
                Log.Error(ex, "Error in {OperationName}", operationName);
                _dispatcher.Post(() => StatusText = $"Error: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        _powerHeartbeatService.HeartbeatError -= OnPowerHeartbeatError;
        _powerHeartbeatService.Dispose();
    }
}
