namespace WalkBase.Services;

/// <summary>Outcome of a sync: the current totals plus how many steps were just credited.</summary>
public readonly record struct StepSyncResult(
    long LifetimeSteps,
    long StepsToday,
    long Balance,
    long DeltaApplied,
    bool SensorAvailable,
    bool PermissionGranted);

/// <summary>
/// Reconciles the raw cumulative sensor reading into lifetime steps and Bricks,
/// handling the first-launch seed and reboot reset described in spec §7.
/// </summary>
public interface IStepService
{
    bool IsSensorAvailable { get; }

    /// <summary>Request ACTIVITY_RECOGNITION; remembers the result for later syncs.</summary>
    Task<bool> EnsurePermissionAsync();

    /// <summary>Read the sensor, apply the step delta, persist, and return the new totals.</summary>
    Task<StepSyncResult> SyncAsync();
}
