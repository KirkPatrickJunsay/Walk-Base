using WalkBase.Models;
using WalkBase.Services;

namespace WalkBase.Tests;

public class HappinessTests
{
    private static PlacedBuilding B(string id, int level = 1) =>
        new() { BuildingId = id, Level = level };

    [Fact]
    public void EmptyTown_IsEmpty()
    {
        var h = HappinessCalculator.Compute(Array.Empty<PlacedBuilding>());
        Assert.Equal(0, h.Score);
        Assert.Equal("Empty", h.Label);
    }

    [Fact]
    public void VarietyAndAmenities_RaiseHappiness()
    {
        var sparse = HappinessCalculator.Compute(new[] { B("tent"), B("tent"), B("tent") });
        var varied = HappinessCalculator.Compute(new[] { B("town_hall"), B("garden"), B("fountain"), B("market") });
        Assert.True(varied.Score > sparse.Score);
    }

    [Fact]
    public void Score_IsClampedTo100()
    {
        var many = Enumerable.Range(0, 30).Select(_ => B("fountain")).ToList();
        Assert.Equal(100, HappinessCalculator.Compute(many).Score);
    }

    [Fact]
    public void Upgrades_AddHappiness()
    {
        var l1 = HappinessCalculator.Compute(new[] { B("house", 1) }).Score;
        var l3 = HappinessCalculator.Compute(new[] { B("house", 3) }).Score;
        Assert.True(l3 > l1);
    }
}
