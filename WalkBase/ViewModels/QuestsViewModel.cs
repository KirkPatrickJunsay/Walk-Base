using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WalkBase.Models;
using WalkBase.Services;

namespace WalkBase.ViewModels;

/// <summary>One quest row with display-ready text and progress.</summary>
public sealed class QuestRow
{
    public required QuestProgress P { get; init; }

    public string Title => P.Quest.Title;
    public string Description => P.Quest.Description;
    public double Fraction => P.Fraction;
    public string ProgressText => P.IsComplete ? "Done" : $"{P.Current:N0} / {P.Quest.Target:N0}";
    public string RewardText => $"+{P.Quest.Reward:N0} 🧱";
    public bool IsClaimable => P.IsClaimable;
    public bool IsClaimed => P.IsClaimed;
    public bool InProgress => !P.IsComplete;
    public double RowOpacity => P.IsClaimed ? 0.55 : 1.0;
    public string Emoji => P.IsClaimed ? "✅" : P.IsComplete ? "🎁" : "🎯";
}

public partial class QuestsViewModel : ObservableObject
{
    private readonly IGameDataService _data;
    private readonly ISettingsService _settings;
    private readonly IFeedbackService _feedback;

    [ObservableProperty] private ObservableCollection<QuestRow> _quests = new();
    [ObservableProperty] private string _summary = string.Empty;
    [ObservableProperty] private string _toastMessage = string.Empty;

    /// <summary>Raised after a reward is claimed so the page can flash a toast.</summary>
    public event Action? RewardClaimed;

    public QuestsViewModel(IGameDataService data, ISettingsService settings, IFeedbackService feedback)
    {
        _data = data;
        _settings = settings;
        _feedback = feedback;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var progress = await BuildProgressAsync();
        Quests = new ObservableCollection<QuestRow>(progress.Select(p => new QuestRow { P = p }));

        int done = progress.Count(p => p.IsComplete);
        Summary = $"{done} of {progress.Count} milestones reached";
    }

    [RelayCommand]
    private async Task ClaimAsync(QuestRow? row)
    {
        if (row is null || !row.IsClaimable)
            return;

        long granted = await _data.ClaimQuestAsync(row.P.Quest.Id, row.P.Quest.Reward);
        if (granted > 0)
        {
            _feedback.Play(FeedbackKind.Claim);
            ToastMessage = $"Claimed +{granted:N0} 🧱 · {row.Title}";
            RewardClaimed?.Invoke();
        }
        await LoadAsync();
    }

    /// <summary>Computes each quest's standing from current player data.</summary>
    private async Task<IReadOnlyList<QuestProgress>> BuildProgressAsync()
    {
        var state = await _data.GetPlayerStateAsync();
        var placed = await _data.GetPlacedBuildingsAsync();
        var history = await _data.GetDailyHistoryAsync();

        var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
        var (_, bestStreak) = StreakCalculator.Compute(history, todayKey, state.StepsToday, _settings.DailyStepGoal);

        var metrics = new QuestMetrics(
            LifetimeSteps: state.LifetimeSteps,
            BuildingCount: placed.Count,
            DistinctBuildingTypes: placed.Select(b => b.BuildingId).Distinct().Count(),
            BaseSize: state.BaseSize > 0 ? state.BaseSize : BaseExpansion.InitialSize,
            BestStreak: bestStreak,
            DaysWalked: history.Count);

        return QuestEvaluator.Evaluate(metrics, QuestEvaluator.ParseClaimed(state.ClaimedQuests));
    }
}
