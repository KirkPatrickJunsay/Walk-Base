using WalkBase.Models;
using WalkBase.Services;

namespace WalkBase.Tests;

public class BackupTests
{
    private static BackupData Sample() => new()
    {
        Version = 1,
        ExportedUtc = "2026-06-22T00:00:00Z",
        Player = new PlayerState { Id = 1, LifetimeSteps = 12345, BonusBricks = 300, BaseSize = 7, ClaimedQuests = "founder" },
        Buildings =
        {
            new PlacedBuilding { Id = 1, BuildingId = "town_hall", GridX = 2, GridY = 2, Level = 1 },
            new PlacedBuilding { Id = 2, BuildingId = "house", GridX = 3, GridY = 1, Level = 2 },
        },
        History = { new DailyStepRecord { Date = "2026-06-21", Steps = 8000 } },
        Settings = new BackupSettings { DailyStepGoal = 8000, SoundEnabled = false, HapticsEnabled = true },
    };

    [Fact]
    public void RoundTrip_PreservesEverything()
    {
        var json = BackupSerializer.Serialize(Sample());
        var back = BackupSerializer.TryDeserialize(json);

        Assert.NotNull(back);
        Assert.Equal(12345, back!.Player!.LifetimeSteps);
        Assert.Equal(300, back.Player.BonusBricks);
        Assert.Equal("founder", back.Player.ClaimedQuests);
        Assert.Equal(2, back.Buildings.Count);
        Assert.Equal("house", back.Buildings[1].BuildingId);
        Assert.Single(back.History);
        Assert.Equal(8000, back.Settings!.DailyStepGoal);
        Assert.False(back.Settings.SoundEnabled);
    }

    [Fact]
    public void TryDeserialize_RejectsJunkAndIncompleteBackups()
    {
        Assert.Null(BackupSerializer.TryDeserialize("not json at all"));
        Assert.Null(BackupSerializer.TryDeserialize("{}"));            // no Player
        Assert.Null(BackupSerializer.TryDeserialize("{\"Buildings\":[]}"));
    }
}
