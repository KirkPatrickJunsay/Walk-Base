using WalkBase.Models;

namespace WalkBase.Services;

/// <summary>A quest plus the player's current standing against it (pure, no I/O).</summary>
public sealed record QuestProgress(Quest Quest, long Current, bool IsClaimed)
{
    public bool IsComplete => Current >= Quest.Target;
    public bool IsClaimable => IsComplete && !IsClaimed;
    public double Fraction => Quest.Target <= 0 ? 1 : Math.Clamp(Current / (double)Quest.Target, 0, 1);
}

/// <summary>Pure progress math for the milestone quests. Unit-tested; no storage.</summary>
public static class QuestEvaluator
{
    public static long MetricValue(QuestMetric metric, QuestMetrics m) => metric switch
    {
        QuestMetric.LifetimeSteps => m.LifetimeSteps,
        QuestMetric.BuildingCount => m.BuildingCount,
        QuestMetric.DistinctBuildingTypes => m.DistinctBuildingTypes,
        QuestMetric.BaseSize => m.BaseSize,
        QuestMetric.BestStreak => m.BestStreak,
        QuestMetric.DaysWalked => m.DaysWalked,
        _ => 0,
    };

    public static long CurrentValue(Quest q, QuestMetrics m) => MetricValue(q.Metric, m);

    public static IReadOnlyList<QuestProgress> Evaluate(QuestMetrics m, ISet<string> claimedIds) =>
        QuestCatalog.All
            .Select(q => new QuestProgress(q, CurrentValue(q, m), claimedIds.Contains(q.Id)))
            .ToList();

    /// <summary>How many quests are complete but not yet claimed (drives the "Goals" badge).</summary>
    public static int ClaimableCount(QuestMetrics m, ISet<string> claimedIds) =>
        Evaluate(m, claimedIds).Count(p => p.IsClaimable);

    /// <summary>Parse the CSV of claimed quest ids stored on the player state.</summary>
    public static HashSet<string> ParseClaimed(string? csv) =>
        (csv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
}
