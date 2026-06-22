namespace WalkBase.Services;

/// <summary>
/// Thin platform abstraction over the OS step-counter sensor. Implemented per-platform
/// (Android <c>TYPE_STEP_COUNTER</c>, iOS <c>CMPedometer</c>) as a partial class.
/// Returns a monotonic cumulative step reading; delta reconciliation lives in
/// <see cref="StepService"/> (spec §7).
/// </summary>
public interface IStepSensor
{
    /// <summary>True if a step-counter sensor exists on this device.</summary>
    bool IsAvailable { get; }

    /// <summary>Request the ACTIVITY_RECOGNITION runtime permission (Android 10+).</summary>
    Task<bool> RequestPermissionAsync();

    /// <summary>
    /// Current cumulative step reading (Android: since last boot). Null if the sensor
    /// is unavailable, permission is denied, or no reading arrived.
    /// </summary>
    Task<long?> ReadRawCumulativeAsync();

    /// <summary>
    /// Release the sensor subscription (call when the app backgrounds, to save battery).
    /// Until <see cref="Resume"/> is called, reads will not re-subscribe — this prevents a
    /// trailing timer tick from re-acquiring the sensor right after we let it go.
    /// </summary>
    void Stop();

    /// <summary>Re-enable subscription after a <see cref="Stop"/> (call when the app returns).</summary>
    void Resume();
}
