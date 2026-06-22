using SQLite;

namespace WalkBase.Models;

/// <summary>
/// Single-row table (Id is always 1) holding all per-player progress.
/// See spec §6 and §7 for the step-baseline reconciliation model.
/// </summary>
public class PlayerState
{
    [PrimaryKey]
    public int Id { get; set; } = 1;

    /// <summary>Total steps ever counted by the app (monotonic).</summary>
    public long LifetimeSteps { get; set; }

    /// <summary>
    /// Raw sensor reading (cumulative-since-boot) at the last sync.
    /// Used to compute deltas; reset to the current reading after a reboot.
    /// </summary>
    public long StepBaselineOffset { get; set; }

    /// <summary>Whether <see cref="StepBaselineOffset"/> has ever been seeded.</summary>
    public bool BaselineInitialized { get; set; }

    /// <summary>
    /// Running total of Bricks spent. Current balance is derived:
    /// (LifetimeSteps / STEPS_PER_BRICK) - TotalBricksSpent. Storing the
    /// spent total (rather than the balance) makes earning idempotent — we
    /// never double-grant Bricks for the same steps. See spec §8.
    /// </summary>
    public long TotalBricksSpent { get; set; }

    public DateTime LastSyncUtc { get; set; }

    /// <summary>Steps counted so far on <see cref="StepsTodayLocalDate"/> (for the Base header, spec §12).</summary>
    public long StepsToday { get; set; }

    /// <summary>Local calendar date (yyyy-MM-dd) that <see cref="StepsToday"/> applies to.</summary>
    public string StepsTodayLocalDate { get; set; } = string.Empty;

    /// <summary>Side length of the square iso base (grows when the user buys land). 0 = legacy/unmigrated.</summary>
    public int BaseSize { get; set; }

    /// <summary>Bricks awarded outside of walking (quest/milestone rewards). Added to the balance.</summary>
    public long BonusBricks { get; set; }

    /// <summary>Comma-separated ids of quests whose reward has already been claimed.</summary>
    public string ClaimedQuests { get; set; } = string.Empty;
}
