using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scalextric;
using ScalextricRace.Models;
using ScalextricRace.Services;
using Serilog;

namespace ScalextricRace.ViewModels;

/// <summary>
/// Tuning wizard stage enumeration.
/// </summary>
public enum TuningStage
{
    /// <summary>
    /// Stage 1: Set default power using throttle.
    /// </summary>
    DefaultPower,

    /// <summary>
    /// Stage 2: Set ghost max power (future).
    /// </summary>
    GhostMaxPower,

    /// <summary>
    /// Stage 3: Set min power (future).
    /// </summary>
    MinPower
}

/// <summary>
/// View model for the car tuning wizard.
/// Manages the 3-stage tuning process for configuring car power settings.
/// </summary>
public partial class CarTuningViewModel : ObservableObject
{
    private readonly Services.IBleService? _bleService;
    private readonly CarViewModel _carViewModel;
    private readonly Car _originalValues;
    private bool _suppressPowerCommand;

    /// <summary>
    /// Event raised when tuning is complete and values should be saved.
    /// </summary>
    public event EventHandler? TuningComplete;

    /// <summary>
    /// Event raised when tuning is cancelled.
    /// </summary>
    public event EventHandler? TuningCancelled;

    /// <summary>
    /// Gets the car being tuned.
    /// </summary>
    public CarViewModel Car => _carViewModel;

    /// <summary>
    /// Gets the car name for display.
    /// </summary>
    public string CarName => _carViewModel.Name;

    /// <summary>
    /// The current tuning stage.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDefaultPowerStage))]
    [NotifyPropertyChangedFor(nameof(IsGhostMaxPowerStage))]
    [NotifyPropertyChangedFor(nameof(IsMinPowerStage))]
    [NotifyPropertyChangedFor(nameof(IsGhostModeStage))]
    [NotifyPropertyChangedFor(nameof(StageTitle))]
    [NotifyPropertyChangedFor(nameof(StageDescription))]
    [NotifyPropertyChangedFor(nameof(StageNumber))]
    [NotifyPropertyChangedFor(nameof(ContinueButtonText))]
    private TuningStage _currentStage = TuningStage.DefaultPower;

    /// <summary>
    /// The selected slot number (1-6).
    /// </summary>
    [ObservableProperty]
    private int _selectedSlot = 1;

    /// <summary>
    /// Available slot numbers for selection.
    /// </summary>
    public static int[] AvailableSlots { get; } = [1, 2, 3, 4, 5, 6];

    /// <summary>
    /// The current power level being tuned.
    /// </summary>
    [ObservableProperty]
    private int _powerLevel;


    /// <summary>
    /// Gets whether we're on the Default Power stage.
    /// </summary>
    public bool IsDefaultPowerStage => CurrentStage == TuningStage.DefaultPower;

    /// <summary>
    /// Gets whether we're on the Ghost Max Power stage.
    /// </summary>
    public bool IsGhostMaxPowerStage => CurrentStage == TuningStage.GhostMaxPower;

    /// <summary>
    /// Gets whether we're on the Min Power stage.
    /// </summary>
    public bool IsMinPowerStage => CurrentStage == TuningStage.MinPower;

    /// <summary>
    /// Gets whether we're on a ghost mode stage (stages 2 or 3).
    /// </summary>
    public bool IsGhostModeStage => CurrentStage != TuningStage.DefaultPower;

    /// <summary>
    /// Gets the button text for continuing/saving.
    /// </summary>
    public string ContinueButtonText => CurrentStage == TuningStage.MinPower ? "Save" : "Next";

    /// <summary>
    /// Gets the current stage number (1-based).
    /// </summary>
    public int StageNumber => CurrentStage switch
    {
        TuningStage.DefaultPower => 1,
        TuningStage.GhostMaxPower => 2,
        TuningStage.MinPower => 3,
        _ => 1
    };

    /// <summary>
    /// Gets the title for the current stage.
    /// </summary>
    public string StageTitle => CurrentStage switch
    {
        TuningStage.DefaultPower => "Default Power",
        TuningStage.GhostMaxPower => "Ghost Max Power",
        TuningStage.MinPower => "Minimum Power",
        _ => "Tuning"
    };

    /// <summary>
    /// Gets the description for the current stage.
    /// </summary>
    public string StageDescription => CurrentStage switch
    {
        TuningStage.DefaultPower => "Set the maximum power level for normal driving.\nUse the throttle to test how the car handles at this power limit.",
        TuningStage.GhostMaxPower => "Find the maximum speed before the car crashes.\nStart low and increase gradually.",
        TuningStage.MinPower => "Find the minimum speed before the car stalls.\nDecrease until the car stops, then increase slightly.",
        _ => ""
    };

    /// <summary>
    /// Creates a new car tuning view model.
    /// </summary>
    /// <param name="carViewModel">The car to tune.</param>
    /// <param name="bleService">The BLE service for track control.</param>
    public CarTuningViewModel(CarViewModel carViewModel, Services.IBleService? bleService)
    {
        _carViewModel = carViewModel;
        _bleService = bleService;

        // Store original values for cancel/restore
        _originalValues = new Car
        {
            DefaultPower = carViewModel.DefaultPower,
            GhostMaxPower = carViewModel.GhostMaxPower,
            MinPower = carViewModel.MinPower
        };

        // Initialize power level from car's current value
        _powerLevel = carViewModel.DefaultPower;

        Log.Information("Car tuning started for {CarName}", CarName);

        // Auto-start power for stage 1 (racing mode - car controlled by throttle)
        _ = SendPowerCommand();
    }

    /// <summary>
    /// Increments the power level by 1.
    /// </summary>
    [RelayCommand]
    private void IncrementPower()
    {
        if (PowerLevel < ScalextricProtocol.MaxPowerLevel)
        {
            PowerLevel++;
        }
    }

    /// <summary>
    /// Decrements the power level by 1.
    /// </summary>
    [RelayCommand]
    private void DecrementPower()
    {
        if (PowerLevel > ScalextricProtocol.MinPowerLevel)
        {
            PowerLevel--;
        }
    }

    /// <summary>
    /// Saves the current stage value and moves to the next stage or completes.
    /// </summary>
    [RelayCommand]
    private async Task SaveAndContinue()
    {
        // Save current stage value to car and move to next stage
        switch (CurrentStage)
        {
            case TuningStage.DefaultPower:
                _carViewModel.DefaultPower = PowerLevel;
                Log.Information("Default power set to {Power} for {CarName}", PowerLevel, CarName);
                // Stop power before changing to ghost mode stage
                await SendStopCommand();
                // Move to Ghost Max Power stage, initialize with current GhostMaxPower
                // Don't send power - wait for user to adjust slider
                _suppressPowerCommand = true;
                PowerLevel = _carViewModel.GhostMaxPower;
                _suppressPowerCommand = false;
                CurrentStage = TuningStage.GhostMaxPower;
                break;

            case TuningStage.GhostMaxPower:
                _carViewModel.GhostMaxPower = PowerLevel;
                Log.Information("Ghost max power set to {Power} for {CarName}", PowerLevel, CarName);
                // Stop power before changing stage
                await SendStopCommand();
                // Move to Min Power stage, initialize with current MinPower
                // Don't send power - wait for user to adjust slider
                _suppressPowerCommand = true;
                PowerLevel = _carViewModel.MinPower;
                _suppressPowerCommand = false;
                CurrentStage = TuningStage.MinPower;
                break;

            case TuningStage.MinPower:
                _carViewModel.MinPower = PowerLevel;
                Log.Information("Min power set to {Power} for {CarName}", PowerLevel, CarName);
                // Stop power and complete
                await SendStopCommand();
                TuningComplete?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    /// <summary>
    /// Cancels tuning and restores original values.
    /// </summary>
    [RelayCommand]
    private async Task Cancel()
    {
        // Stop track power
        await SendStopCommand();

        // Restore original values
        _carViewModel.DefaultPower = _originalValues.DefaultPower;
        _carViewModel.GhostMaxPower = _originalValues.GhostMaxPower;
        _carViewModel.MinPower = _originalValues.MinPower;

        Log.Information("Car tuning cancelled for {CarName}", CarName);
        TuningCancelled?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Called when power level changes - update track power.
    /// </summary>
    partial void OnPowerLevelChanged(int value)
    {
        if (!_suppressPowerCommand)
        {
            _ = SendPowerCommand();
        }
    }

    /// <summary>
    /// Called when selected slot changes - update track power.
    /// </summary>
    partial void OnSelectedSlotChanged(int value)
    {
        _ = SendPowerCommand();
    }

    /// <summary>
    /// Sends power command to enable the selected slot at current power level.
    /// Stage 1 uses racing mode (throttle controls car, power level is the limit).
    /// Stages 2-3 use ghost mode (slider directly controls car speed).
    /// </summary>
    private async Task SendPowerCommand()
    {
        if (_bleService == null) return;

        var builder = new ScalextricProtocol.CommandBuilder
        {
            Type = ScalextricProtocol.CommandType.PowerOnRacing
        };

        // Set all slots to 0 except the selected one
        for (int slot = 1; slot <= 6; slot++)
        {
            if (slot == SelectedSlot)
            {
                var slotPower = builder.GetSlot(slot);
                slotPower.PowerMultiplier = (byte)PowerLevel;

                if (IsGhostModeStage)
                {
                    // Ghost mode: slider directly controls speed
                    slotPower.GhostMode = true;
                }
            }
            else
            {
                builder.SetSlotPower(slot, 0);
            }
        }

        var command = builder.Build();
        await _bleService.WriteCharacteristicAsync(ScalextricProtocol.Characteristics.Command, command);

        Log.Debug("Tuning: Sent {Mode} command for slot {Slot} at power {Power}",
            IsGhostModeStage ? "ghost" : "racing", SelectedSlot, PowerLevel);
    }

    /// <summary>
    /// Sends command to stop the car (zero power to selected slot).
    /// </summary>
    private async Task SendStopCommand()
    {
        if (_bleService == null) return;

        var builder = new ScalextricProtocol.CommandBuilder
        {
            Type = ScalextricProtocol.CommandType.PowerOnRacing
        };

        // Set all slots to 0
        builder.SetAllPower(0);

        var command = builder.Build();
        await _bleService.WriteCharacteristicAsync(ScalextricProtocol.Characteristics.Command, command);

        Log.Debug("Tuning: Sent stop command");
    }

    /// <summary>
    /// Called when the window is closing - ensure power is off.
    /// </summary>
    public async Task OnClosing()
    {
        await SendStopCommand();
    }
}
