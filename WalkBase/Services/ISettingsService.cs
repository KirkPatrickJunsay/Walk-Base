namespace WalkBase.Services;

/// <summary>User preferences, persisted on-device via MAUI Preferences (no network).</summary>
public interface ISettingsService
{
    /// <summary>Daily step target shown as the Base HUD progress bar.</summary>
    int DailyStepGoal { get; set; }

    /// <summary>User height in cm (0 = unset). Drives stride and distance.</summary>
    int HeightCm { get; set; }

    /// <summary>User weight in kg (0 = unset). Drives calorie estimates.</summary>
    int WeightKg { get; set; }

    /// <summary>Per-step stride in metres, derived from height (falls back to a default).</summary>
    double StrideMeters { get; }

    /// <summary>True once both height and weight are set, so calories can be estimated.</summary>
    bool HasBodyMetrics { get; }

    /// <summary>Keep counting steps in a background foreground-service (persistent notification).</summary>
    bool BackgroundTrackingEnabled { get; set; }

    /// <summary>Local date (yyyy-MM-dd) the goal-reached celebration last fired (once per day).</summary>
    string LastGoalCelebratedDate { get; set; }

    /// <summary>Play short sound effects on actions (build, claim, goal).</summary>
    bool SoundEnabled { get; set; }

    /// <summary>Vibrate on actions (build, claim, goal).</summary>
    bool HapticsEnabled { get; set; }

    /// <summary>True once the first-run welcome/onboarding has been completed.</summary>
    bool HasOnboarded { get; set; }

    /// <summary>Send daily step reminders (near-goal, streak-at-risk, go-for-a-walk).</summary>
    bool RemindersEnabled { get; set; }

    /// <summary>The player's chosen name for their town (shown in the HUD).</summary>
    string TownName { get; set; }

    /// <summary>Pause the town animation (frame loop + particles) for accessibility / battery.</summary>
    bool ReduceMotion { get; set; }

    /// <summary>Capture the backup-relevant preferences.</summary>
    Models.BackupSettings ExportSettings();

    /// <summary>Apply preferences from a restored backup.</summary>
    void ImportSettings(Models.BackupSettings s);
}
