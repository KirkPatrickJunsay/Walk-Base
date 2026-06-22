using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using WalkBase.Services;

namespace WalkBase.Platforms.Android;

/// <summary>Home-screen widget showing today's steps, goal progress and Bricks. Reads the
/// values written by <see cref="WidgetSnapshot"/>; no network.</summary>
[BroadcastReceiver(Label = "Walk Track", Exported = false)]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
[MetaData("android.appwidget.provider", Resource = "@xml/walktrack_widget_info")]
public sealed class WalkTrackWidget : AppWidgetProvider
{
    public override void OnUpdate(Context context, AppWidgetManager manager, int[] appWidgetIds)
    {
        var prefs = context.GetSharedPreferences(WidgetSnapshot.Store, FileCreationMode.Private);
        long steps = prefs!.GetLong("steps", 0);
        long bricks = prefs.GetLong("bricks", 0);
        int goal = prefs.GetInt("goal", 6000);
        string town = prefs.GetString("town", "My Town") ?? "My Town";
        int pct = goal > 0 ? (int)Math.Clamp(steps * 100 / goal, 0, 100) : 0;

        foreach (var id in appWidgetIds)
        {
            var views = new RemoteViews(context.PackageName, Resource.Layout.walktrack_widget);
            views.SetTextViewText(Resource.Id.widget_town, town);
            views.SetTextViewText(Resource.Id.widget_bricks, $"🧱 {bricks:N0}");
            views.SetTextViewText(Resource.Id.widget_steps, $"{steps:N0} / {goal:N0} steps");
            views.SetProgressBar(Resource.Id.widget_progress, 100, pct, false);

            var launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
            if (launch is not null)
            {
                var pi = PendingIntent.GetActivity(context, 0, launch,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
                views.SetOnClickPendingIntent(Resource.Id.widget_root, pi);
            }

            manager.UpdateAppWidget(id, views);
        }
    }

    /// <summary>Push a fresh render to every placed widget (call after the data changes).</summary>
    public static void RequestUpdate(Context context)
    {
        var manager = AppWidgetManager.GetInstance(context);
        var component = new ComponentName(context, Java.Lang.Class.FromType(typeof(WalkTrackWidget)));
        var ids = manager?.GetAppWidgetIds(component);
        if (ids is { Length: > 0 })
        {
            var intent = new Intent(context, typeof(WalkTrackWidget));
            intent.SetAction(AppWidgetManager.ActionAppwidgetUpdate);
            intent.PutExtra(AppWidgetManager.ExtraAppwidgetIds, ids);
            context.SendBroadcast(intent);
        }
    }
}
