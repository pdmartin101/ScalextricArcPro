using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ScalextricRace.Models;
using Serilog;

namespace ScalextricRace.ViewModels;

/// <summary>
/// Manages race configuration including race entries (car/driver pairings) and stage settings.
/// </summary>
public partial class RaceConfigurationViewModel : ObservableObject
{
    /// <summary>
    /// Collection of race entries (car/driver pairings) for the current race.
    /// </summary>
    public ObservableCollection<RaceEntryViewModel> RaceEntries { get; } = [];

    /// <summary>
    /// Gets only the enabled race entries for display.
    /// </summary>
    public IEnumerable<RaceEntryViewModel> EnabledEntries =>
        RaceEntries.Where(e => e.IsEnabled);

    /// <summary>
    /// Gets whether the race can be started (at least one configured entry).
    /// </summary>
    [ObservableProperty]
    private bool _canStartRace;

    // Runtime race settings (overrides from the race template)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigFreePracticeDisplay))]
    private bool _configFreePracticeEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigFreePracticeDisplay))]
    private RaceStageMode _configFreePracticeMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigFreePracticeDisplay))]
    private int _configFreePracticeLapCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigFreePracticeDisplay))]
    private int _configFreePracticeTimeMinutes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigQualifyingDisplay))]
    private bool _configQualifyingEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigQualifyingDisplay))]
    private RaceStageMode _configQualifyingMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigQualifyingDisplay))]
    private int _configQualifyingLapCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigQualifyingDisplay))]
    private int _configQualifyingTimeMinutes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigRaceDisplay))]
    private bool _configRaceEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigRaceDisplay))]
    private RaceStageMode _configRaceMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigRaceDisplay))]
    private int _configRaceLapCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfigRaceDisplay))]
    private int _configRaceTimeMinutes;

    /// <summary>
    /// Default power level (0-63) for entries without a car/driver configured.
    /// </summary>
    [ObservableProperty]
    private int _configDefaultPower = 40;

    /// <summary>
    /// Whether test mode is active (power on for testing controllers).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TestButtonText))]
    private bool _isTestModeActive;

    /// <summary>
    /// Gets the text for the test mode toggle button.
    /// </summary>
    public string TestButtonText => IsTestModeActive ? "Stop Test" : "Test";

    /// <summary>
    /// Gets the display string for Free Practice configuration.
    /// </summary>
    public string ConfigFreePracticeDisplay => GetStageDisplay(ConfigFreePracticeMode, ConfigFreePracticeLapCount, ConfigFreePracticeTimeMinutes);

    /// <summary>
    /// Gets the display string for Qualifying configuration.
    /// </summary>
    public string ConfigQualifyingDisplay => GetStageDisplay(ConfigQualifyingMode, ConfigQualifyingLapCount, ConfigQualifyingTimeMinutes);

    /// <summary>
    /// Gets the display string for Race configuration.
    /// </summary>
    public string ConfigRaceDisplay => GetStageDisplay(ConfigRaceMode, ConfigRaceLapCount, ConfigRaceTimeMinutes);

    private static string GetStageDisplay(RaceStageMode mode, int laps, int minutes)
    {
        return mode == RaceStageMode.Laps
            ? $"{laps} Laps"
            : $"{minutes} Min";
    }

    /// <summary>
    /// Initializes race entries for 6 slots using available cars and drivers.
    /// </summary>
    /// <param name="cars">Available cars for selection.</param>
    /// <param name="drivers">Available drivers for selection.</param>
    public void InitializeRaceEntries(
        ObservableCollection<CarViewModel> cars,
        ObservableCollection<DriverViewModel> drivers)
    {
        if (RaceEntries.Count == 0)
        {
            // Create 6 entries (one per slot)
            for (int i = 1; i <= 6; i++)
            {
                var entry = new RaceEntryViewModel(i, cars, drivers);
                entry.PropertyChanged += OnRaceEntryPropertyChanged;
                entry.RaceDefaultPower = ConfigDefaultPower;
                RaceEntries.Add(entry);
            }
        }
        else
        {
            // Update existing entries' default power (car/driver collections are passed by reference)
            foreach (var entry in RaceEntries)
            {
                entry.RaceDefaultPower = ConfigDefaultPower;
            }
        }
    }

    /// <summary>
    /// Loads stage settings from a race configuration.
    /// </summary>
    /// <param name="race">The race to load settings from.</param>
    public void LoadFromRace(RaceViewModel race)
    {
        ConfigFreePracticeEnabled = race.FreePracticeEnabled;
        ConfigFreePracticeMode = race.FreePracticeMode;
        ConfigFreePracticeLapCount = race.FreePracticeLapCount;
        ConfigFreePracticeTimeMinutes = race.FreePracticeTimeMinutes;

        ConfigQualifyingEnabled = race.QualifyingEnabled;
        ConfigQualifyingMode = race.QualifyingMode;
        ConfigQualifyingLapCount = race.QualifyingLapCount;
        ConfigQualifyingTimeMinutes = race.QualifyingTimeMinutes;

        ConfigRaceEnabled = race.RaceStageEnabled;
        ConfigRaceMode = race.RaceStageMode;
        ConfigRaceLapCount = race.RaceStageLapCount;
        ConfigRaceTimeMinutes = race.RaceStageTimeMinutes;

        ConfigDefaultPower = race.DefaultPower;

        Log.Debug("Loaded race configuration from {RaceName}", race.Name);
    }

    /// <summary>
    /// Loads saved race entries from a race configuration.
    /// </summary>
    /// <param name="race">The race containing saved entries.</param>
    public void LoadSavedEntries(RaceViewModel race)
    {
        var model = race.GetModel();
        foreach (var savedEntry in model.Entries)
        {
            var entryVm = RaceEntries.FirstOrDefault(e => e.SlotNumber == savedEntry.SlotNumber);
            if (entryVm != null)
            {
                entryVm.IsEnabled = savedEntry.IsEnabled;
                // Car and driver are set by MainViewModel since it has access to Cars/Drivers collections
            }
        }
    }

    /// <summary>
    /// Saves the current race entries to the race configuration.
    /// </summary>
    /// <param name="race">The race to save entries to.</param>
    public void SaveRaceEntries(RaceViewModel race)
    {
        var model = race.GetModel();
        model.Entries.Clear();
        foreach (var entry in RaceEntries)
        {
            model.Entries.Add(new RaceEntry
            {
                SlotNumber = entry.SlotNumber,
                IsEnabled = entry.IsEnabled,
                CarId = entry.SelectedCar?.Id,
                DriverId = entry.SelectedDriver?.Id
            });
        }
        Log.Information("Saved race entries for {RaceName}", race.Name);
    }

    /// <summary>
    /// Clears all race entries and unsubscribes from events.
    /// </summary>
    public void ClearRaceEntries()
    {
        // Unsubscribe from property changes
        foreach (var entry in RaceEntries)
        {
            entry.PropertyChanged -= OnRaceEntryPropertyChanged;
        }
        RaceEntries.Clear();
        CanStartRace = false;
    }

    /// <summary>
    /// Updates CanStartRace based on configured entries.
    /// </summary>
    public void UpdateCanStartRace()
    {
        CanStartRace = RaceEntries.Any(e => e.IsConfigured);
    }

    private void OnRaceEntryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RaceEntryViewModel.IsConfigured))
        {
            UpdateCanStartRace();
        }

        if (e.PropertyName == nameof(RaceEntryViewModel.IsEnabled))
        {
            OnPropertyChanged(nameof(EnabledEntries));
        }
    }

    partial void OnConfigDefaultPowerChanged(int value)
    {
        foreach (var entry in RaceEntries)
        {
            entry.RaceDefaultPower = value;
        }
    }
}
