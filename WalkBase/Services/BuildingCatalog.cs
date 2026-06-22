using WalkBase.Models;

namespace WalkBase.Services;

/// <summary>
/// The complete, code-defined set of buildings and their progression (spec §10).
/// Sprite keys are semantic filenames — drop matching PNGs (exported/renamed from
/// the Kenney "Isometric Tiles" packs, spec §9) into Resources/Raw/Sprites/ and
/// they light up automatically. Until then the renderer draws colored placeholders.
/// </summary>
public static class BuildingCatalog
{
    public const string TownHallId = "town_hall";

    public static readonly IReadOnlyList<Building> All = new List<Building>
    {
        new() { Id = TownHallId, Name = "Town Hall", Tier = 1,
                BaseCost = 0, UpgradeCostL2 = 800, UpgradeCostL3 = 2_500,
                SpriteKeyL1 = "town_hall_l1.png", SpriteKeyL2 = "town_hall_l2.png", SpriteKeyL3 = "town_hall_l3.png",
                UnlockRequirement = null, IsUnique = true },

        new() { Id = "tent", Name = "Tent", Tier = 1,
                BaseCost = 300, UpgradeCostL2 = 600, UpgradeCostL3 = 1_500,
                SpriteKeyL1 = "tent_l1.png", SpriteKeyL2 = "tent_l2.png", SpriteKeyL3 = "tent_l3.png",
                UnlockRequirement = null },

        new() { Id = "path", Name = "Path", Tier = 1,
                BaseCost = 100, UpgradeCostL2 = null, UpgradeCostL3 = null,
                SpriteKeyL1 = "path_l1.png",
                UnlockRequirement = null },

        new() { Id = "garden", Name = "Garden", Tier = 1,
                BaseCost = 250, UpgradeCostL2 = 500, UpgradeCostL3 = null,
                SpriteKeyL1 = "garden_l1.png", SpriteKeyL2 = "garden_l2.png",
                UnlockRequirement = null },

        // Decorations — cheap Brick sinks, no upgrades.
        new() { Id = "hedge", Name = "Hedge", Tier = 1,
                BaseCost = 50, SpriteKeyL1 = "hedge_l1.png", UnlockRequirement = null },

        new() { Id = "flower_bed", Name = "Flower Bed", Tier = 1,
                BaseCost = 70, SpriteKeyL1 = "flower_bed_l1.png", UnlockRequirement = null },

        new() { Id = "lamppost", Name = "Lamppost", Tier = 1,
                BaseCost = 90, SpriteKeyL1 = "lamppost_l1.png", UnlockRequirement = null },

        new() { Id = "pond", Name = "Pond", Tier = 1,
                BaseCost = 400, SpriteKeyL1 = "pond_l1.png", UnlockRequirement = null },

        new() { Id = "cottage", Name = "Cottage", Tier = 2,
                BaseCost = 800, UpgradeCostL2 = 1_600, UpgradeCostL3 = 3_200,
                SpriteKeyL1 = "cottage_l1.png", SpriteKeyL2 = "cottage_l2.png", SpriteKeyL3 = "cottage_l3.png",
                UnlockRequirement = TownHallId },

        new() { Id = "monument", Name = "Monument", Tier = 2,
                BaseCost = 1_200, SpriteKeyL1 = "monument_l1.png",
                UnlockRequirement = TownHallId, IsUnique = true },

        new() { Id = "house", Name = "House", Tier = 2,
                BaseCost = 1_500, UpgradeCostL2 = 3_000, UpgradeCostL3 = 6_000,
                SpriteKeyL1 = "house_l1.png", SpriteKeyL2 = "house_l2.png", SpriteKeyL3 = "house_l3.png",
                UnlockRequirement = TownHallId },

        new() { Id = "well", Name = "Well", Tier = 2,
                BaseCost = 1_200, UpgradeCostL2 = 2_400, UpgradeCostL3 = null,
                SpriteKeyL1 = "well_l1.png", SpriteKeyL2 = "well_l2.png",
                UnlockRequirement = TownHallId },

        new() { Id = "fountain", Name = "Fountain", Tier = 2,
                BaseCost = 1_800, UpgradeCostL2 = 3_600, UpgradeCostL3 = null,
                SpriteKeyL1 = "fountain_l1.png", SpriteKeyL2 = "fountain_l2.png",
                UnlockRequirement = TownHallId, IsUnique = true },

        new() { Id = "bakery", Name = "Bakery", Tier = 2,
                BaseCost = 2_200, UpgradeCostL2 = 4_400, UpgradeCostL3 = 8_000,
                SpriteKeyL1 = "bakery_l1.png", SpriteKeyL2 = "bakery_l2.png", SpriteKeyL3 = "bakery_l3.png",
                UnlockRequirement = "house" },

        new() { Id = "farm", Name = "Farm", Tier = 2,
                BaseCost = 2_500, UpgradeCostL2 = 5_000, UpgradeCostL3 = 9_000,
                SpriteKeyL1 = "farm_l1.png", SpriteKeyL2 = "farm_l2.png", SpriteKeyL3 = "farm_l3.png",
                UnlockRequirement = "house" },

        new() { Id = "cafe", Name = "Café", Tier = 2,
                BaseCost = 2_000, UpgradeCostL2 = 4_000, UpgradeCostL3 = null,
                SpriteKeyL1 = "cafe_l1.png", SpriteKeyL2 = "cafe_l2.png",
                UnlockRequirement = "house" },

        new() { Id = "chapel", Name = "Chapel", Tier = 2,
                BaseCost = 3_500, UpgradeCostL2 = 7_000, UpgradeCostL3 = null,
                SpriteKeyL1 = "chapel_l1.png", SpriteKeyL2 = "chapel_l2.png",
                UnlockRequirement = "house", IsUnique = true },

        new() { Id = "workshop", Name = "Workshop", Tier = 3,
                BaseCost = 8_000, UpgradeCostL2 = 15_000, UpgradeCostL3 = 28_000,
                SpriteKeyL1 = "workshop_l1.png", SpriteKeyL2 = "workshop_l2.png", SpriteKeyL3 = "workshop_l3.png",
                UnlockRequirement = "farm", IsUnique = true },

        new() { Id = "watchtower", Name = "Watchtower", Tier = 3,
                BaseCost = 10_000, UpgradeCostL2 = 20_000, UpgradeCostL3 = 35_000,
                SpriteKeyL1 = "watchtower_l1.png", SpriteKeyL2 = "watchtower_l2.png", SpriteKeyL3 = "watchtower_l3.png",
                UnlockRequirement = "workshop", IsUnique = true },

        new() { Id = "market", Name = "Market", Tier = 3,
                BaseCost = 12_000, UpgradeCostL2 = 22_000, UpgradeCostL3 = 40_000,
                SpriteKeyL1 = "market_l1.png", SpriteKeyL2 = "market_l2.png", SpriteKeyL3 = "market_l3.png",
                UnlockRequirement = "workshop", IsUnique = true },

        new() { Id = "library", Name = "Library", Tier = 3,
                BaseCost = 9_000, UpgradeCostL2 = 18_000, UpgradeCostL3 = 32_000,
                SpriteKeyL1 = "library_l1.png", SpriteKeyL2 = "library_l2.png", SpriteKeyL3 = "library_l3.png",
                UnlockRequirement = "workshop", IsUnique = true },

        new() { Id = "manor", Name = "Manor", Tier = 3,
                BaseCost = 14_000, UpgradeCostL2 = 26_000, UpgradeCostL3 = 45_000,
                SpriteKeyL1 = "manor_l1.png", SpriteKeyL2 = "manor_l2.png", SpriteKeyL3 = "manor_l3.png",
                UnlockRequirement = "library", IsUnique = true },
    };

    private static readonly Dictionary<string, Building> ById =
        All.ToDictionary(b => b.Id, StringComparer.Ordinal);

    public static Building? Find(string id) => ById.GetValueOrDefault(id);
}
