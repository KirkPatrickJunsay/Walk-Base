namespace WalkBase.Models;

/// <summary>What a quest measures progress against.</summary>
public enum QuestMetric
{
    LifetimeSteps,
    BuildingCount,
    DistinctBuildingTypes,
    BaseSize,
    BestStreak,
    DaysWalked,
}

/// <summary>A milestone the player works toward; completing it grants <see cref="Reward"/> Bricks.</summary>
public record Quest
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required QuestMetric Metric { get; init; }
    public required long Target { get; init; }
    public required long Reward { get; init; }
}

/// <summary>A snapshot of the player's progress numbers, fed to the pure quest evaluator.</summary>
public readonly record struct QuestMetrics(
    long LifetimeSteps,
    int BuildingCount,
    int DistinctBuildingTypes,
    int BaseSize,
    int BestStreak,
    int DaysWalked);
