using WalkBase.Services;

namespace WalkBase.Tests;

public class NotificationPlannerTests
{
    // Convenience builder with sensible defaults (goal 8000, nothing sent yet).
    private static NudgeContext Ctx(long steps, int hour, int streak = 0, bool goalReached = false,
        bool remindersOn = true, bool nearSent = false, bool streakSent = false, bool reminderSent = false,
        bool goalSent = false) =>
        new(remindersOn, 8000, steps, streak, hour, goalReached, nearSent, streakSent, reminderSent, goalSent);

    [Fact]
    public void NearGoal_FiresAt80Percent_NotYetReached()
    {
        Assert.Equal(NudgeKind.NearGoal, NotificationPlanner.Decide(Ctx(steps: 6400, hour: 14)));
        Assert.Equal(NudgeKind.None, NotificationPlanner.Decide(Ctx(steps: 6399, hour: 14))); // just under 80%
    }

    [Fact]
    public void GoalReached_CelebratesOnce_ThenQuiet()
    {
        // Hitting the goal fires a one-off celebration...
        Assert.Equal(NudgeKind.GoalReached, NotificationPlanner.Decide(Ctx(steps: 8000, hour: 20, streak: 5, goalReached: true)));
        // ...and nothing more once it's been sent, even with a streak in the evening.
        Assert.Equal(NudgeKind.None, NotificationPlanner.Decide(Ctx(steps: 8000, hour: 20, streak: 5, goalReached: true, goalSent: true)));
    }

    [Fact]
    public void NoNudges_WhenDisabled()
    {
        Assert.Equal(NudgeKind.None, NotificationPlanner.Decide(Ctx(steps: 6500, hour: 14, remindersOn: false)));
        Assert.Equal(NudgeKind.None, NotificationPlanner.Decide(Ctx(steps: 8000, hour: 20, goalReached: true, remindersOn: false)));
    }

    [Fact]
    public void StreakAtRisk_OnlyInEvening_WithAStreak()
    {
        Assert.Equal(NudgeKind.StreakAtRisk, NotificationPlanner.Decide(Ctx(steps: 3000, hour: 19, streak: 4)));
        Assert.Equal(NudgeKind.None, NotificationPlanner.Decide(Ctx(steps: 3000, hour: 12, streak: 4))); // too early
        Assert.Equal(NudgeKind.None, NotificationPlanner.Decide(Ctx(steps: 3000, hour: 19, streak: 0))); // no streak
    }

    [Fact]
    public void DailyReminder_Afternoon_WhenBarelyMoving()
    {
        Assert.Equal(NudgeKind.DailyReminder, NotificationPlanner.Decide(Ctx(steps: 1000, hour: 17)));
    }

    [Fact]
    public void EachNudge_FiresOncePerDay()
    {
        Assert.Equal(NudgeKind.None, NotificationPlanner.Decide(Ctx(steps: 6500, hour: 14, nearSent: true)));
        Assert.Equal(NudgeKind.None, NotificationPlanner.Decide(Ctx(steps: 3000, hour: 19, streak: 4, streakSent: true)));
    }
}
