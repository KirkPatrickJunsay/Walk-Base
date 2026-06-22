using WalkBase.Models;
using WalkBase.Services;

namespace WalkBase.Tests;

public class QuestTests
{
    private static QuestMetrics Metrics(long steps = 0, int buildings = 0, int types = 0,
        int baseSize = 6, int bestStreak = 0, int daysWalked = 0) =>
        new(steps, buildings, types, baseSize, bestStreak, daysWalked);

    [Fact]
    public void Quest_IsComplete_WhenMetricMeetsTarget()
    {
        var firstSteps = QuestCatalog.All.First(q => q.Id == "first_steps"); // 5,000 steps
        Assert.Equal(5_000, QuestEvaluator.CurrentValue(firstSteps, Metrics(steps: 5_000)));

        var below = QuestEvaluator.Evaluate(Metrics(steps: 4_999), new HashSet<string>());
        var at = QuestEvaluator.Evaluate(Metrics(steps: 5_000), new HashSet<string>());
        Assert.False(below.First(p => p.Quest.Id == "first_steps").IsComplete);
        Assert.True(at.First(p => p.Quest.Id == "first_steps").IsComplete);
    }

    [Fact]
    public void Claimable_RequiresCompleteAndUnclaimed()
    {
        var m = Metrics(steps: 5_000);

        // Complete + unclaimed → claimable.
        Assert.Equal(1, QuestEvaluator.ClaimableCount(m, new HashSet<string>()));

        // Complete but already claimed → not claimable.
        Assert.Equal(0, QuestEvaluator.ClaimableCount(m, new HashSet<string> { "first_steps" }));
    }

    [Fact]
    public void ParseClaimed_HandlesNullAndWhitespace()
    {
        Assert.Empty(QuestEvaluator.ParseClaimed(null));
        Assert.Empty(QuestEvaluator.ParseClaimed(""));
        var set = QuestEvaluator.ParseClaimed("first_steps, founder ,");
        Assert.Equal(2, set.Count);
        Assert.Contains("founder", set);
    }

    [Fact]
    public void Fraction_IsClampedToOne()
    {
        var p = QuestEvaluator.Evaluate(Metrics(steps: 999_999), new HashSet<string>())
            .First(x => x.Quest.Id == "first_steps");
        Assert.Equal(1.0, p.Fraction);
    }
}
