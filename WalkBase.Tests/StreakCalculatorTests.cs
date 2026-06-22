using WalkBase.Models;
using WalkBase.Services;

namespace WalkBase.Tests;

public class StreakCalculatorTests
{
    private static DailyStepRecord Day(string date, long steps) => new() { Date = date, Steps = steps };

    [Fact]
    public void NoHistory_NoStreak()
    {
        var (cur, best) = StreakCalculator.Compute(new List<DailyStepRecord>(), "2026-06-22", 0, 6000);
        Assert.Equal(0, cur);
        Assert.Equal(0, best);
    }

    [Fact]
    public void ConsecutiveDays_CountTowardCurrentStreak_IncludingTodayLive()
    {
        var history = new List<DailyStepRecord>
        {
            Day("2026-06-20", 7000),
            Day("2026-06-21", 8000),
        };
        // today's live steps cross the goal too → 3-day streak
        var (cur, _) = StreakCalculator.Compute(history, "2026-06-22", 6500, 6000);
        Assert.Equal(3, cur);
    }

    [Fact]
    public void TodayNotYetMet_StreakHoldsFromYesterday()
    {
        var history = new List<DailyStepRecord>
        {
            Day("2026-06-20", 7000),
            Day("2026-06-21", 8000),
        };
        // today only 100 steps so far — streak should still be 2 (yesterday + day before)
        var (cur, _) = StreakCalculator.Compute(history, "2026-06-22", 100, 6000);
        Assert.Equal(2, cur);
    }

    [Fact]
    public void Gap_BreaksCurrentButBestRemembersLongestRun()
    {
        var history = new List<DailyStepRecord>
        {
            Day("2026-06-17", 9000),
            Day("2026-06-18", 9000),
            Day("2026-06-19", 9000),
            // 06-20 missed
            Day("2026-06-21", 7000),
        };
        var (cur, best) = StreakCalculator.Compute(history, "2026-06-22", 0, 6000);
        Assert.Equal(1, cur);  // only yesterday (06-21) counts now
        Assert.Equal(3, best); // 06-17..06-19
    }

    [Fact]
    public void BelowGoalDays_DoNotCount()
    {
        var history = new List<DailyStepRecord> { Day("2026-06-21", 3000) };
        var (cur, best) = StreakCalculator.Compute(history, "2026-06-22", 0, 6000);
        Assert.Equal(0, cur);
        Assert.Equal(0, best);
    }
}
