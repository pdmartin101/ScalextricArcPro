using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScalextricRace.Models;

namespace ScalextricRace.ViewModels;

/// <summary>
/// View model for race configuration editing and display.
/// Wraps a Race model and provides observable properties for data binding.
/// </summary>
public partial class RaceViewModel : ObservableObject
{
    private readonly Race _race;

    /// <summary>
    /// Callback invoked when deletion is requested for this race.
    /// </summary>
    public Action<RaceViewModel>? OnDeleteRequested { get; set; }

    /// <summary>
    /// Callback invoked when any race property changes (for auto-save).
    /// </summary>
    public Action<RaceViewModel>? OnChanged { get; set; }

    /// <summary>
    /// Callback invoked when image selection is requested for this race.
    /// </summary>
    public Action<RaceViewModel>? OnImageChangeRequested { get; set; }

    /// <summary>
    /// Callback invoked when edit is requested for this race.
    /// </summary>
    public Action<RaceViewModel>? OnEditRequested { get; set; }

    /// <summary>
    /// Callback invoked when start is requested for this race.
    /// </summary>
    public Action<RaceViewModel>? OnStartRequested { get; set; }

    /// <summary>
    /// Gets whether this is the default race (cannot be deleted).
    /// </summary>
    public bool IsDefault { get; }

    /// <summary>
    /// Gets whether this race can be deleted (not the default race).
    /// </summary>
    public bool CanDelete => !IsDefault;

    /// <summary>
    /// Gets the unique identifier for the race.
    /// </summary>
    public Guid Id => _race.Id;

    /// <summary>
    /// Display name for the race.
    /// </summary>
    [ObservableProperty]
    private string _name;

    /// <summary>
    /// Optional path to race image for UI display.
    /// Use ImagePathToBitmapConverter in XAML to convert to Bitmap.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private string? _imagePath;

    /// <summary>
    /// Gets whether this race has an image set.
    /// </summary>
    public bool HasImage => !string.IsNullOrEmpty(ImagePath);

    // Free Practice stage properties
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FreePracticeDisplay))]
    [NotifyPropertyChangedFor(nameof(StagesSummary))]
    private bool _freePracticeEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FreePracticeDisplay))]
    [NotifyPropertyChangedFor(nameof(StagesSummary))]
    private RaceStageMode _freePracticeMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FreePracticeDisplay))]
    [NotifyPropertyChangedFor(nameof(StagesSummary))]
    private int _freePracticeLapCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FreePracticeDisplay))]
    [NotifyPropertyChangedFor(nameof(StagesSummary))]
    private int _freePracticeTimeMinutes;

    /// <summary>
    /// Gets the display string for the Free Practice stage.
    /// </summary>
    public string FreePracticeDisplay => GetStageDisplay(FreePracticeMode, FreePracticeLapCount, FreePracticeTimeMinutes);

    // Qualifying stage properties
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QualifyingDisplay))]
    [NotifyPropertyChangedFor(nameof(StagesSummary))]
    private bool _qualifyingEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QualifyingDisplay))]
    [NotifyPropertyChangedFor(nameof(StagesSummary))]
    private RaceStageMode _qualifyingMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QualifyingDisplay))]
    [NotifyPropertyChangedFor(nameof(StagesSummary))]
    private int _qualifyingLapCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QualifyingDisplay))]
    [NotifyPropertyChangedFor(nameof(StagesSummary))]
    private int _qualifyingTimeMinutes;

    /// <summary>
    /// Gets the display string for the Qualifying stage.
    /// </summary>
    public string QualifyingDisplay => GetStageDisplay(QualifyingMode, QualifyingLapCount, QualifyingTimeMinutes);

    // Race stage properties
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RaceStageDisplay))]
    [NotifyPropertyChangedFor(nameof(StagesSummary))]
    private bool _raceStageEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RaceStageDisplay))]
    [NotifyPropertyChangedFor(nameof(StagesSummary))]
    private RaceStageMode _raceStageMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RaceStageDisplay))]
    [NotifyPropertyChangedFor(nameof(StagesSummary))]
    private int _raceStageLapCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RaceStageDisplay))]
    [NotifyPropertyChangedFor(nameof(StagesSummary))]
    private int _raceStageTimeMinutes;

    /// <summary>
    /// Gets the display string for the Race stage.
    /// </summary>
    public string RaceStageDisplay => GetStageDisplay(RaceStageMode, RaceStageLapCount, RaceStageTimeMinutes);

    /// <summary>
    /// Default power level (0-63) for entries without a car/driver configured.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StagesSummary))]
    private int _defaultPower;

    /// <summary>
    /// Gets a summary of the enabled stages for card display.
    /// </summary>
    public string StagesSummary
    {
        get
        {
            var stages = new List<string>();
            if (FreePracticeEnabled) stages.Add($"FP: {FreePracticeDisplay}");
            if (QualifyingEnabled) stages.Add($"Q: {QualifyingDisplay}");
            if (RaceStageEnabled) stages.Add($"R: {RaceStageDisplay}");
            var stagesPart = stages.Count > 0 ? string.Join(" | ", stages) : "No stages enabled";
            return $"{stagesPart} | Power: {DefaultPower}";
        }
    }

    /// <summary>
    /// Creates a new RaceViewModel wrapping the specified race.
    /// </summary>
    /// <param name="race">The race model to wrap.</param>
    /// <param name="isDefault">Whether this is the default race (non-deletable).</param>
    public RaceViewModel(Race race, bool isDefault = false)
    {
        _race = race;
        IsDefault = isDefault;

        // Initialize from model
        _name = race.Name;
        _imagePath = race.ImagePath;

        // Free Practice
        _freePracticeEnabled = race.FreePractice.Enabled;
        _freePracticeMode = race.FreePractice.Mode;
        _freePracticeLapCount = race.FreePractice.LapCount;
        _freePracticeTimeMinutes = race.FreePractice.TimeMinutes;

        // Qualifying
        _qualifyingEnabled = race.Qualifying.Enabled;
        _qualifyingMode = race.Qualifying.Mode;
        _qualifyingLapCount = race.Qualifying.LapCount;
        _qualifyingTimeMinutes = race.Qualifying.TimeMinutes;

        // Race
        _raceStageEnabled = race.RaceStage.Enabled;
        _raceStageMode = race.RaceStage.Mode;
        _raceStageLapCount = race.RaceStage.LapCount;
        _raceStageTimeMinutes = race.RaceStage.TimeMinutes;

        // Default power
        _defaultPower = race.DefaultPower;
    }

    /// <summary>
    /// Gets the underlying Race model.
    /// </summary>
    public Race GetModel() => _race;

    private static string GetStageDisplay(RaceStageMode mode, int laps, int minutes)
    {
        return mode == RaceStageMode.Laps
            ? $"{laps} lap{(laps == 1 ? "" : "s")}"
            : $"{minutes} min";
    }

    // Sync changes back to model and notify via callback
    partial void OnNameChanged(string value)
    {
        _race.Name = value;
        OnChanged?.Invoke(this);
    }

    partial void OnImagePathChanged(string? value)
    {
        _race.ImagePath = value;
        OnChanged?.Invoke(this);
    }

    partial void OnDefaultPowerChanged(int value)
    {
        _race.DefaultPower = Math.Clamp(value, 0, 63);
        OnChanged?.Invoke(this);
    }

    // Free Practice sync
    partial void OnFreePracticeEnabledChanged(bool value)
    {
        _race.FreePractice.Enabled = value;
        OnChanged?.Invoke(this);
    }

    partial void OnFreePracticeModeChanged(RaceStageMode value)
    {
        _race.FreePractice.Mode = value;
        OnChanged?.Invoke(this);
    }

    partial void OnFreePracticeLapCountChanged(int value)
    {
        _race.FreePractice.LapCount = Math.Max(1, value);
        OnChanged?.Invoke(this);
    }

    partial void OnFreePracticeTimeMinutesChanged(int value)
    {
        _race.FreePractice.TimeMinutes = Math.Max(1, value);
        OnChanged?.Invoke(this);
    }

    // Qualifying sync
    partial void OnQualifyingEnabledChanged(bool value)
    {
        _race.Qualifying.Enabled = value;
        OnChanged?.Invoke(this);
    }

    partial void OnQualifyingModeChanged(RaceStageMode value)
    {
        _race.Qualifying.Mode = value;
        OnChanged?.Invoke(this);
    }

    partial void OnQualifyingLapCountChanged(int value)
    {
        _race.Qualifying.LapCount = Math.Max(1, value);
        OnChanged?.Invoke(this);
    }

    partial void OnQualifyingTimeMinutesChanged(int value)
    {
        _race.Qualifying.TimeMinutes = Math.Max(1, value);
        OnChanged?.Invoke(this);
    }

    // Race stage sync
    partial void OnRaceStageEnabledChanged(bool value)
    {
        _race.RaceStage.Enabled = value;
        OnChanged?.Invoke(this);
    }

    partial void OnRaceStageModeChanged(RaceStageMode value)
    {
        _race.RaceStage.Mode = value;
        OnChanged?.Invoke(this);
    }

    partial void OnRaceStageLapCountChanged(int value)
    {
        _race.RaceStage.LapCount = Math.Max(1, value);
        OnChanged?.Invoke(this);
    }

    partial void OnRaceStageTimeMinutesChanged(int value)
    {
        _race.RaceStage.TimeMinutes = Math.Max(1, value);
        OnChanged?.Invoke(this);
    }

    /// <summary>
    /// Requests deletion of this race.
    /// </summary>
    [RelayCommand]
    private void RequestDelete()
    {
        if (!IsDefault)
        {
            OnDeleteRequested?.Invoke(this);
        }
    }

    /// <summary>
    /// Requests image change for this race.
    /// </summary>
    [RelayCommand]
    private void RequestImageChange()
    {
        OnImageChangeRequested?.Invoke(this);
    }

    /// <summary>
    /// Requests edit for this race.
    /// </summary>
    [RelayCommand]
    private void RequestEdit()
    {
        OnEditRequested?.Invoke(this);
    }

    /// <summary>
    /// Requests to start this race.
    /// </summary>
    [RelayCommand]
    private void RequestStart()
    {
        OnStartRequested?.Invoke(this);
    }

    // Free Practice commands
    [RelayCommand]
    private void ToggleFreePracticeMode()
    {
        FreePracticeMode = FreePracticeMode == RaceStageMode.Laps ? RaceStageMode.Time : RaceStageMode.Laps;
    }

    [RelayCommand]
    private void IncrementFreePractice()
    {
        if (FreePracticeMode == RaceStageMode.Laps)
            FreePracticeLapCount++;
        else
            FreePracticeTimeMinutes++;
    }

    [RelayCommand]
    private void DecrementFreePractice()
    {
        if (FreePracticeMode == RaceStageMode.Laps)
            FreePracticeLapCount = Math.Max(1, FreePracticeLapCount - 1);
        else
            FreePracticeTimeMinutes = Math.Max(1, FreePracticeTimeMinutes - 1);
    }

    // Qualifying commands
    [RelayCommand]
    private void ToggleQualifyingMode()
    {
        QualifyingMode = QualifyingMode == RaceStageMode.Laps ? RaceStageMode.Time : RaceStageMode.Laps;
    }

    [RelayCommand]
    private void IncrementQualifying()
    {
        if (QualifyingMode == RaceStageMode.Laps)
            QualifyingLapCount++;
        else
            QualifyingTimeMinutes++;
    }

    [RelayCommand]
    private void DecrementQualifying()
    {
        if (QualifyingMode == RaceStageMode.Laps)
            QualifyingLapCount = Math.Max(1, QualifyingLapCount - 1);
        else
            QualifyingTimeMinutes = Math.Max(1, QualifyingTimeMinutes - 1);
    }

    // Race stage commands
    [RelayCommand]
    private void ToggleRaceStageMode()
    {
        RaceStageMode = RaceStageMode == RaceStageMode.Laps ? RaceStageMode.Time : RaceStageMode.Laps;
    }

    [RelayCommand]
    private void IncrementRaceStage()
    {
        if (RaceStageMode == RaceStageMode.Laps)
            RaceStageLapCount++;
        else
            RaceStageTimeMinutes++;
    }

    [RelayCommand]
    private void DecrementRaceStage()
    {
        if (RaceStageMode == RaceStageMode.Laps)
            RaceStageLapCount = Math.Max(1, RaceStageLapCount - 1);
        else
            RaceStageTimeMinutes = Math.Max(1, RaceStageTimeMinutes - 1);
    }
}
