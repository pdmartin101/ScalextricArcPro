using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScalextricRace.Models;
using ScalextricRace.Services;
using Serilog;

namespace ScalextricRace.ViewModels;

/// <summary>
/// Manages the collection of race configurations and race-related operations.
/// Handles race CRUD operations and image management.
/// </summary>
public partial class RaceManagementViewModel : ObservableObject
{
    private readonly IRaceStorage _raceStorage;
    private readonly IWindowService _windowService;
    private bool _isInitializing = true;

    // Callbacks for actions that affect MainViewModel state
    private Action<RaceViewModel>? _onStartRequested;

    /// <summary>
    /// Collection of all race configurations.
    /// </summary>
    public ObservableCollection<RaceViewModel> Races { get; } = [];

    /// <summary>
    /// The currently selected race for editing.
    /// </summary>
    [ObservableProperty]
    private RaceViewModel? _selectedRace;

    /// <summary>
    /// Initializes a new instance of the RaceManagementViewModel.
    /// </summary>
    /// <param name="raceStorage">The race storage service.</param>
    /// <param name="windowService">The window service for dialogs.</param>
    public RaceManagementViewModel(
        IRaceStorage raceStorage,
        IWindowService windowService)
    {
        _raceStorage = raceStorage;
        _windowService = windowService;

        LoadRaces();
        _isInitializing = false;
    }

    /// <summary>
    /// Sets the callback for when a race start is requested.
    /// This is needed because starting a race affects MainViewModel's AppMode.
    /// </summary>
    public void SetStartRequestedCallback(Action<RaceViewModel> callback)
    {
        _onStartRequested = callback;
    }

    /// <summary>
    /// Adds a new race configuration.
    /// </summary>
    [RelayCommand]
    private void AddRace()
    {
        var newRace = new Race { Name = $"Race {Races.Count + 1}" };
        var viewModel = new RaceViewModel(newRace, isDefault: false);
        viewModel.DeleteRequested += OnRaceDeleteRequested;
        viewModel.Changed += OnRaceChanged;
        viewModel.ImageChangeRequested += OnRaceImageChangeRequested;
        viewModel.EditRequested += OnRaceEditRequested;
        viewModel.StartRequested += OnRaceStartRequested;
        Races.Add(viewModel);
        SelectedRace = viewModel;
        Log.Information("Added new race: {RaceName}", newRace.Name);
        SaveRaces();
    }

    /// <summary>
    /// Handles delete request from a race view model.
    /// </summary>
    private void OnRaceDeleteRequested(object? sender, EventArgs e)
    {
        if (sender is RaceViewModel race)
        {
            DeleteRace(race);
        }
    }

    /// <summary>
    /// Handles property change on a race view model.
    /// </summary>
    private void OnRaceChanged(object? sender, EventArgs e)
    {
        SaveRaces();
    }

    /// <summary>
    /// Handles image change request from a race view model.
    /// Opens a file picker and copies the image via the window service.
    /// </summary>
    private async void OnRaceImageChangeRequested(object? sender, EventArgs e)
    {
        if (sender is RaceViewModel race)
        {
            Log.Information("Image change requested for race: {RaceName}", race.Name);
            var imagePath = await _windowService.PickAndCopyImageAsync("Select Race Image", race.Id);
            if (imagePath != null)
            {
                race.ImagePath = imagePath;
                SaveRaces();
            }
        }
    }

    /// <summary>
    /// Handles edit request from a race view model.
    /// Opens the race config editing window.
    /// </summary>
    private async void OnRaceEditRequested(object? sender, EventArgs e)
    {
        if (sender is RaceViewModel race)
        {
            Log.Information("Edit requested for race: {RaceName}", race.Name);
            await _windowService.ShowRaceConfigDialogAsync(race);
            SaveRaces();
        }
    }

    /// <summary>
    /// Handles start request from a race view model.
    /// Delegates to MainViewModel via callback.
    /// </summary>
    private void OnRaceStartRequested(object? sender, EventArgs e)
    {
        if (sender is RaceViewModel race)
        {
            Log.Information("Start requested for race: {RaceName}", race.Name);
            _onStartRequested?.Invoke(race);
        }
    }

    /// <summary>
    /// Deletes the specified race (cannot delete the default race).
    /// </summary>
    /// <param name="race">The race view model to delete.</param>
    private void DeleteRace(RaceViewModel? race)
    {
        if (race == null || race.IsDefault)
        {
            Log.Warning("Cannot delete null or default race");
            return;
        }

        race.DeleteRequested -= OnRaceDeleteRequested;
        race.Changed -= OnRaceChanged;
        race.ImageChangeRequested -= OnRaceImageChangeRequested;
        race.EditRequested -= OnRaceEditRequested;
        race.StartRequested -= OnRaceStartRequested;
        Races.Remove(race);
        if (SelectedRace == race)
        {
            SelectedRace = null;
        }
        Log.Information("Deleted race: {RaceName}", race.Name);
        SaveRaces();
    }

    /// <summary>
    /// Loads races from storage.
    /// Ensures the default race is always present.
    /// </summary>
    private void LoadRaces()
    {
        var storedRaces = _raceStorage.Load();

        // Check if default race exists in storage
        var hasDefaultRace = storedRaces.Any(r => r.Id == Race.DefaultRaceId);

        if (!hasDefaultRace)
        {
            // Create default race if not in storage
            var defaultRace = Race.CreateDefault();
            storedRaces.Insert(0, defaultRace);
        }

        // Create view models for all races
        foreach (var race in storedRaces)
        {
            var isDefault = race.Id == Race.DefaultRaceId;
            var viewModel = new RaceViewModel(race, isDefault);
            viewModel.DeleteRequested += OnRaceDeleteRequested;
            viewModel.Changed += OnRaceChanged;
            viewModel.ImageChangeRequested += OnRaceImageChangeRequested;
            viewModel.EditRequested += OnRaceEditRequested;
            viewModel.StartRequested += OnRaceStartRequested;
            Races.Add(viewModel);
        }

        Log.Information("Loaded {Count} races", Races.Count);
    }

    /// <summary>
    /// Saves all races to storage.
    /// </summary>
    public void SaveRaces()
    {
        if (_isInitializing) return;

        var races = Races.Select(vm => vm.GetModel());
        _raceStorage.Save(races);
    }
}
