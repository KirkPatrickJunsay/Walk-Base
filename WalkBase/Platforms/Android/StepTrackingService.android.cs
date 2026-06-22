using Android.App;
using Android.Content;
using Android.Hardware;
using Android.OS;
using AndroidX.Core.App;
using WalkBase.Services;
using AndroidApp = Android.App.Application;

namespace WalkBase.Platforms.Android;

/// <summary>
/// Foreground service that keeps counting steps (and crediting Bricks) while the app is
/// closed. Holds its own step-counter listener and reconciles into the shared SQLite DB
/// every minute via <see cref="StepReconciler"/>. No network — local only.
/// </summary>
[Service(Exported = false, ForegroundServiceType = (global::Android.Content.PM.ForegroundService)FgsTypeHealth)]
public sealed class StepTrackingService : Service, ISensorEventListener
{
    public const string ChannelId = "walktrack_steps";
    public const int NotificationId = 4711;
    public const string NudgeChannelId = "walktrack_nudges";
    public const int NudgeNotificationId = 4712;
    public const string ActionStop = "com.codesandchips.walktrack.STOP_TRACKING";
    private const int FgsTypeHealth = 256; // ServiceInfo.FOREGROUND_SERVICE_TYPE_HEALTH

    private SensorManager? _manager;
    private Sensor? _sensor;
    private long? _latest;
    private System.Threading.Timer? _timer;
    private readonly GameDataService _data = new(new CurrencyService());
    private readonly CurrencyService _currency = new();
    private readonly SettingsService _settings = new();

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == ActionStop)
        {
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        CreateChannel();
        StartInForeground(BuildNotification("Counting your steps", "Walk Track is tracking in the background"));

        _manager = GetSystemService(SensorService) as SensorManager;
        _sensor = _manager?.GetDefaultSensor(SensorType.StepCounter);
        if (_manager is not null && _sensor is not null)
            _manager.RegisterListener(this, _sensor, SensorDelay.Normal);

        // Reconcile to the DB every minute (plus immediately).
        _timer ??= new System.Threading.Timer(_ => _ = ReconcileAsync(), null,
            TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1));

        return StartCommandResult.Sticky;
    }

    public void OnSensorChanged(SensorEvent? e)
    {
        if (e?.Values is { Count: > 0 } v)
            _latest = (long)v[0];
    }

    public void OnAccuracyChanged(Sensor? sensor, SensorStatus accuracy) { }

    private async Task ReconcileAsync()
    {
        try
        {
            if (_latest is null)
                return;
            var (state, _) = await StepReconciler.ApplyAsync(_data, _latest);
            long bricks = _currency.Balance(state);
            UpdateNotification($"{state.StepsToday:N0} steps today",
                $"🧱 {bricks:N0} Bricks · goal {_settings.DailyStepGoal:N0}");
            await CheckNudgesAsync(state);

            // Refresh the home-screen widget with the latest numbers.
            WidgetSnapshot.Write(state.StepsToday, bricks, _settings.DailyStepGoal, _settings.TownName);
            WalkTrackWidget.RequestUpdate(this);
        }
        catch
        {
            // Background best-effort; never crash the service on a transient DB hiccup.
        }
    }

    private void StartInForeground(Notification notification)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            StartForeground(NotificationId, notification,
                (global::Android.Content.PM.ForegroundService)FgsTypeHealth);
        else
            StartForeground(NotificationId, notification);
    }

    private void CreateChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        var channel = new NotificationChannel(ChannelId, "Step tracking", NotificationImportance.Low)
        {
            Description = "Keeps counting your steps in the background.",
        };
        channel.SetShowBadge(false);
        manager?.CreateNotificationChannel(channel);

        // Separate, alerting channel for the daily reminders.
        var nudges = new NotificationChannel(NudgeChannelId, "Reminders", NotificationImportance.Default)
        {
            Description = "Gentle nudges to reach your daily step goal.",
        };
        manager?.CreateNotificationChannel(nudges);
    }

    private Notification BuildNotification(string title, string text)
    {
        var launch = PackageManager?.GetLaunchIntentForPackage(PackageName!);
        var pending = launch is null ? null : PendingIntent.GetActivity(
            this, 0, launch, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        return new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle(title)
            .SetContentText(text)
            .SetSmallIcon(global::Android.Resource.Drawable.StatNotifySync)
            .SetContentIntent(pending)
            .SetOngoing(true)
            .SetOnlyAlertOnce(true)
            .SetPriority(NotificationCompat.PriorityLow)
            .Build();
    }

    private void UpdateNotification(string title, string text)
    {
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.Notify(NotificationId, BuildNotification(title, text));
    }

    /// <summary>Evaluate the daily reminders and post one if due (at most one of each per day).</summary>
    private async Task CheckNudgesAsync(WalkBase.Models.PlayerState state)
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        int goal = _settings.DailyStepGoal;

        var history = await _data.GetDailyHistoryAsync();
        var (streak, _) = StreakCalculator.Compute(history, today, state.StepsToday, goal);

        var ctx = new NudgeContext(
            RemindersEnabled: _settings.RemindersEnabled,
            Goal: goal,
            StepsToday: state.StepsToday,
            CurrentStreak: streak,
            Hour: DateTime.Now.Hour,
            GoalReachedToday: goal > 0 && state.StepsToday >= goal,
            NearGoalSent: NudgeSentToday(NudgeKind.NearGoal, today),
            StreakSent: NudgeSentToday(NudgeKind.StreakAtRisk, today),
            ReminderSent: NudgeSentToday(NudgeKind.DailyReminder, today),
            GoalSent: NudgeSentToday(NudgeKind.GoalReached, today));

        var kind = NotificationPlanner.Decide(ctx);
        if (kind == NudgeKind.None)
            return;

        long remaining = Math.Max(0, goal - state.StepsToday);
        var (title, text) = kind switch
        {
            NudgeKind.GoalReached => ("Goal smashed! 🎉", $"You hit {goal:N0} steps today — spend your Bricks and grow your town."),
            NudgeKind.NearGoal => ("Almost there! 🎯", $"You're {remaining:N0} steps from today's goal."),
            NudgeKind.StreakAtRisk => ("Keep your streak alive 🔥", $"Your {streak}-day streak needs {remaining:N0} more steps today."),
            _ => ("Time for a walk? 👟", "Step out to earn Bricks and grow your town."),
        };

        PostNudge(title, text);
        Microsoft.Maui.Storage.Preferences.Default.Set(NudgeKey(kind), today);
    }

    private static string NudgeKey(NudgeKind kind) => $"nudge_{kind}";

    private static bool NudgeSentToday(NudgeKind kind, string today) =>
        Microsoft.Maui.Storage.Preferences.Default.Get(NudgeKey(kind), string.Empty) == today;

    private void PostNudge(string title, string text)
    {
        var launch = PackageManager?.GetLaunchIntentForPackage(PackageName!);
        var pending = launch is null ? null : PendingIntent.GetActivity(
            this, 1, launch, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var notification = new NotificationCompat.Builder(this, NudgeChannelId)
            .SetContentTitle(title)
            .SetContentText(text)
            .SetSmallIcon(global::Android.Resource.Drawable.StatNotifySync)
            .SetContentIntent(pending)
            .SetAutoCancel(true)
            .SetPriority(NotificationCompat.PriorityDefault)
            .Build();

        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.Notify(NudgeNotificationId, notification);
    }

    public override void OnDestroy()
    {
        _timer?.Dispose();
        _timer = null;
        if (_manager is not null)
            _manager.UnregisterListener(this);
        base.OnDestroy();
    }
}
