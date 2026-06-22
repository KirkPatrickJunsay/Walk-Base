namespace WalkBase.Services;

// iOS background step tracking is deferred (CMPedometer can backfill on resume).
public partial class BackgroundTracker
{
    public partial bool IsSupported => false;
    public partial Task<bool> EnsureNotificationPermissionAsync() => Task.FromResult(false);
    public partial void Start() { }
    public partial void Stop() { }
}
