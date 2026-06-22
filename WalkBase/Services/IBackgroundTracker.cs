namespace WalkBase.Services;

/// <summary>Starts/stops the platform background step-tracking service.</summary>
public interface IBackgroundTracker
{
    /// <summary>True if background tracking is available on this platform.</summary>
    bool IsSupported { get; }

    /// <summary>Request the notification permission needed to show the tracking notification (Android 13+).</summary>
    Task<bool> EnsureNotificationPermissionAsync();

    /// <summary>Start the foreground service (no-op if already running or unsupported).</summary>
    void Start();

    /// <summary>Stop the foreground service.</summary>
    void Stop();
}
