using Android.Content;
using Microsoft.Maui.ApplicationModel;
using WalkBase.Platforms.Android;
using AndroidApp = Android.App.Application;

namespace WalkBase.Services;

public partial class BackgroundTracker
{
    [System.Runtime.Versioning.SupportedOSPlatform("android33.0")]
    private sealed class PostNotificationsPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            new[] { (global::Android.Manifest.Permission.PostNotifications, true) };
    }

    public partial bool IsSupported => true;

    public partial async Task<bool> EnsureNotificationPermissionAsync()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
            return true; // notifications don't need a runtime grant pre-Android 13
        var status = await Permissions.RequestAsync<PostNotificationsPermission>();
        return status == PermissionStatus.Granted;
    }

    public partial void Start()
    {
        var ctx = AndroidApp.Context;
        var intent = new Intent(ctx, typeof(StepTrackingService));
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            ctx.StartForegroundService(intent);
        else
            ctx.StartService(intent);
    }

    public partial void Stop()
    {
        var ctx = AndroidApp.Context;
        ctx.StopService(new Intent(ctx, typeof(StepTrackingService)));
    }
}
