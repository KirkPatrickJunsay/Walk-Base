using System.Globalization;
using WalkBase.Models;

namespace WalkBase.Services;

/// <summary>
/// Computes the daily-goal streak: how many consecutive days (ending today, or yesterday
/// if today's goal isn't met yet) the user hit their step goal, plus the best-ever run.
/// </summary>
public static class StreakCalculator
{
    public static (int current, int best) Compute(
        IEnumerable<DailyStepRecord> history, string todayKey, long todaySteps, int goal)
    {
        if (goal <= 0)
            return (0, 0);

        var met = new HashSet<string>(
            history.Where(r => r.Steps >= goal).Select(r => r.Date), StringComparer.Ordinal);
        if (todaySteps >= goal)
            met.Add(todayKey);

        if (!TryParse(todayKey, out var today))
            return (0, 0);

        // Current streak: count back from today (or yesterday if today is still in progress).
        int current = 0;
        var cursor = met.Contains(todayKey) ? today : today.AddDays(-1);
        while (met.Contains(Key(cursor)))
        {
            current++;
            cursor = cursor.AddDays(-1);
        }

        // Best streak: longest consecutive run across all met days.
        int best = 0, run = 0;
        DateTime? prev = null;
        foreach (var d in met.Select(k => { TryParse(k, out var dt); return dt; }).OrderBy(d => d))
        {
            run = (prev is { } p && (d - p).Days == 1) ? run + 1 : 1;
            best = Math.Max(best, run);
            prev = d;
        }

        return (current, best);
    }

    private static string Key(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static bool TryParse(string key, out DateTime date) =>
        DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
