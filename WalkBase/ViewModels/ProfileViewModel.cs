using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WalkBase.Models;
using WalkBase.Services;

namespace WalkBase.ViewModels;

/// <summary>One achievement badge with display state.</summary>
public sealed class BadgeRow
{
    public required BadgeStatus S { get; init; }

    public string Emoji => S.Badge.Emoji;
    public string Title => S.Badge.Title;
    public string Description => S.Badge.Description;
    public bool Earned => S.Earned;
    public double Opacity => S.Earned ? 1.0 : 0.4;
    public string Status => S.Earned ? "Earned ✓" : $"{S.Current:N0} / {S.Badge.Threshold:N0}";
}

public partial class ProfileViewModel : ObservableObject
{
    private readonly IGameDataService _data;
    private readonly ISettingsService _settings;

    [ObservableProperty] private ObservableCollection<BadgeRow> _badges = new();
    [ObservableProperty] private string _summary = string.Empty;
    [ObservableProperty] private string _lifetimeSteps = "0";
    [ObservableProperty] private string _distance = "0.00 km";
    [ObservableProperty] private int _buildingCount;
    [ObservableProperty] private int _bestStreak;

    public ProfileViewModel(IGameDataService data, ISettingsService settings)
    {
        _data = data;
        _settings = settings;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var state = await _data.GetPlayerStateAsync();
        var placed = await _data.GetPlacedBuildingsAsync();
        var history = await _data.GetDailyHistoryAsync();

        var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
        var (_, best) = StreakCalculator.Compute(history, todayKey, state.StepsToday, _settings.DailyStepGoal);

        var metrics = new QuestMetrics(
            state.LifetimeSteps, placed.Count,
            placed.Select(b => b.BuildingId).Distinct().Count(),
            state.BaseSize > 0 ? state.BaseSize : BaseExpansion.InitialSize,
            best, history.Count);

        var statuses = BadgeEvaluator.Evaluate(metrics);
        Badges = new ObservableCollection<BadgeRow>(statuses.Select(s => new BadgeRow { S = s }));
        Summary = $"{statuses.Count(s => s.Earned)} of {statuses.Count} badges earned";

        LifetimeSteps = state.LifetimeSteps.ToString("N0");
        Distance = $"{HealthMath.DistanceKm(state.LifetimeSteps, _settings.StrideMeters):N2} km";
        BuildingCount = placed.Count;
        BestStreak = best;
    }
}
