using WalkBase.Models;

namespace WalkBase.Services;

/// <summary>
/// The §7 step reconciliation (baseline seed, delta, reboot reset, daily rollover/archive)
/// shared by the in-app <see cref="StepService"/> and the background tracking service so
/// both credit Bricks identically.
/// </summary>
public static class StepReconciler
{
    /// <summary>
    /// Applies a raw cumulative sensor reading to persisted state: updates baseline,
    /// lifetime, today's total + history, and saves only when something changed.
    /// Returns the (possibly updated) state and the steps just credited.
    /// </summary>
    public static async Task<(PlayerState state, long delta)> ApplyAsync(IGameDataService data, long? raw)
    {
        var state = await data.GetPlayerStateAsync();
        if (raw is null)
            return (state, 0);

        long current = raw.Value;
        long delta = 0;
        bool dirty = false;

        if (!state.BaselineInitialized)
        {
            state.StepBaselineOffset = current;   // §7.1 seed, credit nothing
            state.BaselineInitialized = true;
            dirty = true;
        }
        else if (current != state.StepBaselineOffset)
        {
            delta = current - state.StepBaselineOffset;
            if (delta < 0)
                delta = 0;                        // §7.2 counter went backwards (reboot / reset /
                                                  // glitch). Re-baseline, but DO NOT credit the
                                                  // whole `current` reading — that is the device's
                                                  // entire since-boot count and would dump thousands
                                                  // of (often phantom) steps into today. A fitness
                                                  // tracker must never over-count; we forgo the small
                                                  // gap since the reset instead of inflating the total.
            state.LifetimeSteps += delta;
            state.StepBaselineOffset = current;
            dirty = true;
        }

        var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
        if (state.StepsTodayLocalDate != todayKey)
        {
            if (state.StepsToday > 0 && state.StepsTodayLocalDate.Length > 0)
                await data.RecordDailyStepsAsync(state.StepsTodayLocalDate, state.StepsToday);
            state.StepsTodayLocalDate = todayKey;
            state.StepsToday = 0;
            dirty = true;
        }
        if (delta > 0)
            state.StepsToday += delta;

        if (dirty)
        {
            state.LastSyncUtc = DateTime.UtcNow;
            await data.SavePlayerStateAsync(state);
            if (state.StepsToday > 0)
                await data.RecordDailyStepsAsync(state.StepsTodayLocalDate, state.StepsToday);
        }

        return (state, delta);
    }
}
