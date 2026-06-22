namespace WalkBase.Models;

/// <summary>
/// Static catalog definition (NOT a DB table) — see <see cref="Services.BuildingCatalog"/>.
/// Costs and sprite keys per level; <see cref="UnlockRequirement"/> gates availability.
/// </summary>
public record Building
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int Tier { get; init; }

    /// <summary>Cost to place the building (Level 1).</summary>
    public required long BaseCost { get; init; }

    /// <summary>Cost to upgrade to Level 2, or null if the building has no L2.</summary>
    public long? UpgradeCostL2 { get; init; }

    /// <summary>Cost to upgrade to Level 3, or null if the building has no L3.</summary>
    public long? UpgradeCostL3 { get; init; }

    public string? SpriteKeyL1 { get; init; }
    public string? SpriteKeyL2 { get; init; }
    public string? SpriteKeyL3 { get; init; }

    /// <summary>
    /// BuildingId that must already exist on the base before this is buildable.
    /// Null means available from the start. See spec §10.
    /// </summary>
    public string? UnlockRequirement { get; init; }

    /// <summary>If true, only one of this building may be placed on the base (a landmark).</summary>
    public bool IsUnique { get; init; }

    /// <summary>Highest level this building supports (1–3), inferred from upgrade costs.</summary>
    public int MaxLevel => UpgradeCostL3.HasValue ? 3 : UpgradeCostL2.HasValue ? 2 : 1;

    /// <summary>Total Bricks sunk into this building at <paramref name="level"/> (base + upgrades paid).</summary>
    public long InvestedBricks(int level) =>
        BaseCost
        + (level >= 2 ? UpgradeCostL2 ?? 0 : 0)
        + (level >= 3 ? UpgradeCostL3 ?? 0 : 0);

    /// <summary>Cost to go from <paramref name="currentLevel"/> to the next level, or null if maxed.</summary>
    public long? UpgradeCostFrom(int currentLevel) => currentLevel switch
    {
        1 => UpgradeCostL2,
        2 => UpgradeCostL3,
        _ => null,
    };

    /// <summary>Sprite filename for the given level (falls back to lower levels if unset).</summary>
    public string? SpriteForLevel(int level) => level switch
    {
        >= 3 => SpriteKeyL3 ?? SpriteKeyL2 ?? SpriteKeyL1,
        2 => SpriteKeyL2 ?? SpriteKeyL1,
        _ => SpriteKeyL1,
    };
}
