using Android.Content;
using Android.Hardware;
using Microsoft.Maui.ApplicationModel;
using AndroidApp = Android.App.Application;

namespace WalkBase.Services;

public partial class StepSensor
{
    /// <summary>ACTIVITY_RECOGNITION is a runtime permission on Android 10+ (API 29).</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("android29.0")]
    private sealed class ActivityRecognitionPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            new[] { (Android.Manifest.Permission.ActivityRecognition, true) };
    }

    private static SensorManager? Manager =>
        AndroidApp.Context.GetSystemService(Context.SensorService) as SensorManager;

    // TYPE_STEP_COUNTER is an "on-change" sensor: it only delivers new counts to a
    // listener that STAYS registered while you walk (Samsung in particular won't flush
    // increments to a transient listener). So we register once and keep a live value.
    private readonly object _gate = new();
    private LiveStepListener? _listener;
    private Sensor? _sensor;
    private long? _latest;
    private bool _stopped;

    public partial bool IsAvailable =>
        Manager?.GetDefaultSensor(SensorType.StepCounter) is not null;

    public partial async Task<bool> RequestPermissionAsync()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            var status = await Permissions.RequestAsync<ActivityRecognitionPermission>();
            return status == PermissionStatus.Granted;
        }
        return true; // Pre-API-29 grants ACTIVITY_RECOGNITION at install time.
    }

    private void EnsureRegistered()
    {
        if (_stopped || _listener is not null)
            return;

        var manager = Manager;
        _sensor = manager?.GetDefaultSensor(SensorType.StepCounter);
        if (manager is null || _sensor is null)
            return;

        _listener = new LiveStepListener(value =>
        {
            lock (_gate)
                _latest = value;
        });
        // Stay registered for the app's foreground lifetime. SensorDelay is advisory for
        // an on-change sensor; the framework delivers the current value on registration
        // and every subsequent change as the user walks.
        manager.RegisterListener(_listener, _sensor, SensorDelay.Normal);
    }

    public partial void Stop()
    {
        _stopped = true; // block re-subscription from any in-flight read
        if (_listener is null)
            return;
        Manager?.UnregisterListener(_listener);
        _listener = null;
        // Keep _latest: it's the last known cumulative reading, still valid after resume.
    }

    public partial void Resume() => _stopped = false;

    public partial async Task<long?> ReadRawCumulativeAsync()
    {
        EnsureRegistered();
        if (_sensor is null)
            return null;

        // On a cold start the first on-register event can take a moment; wait briefly.
        for (int i = 0; i < 30; i++)
        {
            lock (_gate)
            {
                if (_latest is not null)
                    return _latest;
            }
            await Task.Delay(100);
        }

        lock (_gate)
            return _latest;
    }

    private sealed class LiveStepListener : Java.Lang.Object, ISensorEventListener
    {
        private readonly Action<long> _onValue;
        public LiveStepListener(Action<long> onValue) => _onValue = onValue;

        public void OnAccuracyChanged(Sensor? sensor, SensorStatus accuracy) { }

        public void OnSensorChanged(SensorEvent? e)
        {
            if (e?.Values is { Count: > 0 } v)
                _onValue((long)v[0]);
        }
    }
}
