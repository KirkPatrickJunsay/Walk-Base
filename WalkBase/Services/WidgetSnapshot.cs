using Microsoft.Maui.Storage;

namespace WalkBase.Services;

/// <summary>
/// Writes the few values the home-screen widget shows into a dedicated, shared SharedPreferences
/// file (read natively by the Android AppWidgetProvider). No network — local only.
/// </summary>
public static class WidgetSnapshot
{
    public const string Store = "walktrack_widget";

    public static void Write(long stepsToday, long bricks, int goal, string townName)
    {
        Preferences.Set("steps", stepsToday, Store);
        Preferences.Set("bricks", bricks, Store);
        Preferences.Set("goal", goal, Store);
        Preferences.Set("town", townName, Store);
    }
}
