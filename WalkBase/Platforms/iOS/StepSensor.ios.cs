using CoreMotion;
using Foundation;
using Microsoft.Maui.Storage;

namespace WalkBase.Services;

// iOS is kept compilable but device testing is deferred (spec §1). CMPedometer has no
// "cumulative since boot" counter, so we emulate one by querying steps from a fixed
// per-install anchor date — monotonic, which is all StepService's delta logic needs.
public partial class StepSensor
{
    private const string AnchorKey = "walkbase_ios_pedometer_anchor_unix";
    private readonly CMPedometer _pedometer = new();

    public partial bool IsAvailable => CMPedometer.IsStepCountingAvailable;

    public partial Task<bool> RequestPermissionAsync()
    {
        if (!CMPedometer.IsStepCountingAvailable)
            return Task.FromResult(false);

        // A trivial query triggers the system Motion & Fitness authorization prompt.
        var tcs = new TaskCompletionSource<bool>();
        var from = (NSDate)DateTime.UtcNow.AddMinutes(-1);
        var to = (NSDate)DateTime.UtcNow;
        _pedometer.QueryPedometerData(from, to, (_, error) => tcs.TrySetResult(error is null));
        return tcs.Task;
    }

    public partial Task<long?> ReadRawCumulativeAsync()
    {
        if (!CMPedometer.IsStepCountingAvailable)
            return Task.FromResult<long?>(null);

        var tcs = new TaskCompletionSource<long?>();
        _pedometer.QueryPedometerData(GetOrCreateAnchor(), (NSDate)DateTime.UtcNow, (data, error) =>
            tcs.TrySetResult(error is null && data is not null ? data.NumberOfSteps.Int64Value : null));
        return tcs.Task;
    }

    // Nothing to release: iOS reads are one-shot CMPedometer queries, not a live subscription.
    public partial void Stop() { }
    public partial void Resume() { }

    private static NSDate GetOrCreateAnchor()
    {
        var stored = Preferences.Get(AnchorKey, 0L);
        if (stored == 0L)
        {
            stored = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Preferences.Set(AnchorKey, stored);
        }
        return (NSDate)DateTimeOffset.FromUnixTimeSeconds(stored).UtcDateTime;
    }
}
