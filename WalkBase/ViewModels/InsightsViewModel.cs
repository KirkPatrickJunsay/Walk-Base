using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WalkBase.Rendering;
using WalkBase.Services;

namespace WalkBase.ViewModels;

public partial class InsightsViewModel : ObservableObject
{
    private readonly IGameDataService _data;
    private readonly ISettingsService _settings;

    public StepChartRenderer Chart { get; } = new();

    /// <summary>Raised when chart data changes so the page can InvalidateSurface().</summary>
    public event Action? ChartChanged;

    public List<DayBar> Bars { get; private set; } = new();
    public int Goal { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasData))]
    private bool _isEmpty = true;
    public bool HasData => !IsEmpty;

    [ObservableProperty] private string _weekTotalText = "0";
    [ObservableProperty] private string _dailyAvgText = "0";
    [ObservableProperty] private string _bestDayText = "0";
    [ObservableProperty] private string _goalHitText = "0 / 7";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsMetrics))]
    private bool _hasMetrics;
    public bool NeedsMetrics => !HasMetrics;

    [ObservableProperty] private string _caloriesText = "0";

    public InsightsViewModel(IGameDataService data, ISettingsService settings)
    {
        _data = data;
        _settings = settings;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        Goal = _settings.DailyStepGoal;
        var records = await _data.GetDailyHistoryAsync(40);
        var state = await _data.GetPlayerStateAsync();

        var byDate = records.ToDictionary(r => r.Date, r => r.Steps, StringComparer.Ordinal);
        if (state.StepsToday > 0 && state.StepsTodayLocalDate.Length > 0)
            byDate[state.StepsTodayLocalDate] = state.StepsToday;

        var today = DateTime.Now.Date;
        var bars = new List<DayBar>(14);
        for (int i = 13; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            byDate.TryGetValue(d.ToString("yyyy-MM-dd"), out var steps);
            bars.Add(new DayBar(d, steps));
        }
        Bars = bars;
        IsEmpty = bars.All(b => b.Steps == 0);

        var week = bars.Where(b => b.Date >= today.AddDays(-6)).ToList();
        long weekSteps = week.Sum(b => b.Steps);
        long bestDay = bars.Max(b => b.Steps);
        int goalHit = Goal > 0 ? week.Count(b => b.Steps >= Goal) : 0;

        WeekTotalText = $"{weekSteps:N0}";
        DailyAvgText = $"{weekSteps / 7:N0}";
        BestDayText = $"{bestDay:N0}";
        GoalHitText = $"{goalHit} / 7";

        HasMetrics = _settings.HasBodyMetrics;
        CaloriesText = HasMetrics
            ? $"{HealthMath.Calories(weekSteps, _settings.HeightCm, _settings.WeightKg):N0}"
            : "0";

        ChartChanged?.Invoke();
    }

    [RelayCommand]
    private static Task OpenSettings() => Shell.Current.GoToAsync("//SettingsPage");

    [RelayCommand]
    private static Task OpenStats() => Shell.Current.GoToAsync("stats");

    [RelayCommand]
    private static Task OpenProfile() => Shell.Current.GoToAsync("profile");
}
