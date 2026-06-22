namespace WalkBase.Services;

/// <summary>Which reminder (if any) to fire right now.</summary>
public enum NudgeKind { None, NearGoal, StreakAtRisk, DailyReminder, GoalReached }

/// <summary>Everything the planner needs to decide on a nudge (pure inputs).</summary>
public readonly record struct NudgeContext(
    bool RemindersEnabled,
    int Goal,
    long StepsToday,
    int CurrentStreak,
    int Hour,
    bool GoalReachedToday,
    bool NearGoalSent,
    bool StreakSent,
    bool ReminderSent,
    bool GoalSent = false);

/// <summary>
/// Pure decision logic for the daily step reminders (no I/O, unit-tested). The Android
/// foreground service calls this each tick, then posts a notification + marks it sent.
/// At most one nudge of each kind per day; none once the goal is reached.
/// </summary>
public static class NotificationPlanner
{
    public const double NearGoalFraction = 0.8;   // ≥80% of goal → "almost there"
    public const double LowActivityFraction = 0.25;
    public const int StreakHour = 18;             // evening streak warning
    public const int ReminderHour = 17;           // afternoon "go for a walk"

    public static NudgeKind Decide(NudgeContext c)
    {
        if (!c.RemindersEnabled || c.Goal <= 0)
            return NudgeKind.None;

        // Goal reached → celebrate once, then stay quiet for the rest of the day.
        if (c.GoalReachedToday)
            return c.GoalSent ? NudgeKind.None : NudgeKind.GoalReached;

        // Closest to converting first: nearly at the goal but not over the line.
        if (!c.NearGoalSent &&
            c.StepsToday >= (long)(c.Goal * NearGoalFraction) && c.StepsToday < c.Goal)
            return NudgeKind.NearGoal;

        // Evening + an active streak about to break.
        if (!c.StreakSent && c.Hour >= StreakHour && c.CurrentStreak > 0)
            return NudgeKind.StreakAtRisk;

        // Afternoon and barely moving → a gentle "time for a walk".
        if (!c.ReminderSent && c.Hour >= ReminderHour &&
            c.StepsToday < (long)(c.Goal * LowActivityFraction))
            return NudgeKind.DailyReminder;

        return NudgeKind.None;
    }
}
