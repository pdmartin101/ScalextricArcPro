using ScalextricRace.Models;

namespace ScalextricRace.Services;

/// <summary>
/// Handles persistence of race data to JSON file.
/// Stored in %LocalAppData%/ScalextricPdm/ScalextricRace/races.json
/// </summary>
public class RaceStorage : JsonStorageBase<Race>, IRaceStorage
{
    /// <inheritdoc />
    protected override string FileName => "races.json";

    /// <inheritdoc />
    protected override string EntityName => "races";

    /// <inheritdoc />
    protected override void ValidateItems(List<Race> items)
    {
        foreach (var race in items)
        {
            ValidateStage(race.FreePractice);
            ValidateStage(race.Qualifying);
            ValidateStage(race.RaceStage);
        }
    }

    private static void ValidateStage(RaceStage stage)
    {
        stage.LapCount = Math.Max(1, stage.LapCount);
        stage.TimeMinutes = Math.Max(1, stage.TimeMinutes);
    }
}
