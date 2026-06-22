using WalkBase.Models;

namespace WalkBase.Services;

/// <summary>A badge plus whether it's earned and current progress toward it.</summary>
public sealed record BadgeStatus(Badge Badge, bool Earned, long Current)
{
    public double Fraction => Badge.Threshold <= 0 ? 1 : Math.Clamp(Current / (double)Badge.Threshold, 0, 1);
}

/// <summary>Pure achievement evaluation from the player's metric snapshot (no I/O, unit-tested).</summary>
public static class BadgeEvaluator
{
    public static IReadOnlyList<BadgeStatus> Evaluate(QuestMetrics m) =>
        BadgeCatalog.All
            .Select(b =>
            {
                long cur = QuestEvaluator.MetricValue(b.Metric, m);
                return new BadgeStatus(b, cur >= b.Threshold, cur);
            })
            .ToList();

    public static int EarnedCount(QuestMetrics m) => Evaluate(m).Count(s => s.Earned);
}
