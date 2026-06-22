using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WalkBase.Services;

namespace WalkBase.ViewModels;

/// <summary>One day in the walk history list.</summary>
public sealed class DailyHistoryItem
{
    public required string DateLabel { get; init; }
    public required long Steps { get; init; }
    public required string DistanceText { get; init; }
    public required double GoalProgress { get; init; }
    public required bool GoalMet { get; init; }

    public string StepsText => $"{Steps:N0}";
    public string GoalIcon => GoalMet ? "🎯" : "👟";
}

public partial class HistoryViewModel : ObservableObject
{
    private readonly IGameDataService _data;
    private readonly ISettingsService _settings;

    [ObservableProperty] private ObservableCollection<DailyHistoryItem> _days = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDays))]
    private bool _isEmpty = true;

    public bool HasDays => !IsEmpty;

    [ObservableProperty] private string _summary = string.Empty;

    public HistoryViewModel(IGameDataService data, ISettingsService settings)
    {
        _data = data;
        _settings = settings;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var records = await _data.GetDailyHistoryAsync();

        // Merge today's live in-progress total so History reflects the current day
        // even before a step sync happens on this tab.
        var state = await _data.GetPlayerStateAsync();
        var byDate = records.ToDictionary(r => r.Date, r => r.Steps, StringComparer.Ordinal);
        if (state.StepsToday > 0 && state.StepsTodayLocalDate.Length > 0)
            byDate[state.StepsTodayLocalDate] = state.StepsToday;

        int goal = _settings.DailyStepGoal;
        double stride = _settings.StrideMeters;
        var items = byDate
            .OrderByDescending(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new DailyHistoryItem
            {
                DateLabel = FormatDate(kv.Key),
                Steps = kv.Value,
                DistanceText = $"{HealthMath.DistanceKm(kv.Value, stride):N2} km",
                GoalProgress = goal > 0 ? Math.Clamp(kv.Value / (double)goal, 0, 1) : 0,
                GoalMet = kv.Value >= goal,
            })
            .ToList();

        Days = new ObservableCollection<DailyHistoryItem>(items);
        IsEmpty = items.Count == 0;

        if (items.Count > 0)
        {
            long best = items.Max(i => i.Steps);
            var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
            long todaySteps = byDate.TryGetValue(todayKey, out var t) ? t : 0;
            var (currentStreak, _) = StreakCalculator.Compute(records, todayKey, todaySteps, goal);
            var streakPart = currentStreak > 0 ? $"🔥 {currentStreak}-day streak · " : "";
            Summary = $"{streakPart}{items.Count} day{(items.Count == 1 ? "" : "s")} walked · best {best:N0}";
        }
        else
        {
            Summary = string.Empty;
        }
    }

    private static string FormatDate(string yyyymmdd)
    {
        if (!DateTime.TryParseExact(yyyymmdd, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            return yyyymmdd;

        var today = DateTime.Now.Date;
        if (date.Date == today) return "Today";
        if (date.Date == today.AddDays(-1)) return "Yesterday";
        return date.ToString("ddd, MMM d", CultureInfo.InvariantCulture);
    }
}
