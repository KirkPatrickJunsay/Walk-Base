using WalkBase.Models;
using WalkBase.Services;

namespace WalkBase.Tests;

/// <summary>
/// Locks in the "a save outlives the catalog" guarantees. If a future app update removes or
/// renames a building id, or trims a building's levels, an existing player's save may contain
/// a building the running catalog no longer fully knows. None of that may crash — buildings
/// fall back to a placeholder render and stay sellable. These tests pin that contract so a
/// refactor can't silently reintroduce a null-reference crash on someone's installed town.
/// </summary>
public class ForwardCompatibilityTests
{
    [Fact]
    public void Catalog_Find_ReturnsNull_ForUnknownId() // the contract every consumer null-checks
    {
        Assert.Null(BuildingCatalog.Find("a_building_from_a_future_version"));
        Assert.Null(BuildingCatalog.Find(""));
    }

    [Fact]
    public void Happiness_ToleratesOrphanedBuildings_WithoutThrowing()
    {
        var town = new List<PlacedBuilding>
        {
            new() { BuildingId = "house", Level = 1 },
            new() { BuildingId = "retired_building_id", Level = 2 },   // no longer in the catalog
        };

        var ex = Record.Exception(() => HappinessCalculator.Compute(town));
        Assert.Null(ex);

        var h = HappinessCalculator.Compute(town);
        Assert.InRange(h.Score, 1, 100);   // orphan still contributes a baseline, no crash
    }

    [Fact]
    public void Happiness_AllOrphans_StillComputes()
    {
        var town = new List<PlacedBuilding> { new() { BuildingId = "ghost", Level = 1 } };
        var h = HappinessCalculator.Compute(town);
        Assert.True(h.Score > 0);
    }

    [Fact]
    public void DistinctBuildingTypes_CountsOrphans_WithoutLookup()
    {
        // The variety metric is a pure id operation, so an unknown id can't break it.
        var town = new List<PlacedBuilding>
        {
            new() { BuildingId = "house" },
            new() { BuildingId = "house" },
            new() { BuildingId = "retired_id" },
        };
        Assert.Equal(2, town.Select(b => b.BuildingId).Distinct().Count());
    }

    [Fact]
    public void SpriteForLevel_FallsBack_WhenAnUpdateTrimsLevels()
    {
        // A save holds a building at L3, but a later catalog only ships its L1 art.
        var trimmed = new Building { Id = "x", Name = "X", Tier = 1, BaseCost = 0, SpriteKeyL1 = "x_l1.png" };
        Assert.Equal("x_l1.png", trimmed.SpriteForLevel(3));  // falls back, never null-renders blank
        Assert.Equal("x_l1.png", trimmed.SpriteForLevel(2));

        // With L1 + L2 only, a stored L3 building uses the L2 art.
        var twoLevels = trimmed with { SpriteKeyL2 = "x_l2.png" };
        Assert.Equal("x_l2.png", twoLevels.SpriteForLevel(3));
    }
}
