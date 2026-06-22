namespace WalkBase.Services;

/// <summary>
/// Shared declaration; per-platform implementations live in
/// Platforms/Android/BackgroundTracker.android.cs and Platforms/iOS/BackgroundTracker.ios.cs.
/// </summary>
public partial class BackgroundTracker : IBackgroundTracker
{
    public partial bool IsSupported { get; }
    public partial Task<bool> EnsureNotificationPermissionAsync();
    public partial void Start();
    public partial void Stop();
}
