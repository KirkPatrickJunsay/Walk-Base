using WalkBase.Models;
using WalkBase.Services;

namespace WalkBase.Tests;

public class CurrencyTests
{
    private readonly CurrencyService _c = new();

    [Theory]
    [InlineData(0, 0)]
    [InlineData(9, 0)]
    [InlineData(10, 1)]
    [InlineData(25, 2)]
    [InlineData(2000, 200)] // spec §10: ~2,000 steps ≈ 200 Bricks
    public void TotalBricksEarned_IsStepsOverTen(long steps, long bricks) =>
        Assert.Equal(bricks, _c.TotalBricksEarned(steps));

    [Fact]
    public void Balance_IsEarnedMinusSpent_NeverNegative()
    {
        var state = new PlayerState { LifetimeSteps = 5000, TotalBricksSpent = 300 };
        Assert.Equal(200, _c.Balance(state)); // 500 earned − 300 spent

        state.TotalBricksSpent = 999;
        Assert.Equal(0, _c.Balance(state)); // clamped at 0
    }
}

public class CatalogTests
{
    [Fact]
    public void TownHall_IsFreeAndUnlockedFromStart()
    {
        var th = BuildingCatalog.Find(BuildingCatalog.TownHallId);
        Assert.NotNull(th);
        Assert.Equal(0, th!.BaseCost);
        Assert.Null(th.UnlockRequirement);
    }

    [Fact]
    public void EveryUnlockRequirement_PointsToARealBuilding()
    {
        foreach (var b in BuildingCatalog.All)
        {
            if (b.UnlockRequirement is not null)
                Assert.NotNull(BuildingCatalog.Find(b.UnlockRequirement));
        }
    }

    [Fact]
    public void Ids_AreUnique()
    {
        var ids = BuildingCatalog.All.Select(b => b.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Theory]
    [InlineData("path", 1)]      // no upgrades
    [InlineData("well", 2)]      // L2 only
    [InlineData("house", 3)]     // full L3
    public void MaxLevel_MatchesUpgradeCosts(string id, int expectedMax) =>
        Assert.Equal(expectedMax, BuildingCatalog.Find(id)!.MaxLevel);

    [Fact]
    public void OnlyLandmarks_AreUnique()
    {
        var unique = BuildingCatalog.All.Where(b => b.IsUnique).Select(b => b.Id).OrderBy(x => x);
        Assert.Equal(new[] { "chapel", "fountain", "library", "manor", "market", "monument", "town_hall", "watchtower", "workshop" }, unique);
    }

    [Fact]
    public void UpgradeCostFrom_FollowsTheLadder()
    {
        var house = BuildingCatalog.Find("house")!;
        Assert.Equal(3000, house.UpgradeCostFrom(1));
        Assert.Equal(6000, house.UpgradeCostFrom(2));
        Assert.Null(house.UpgradeCostFrom(3)); // maxed
    }

    [Fact]
    public void InvestedBricks_SumsBaseAndUpgradesPaid()
    {
        var house = BuildingCatalog.Find("house")!; // base 1500, L2 +3000, L3 +6000
        Assert.Equal(1500, house.InvestedBricks(1));
        Assert.Equal(4500, house.InvestedBricks(2));
        Assert.Equal(10500, house.InvestedBricks(3));
    }
}
