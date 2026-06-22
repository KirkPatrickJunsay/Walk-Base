using WalkBase.Models;
using WalkBase.Services;

namespace WalkBase.Tests;

public class BadgeTests
{
    private static QuestMetrics Metrics(long steps = 0, int buildings = 0, int types = 0,
        int baseSize = 6, int bestStreak = 0, int daysWalked = 0) =>
        new(steps, buildings, types, baseSize, bestStreak, daysWalked);

    [Fact]
    public void EarnedExactlyWhenThresholdMet()
    {
        var below = BadgeEvaluator.Evaluate(Metrics(steps: 9_999));
        var at = BadgeEvaluator.Evaluate(Metrics(steps: 10_000));
        Assert.False(below.First(b => b.Badge.Id == "steps_10k").Earned);
        Assert.True(at.First(b => b.Badge.Id == "steps_10k").Earned);
    }

    [Fact]
    public void EarnedCount_CountsAllMetrics()
    {
        // 100k steps → 2 step badges; 5 buildings → 2 build badges; 4 types → 1; size 8 → 1; streak 7 → 2; 7 days → 1
        var m = Metrics(steps: 100_000, buildings: 5, types: 4, baseSize: 8, bestStreak: 7, daysWalked: 7);
        Assert.Equal(9, BadgeEvaluator.EarnedCount(m));
    }

    [Fact]
    public void EmptyPlayer_HasNoBadges()
    {
        Assert.Equal(0, BadgeEvaluator.EarnedCount(Metrics()));
    }

    [Fact]
    public void Fraction_IsClamped()
    {
        var s = BadgeEvaluator.Evaluate(Metrics(steps: 5_000_000)).First(b => b.Badge.Id == "steps_10k");
        Assert.Equal(1.0, s.Fraction);
    }
}
