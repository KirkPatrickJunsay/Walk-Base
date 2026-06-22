using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WalkBase.Models;
using WalkBase.Rendering;
using WalkBase.Services;

namespace WalkBase.ViewModels;

/// <summary>One slide in the first-run onboarding carousel.</summary>
public sealed record OnboardSlide(string Emoji, string Title, string Body);

public partial class BaseViewModel : ObservableObject
{
    private readonly IStepService _steps;
    private readonly IGameDataService _data;
    private readonly SpriteCache _sprites;
    private readonly SelectionState _selection;
    private readonly ISettingsService _settings;
    private readonly IBackgroundTracker _tracker;
    private readonly IFeedbackService _feedback;
    private readonly IWidgetService _widget;
    private readonly IShareService _share;

    public IsometricRenderer Renderer { get; }

    /// <summary>Little people wandering the base (animated by the page's frame loop).</summary>
    public TownsfolkSim Townsfolk { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExpand))]
    private int _gridSize = BaseExpansion.InitialSize;

    /// <summary>True while the base can still grow (drives the translucent buyable ring).</summary>
    public bool CanExpand => BaseExpansion.CanExpand(GridSize);

    /// <summary>Raised when render data changes so the page can InvalidateSurface().</summary>
    public event Action? StateChanged;

    // ===== Zoom / pan (so tiles are easy to tap) =====
    public const float MinZoom = 1f, MaxZoom = 3f;
    [ObservableProperty] private float _zoom = 1f;
    public float PanX { get; set; }
    public float PanY { get; set; }

    /// <summary>Number of townsfolk living on the base (grows as you build dwellings).</summary>
    [ObservableProperty] private int _population = 2;

    /// <summary>The player's town name, shown atop the HUD.</summary>
    [ObservableProperty] private string _townName = "My Town";

    /// <summary>Town happiness (0–100) with a friendly label/emoji, shown in the HUD.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HappinessText))]
    private string _happinessEmoji = "🙂";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HappinessText))]
    private string _happinessLabel = "Content";
    public string HappinessText => $"{HappinessEmoji} {HappinessLabel}";

    /// <summary>Quest rewards ready to claim — shows a badge on the Goals button.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClaimableQuests))]
    [NotifyPropertyChangedFor(nameof(QuestsBadge))]
    private int _questsClaimable;
    public bool HasClaimableQuests => QuestsClaimable > 0;
    public string QuestsBadge => QuestsClaimable.ToString();

    [RelayCommand]
    private static Task OpenQuests() => Shell.Current.GoToAsync("quests");

    // ===== First-run onboarding =====
    /// <summary>Shown over the Town on first launch until the user taps "Get started".</summary>
    [ObservableProperty] private bool _showOnboarding;

    public IReadOnlyList<OnboardSlide> OnboardSlides { get; } = new List<OnboardSlide>
    {
        new("🧱", "Walk to earn Bricks", "Every 10 steps earns a Brick — counted even while the app is closed."),
        new("🏘️", "Build your town", "Spend Bricks to place and upgrade buildings, then watch townsfolk move in."),
        new("🎯", "Reach your goals", "Hit step milestones and daily streaks to claim bonus Brick rewards."),
        new("🔒", "Private & permission", "Next we'll ask for the Physical activity permission so we can read your step count. No location, no account, no internet — your data never leaves this phone."),
    };

    // The onboarding CTA explains why first (the slide above), then triggers the OS permission
    // prompt — a rationale-before-prompt flow that lifts grant rates and avoids a surprise dialog.
    [RelayCommand]
    private async Task CompleteOnboardingAsync()
    {
        _settings.HasOnboarded = true;
        ShowOnboarding = false;
        await EnsurePermissionAndTrackingAsync();
        await RefreshAsync();
    }

    /// <summary>Request ACTIVITY_RECOGNITION and start background tracking if granted/enabled.</summary>
    private async Task EnsurePermissionAndTrackingAsync()
    {
        SensorAvailable = _steps.IsSensorAvailable;
        if (!SensorAvailable)
            return;

        PermissionGranted = await _steps.EnsurePermissionAsync();
        if (PermissionGranted && _tracker.IsSupported && _settings.BackgroundTrackingEnabled)
        {
            await _tracker.EnsureNotificationPermissionAsync();
            _tracker.Start();
        }
    }

    public void SetZoom(float z)
    {
        Zoom = Math.Clamp(z, MinZoom, MaxZoom);
        if (Zoom <= MinZoom + 0.001f)   // fully zoomed out → recenter
        {
            PanX = 0;
            PanY = 0;
        }
        StateChanged?.Invoke();
    }

    /// <summary>Daily step target (user-adjustable in Settings).</summary>
    public int DailyStepGoal => _settings.DailyStepGoal;

    /// <summary>When true, the page keeps the town static (no animation loop).</summary>
    public bool ReduceMotion => _settings.ReduceMotion;

    [ObservableProperty] private long _bricks;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GoalProgress))]
    [NotifyPropertyChangedFor(nameof(GoalText))]
    [NotifyPropertyChangedFor(nameof(DistanceTodayText))]
    private long _stepsToday;

    public double GoalProgress => Math.Clamp(StepsToday / (double)DailyStepGoal, 0, 1);
    public string GoalText => $"{StepsToday:N0} / {DailyStepGoal:N0} steps today";
    public string DistanceTodayText => $"{HealthMath.DistanceKm(StepsToday, _settings.StrideMeters):N2} km";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStreak))]
    [NotifyPropertyChangedFor(nameof(StreakBadge))]
    private int _currentStreak;
    public bool HasStreak => CurrentStreak > 0;
    public string StreakBadge => $"🔥 {CurrentStreak}";

    /// <summary>Streak count shown on the goal-reached celebration; raised by <see cref="GoalReached"/>.</summary>
    public string CelebrationText =>
        CurrentStreak > 1 ? $"{CurrentStreak}-day streak! 🔥" : "Daily goal reached!";

    /// <summary>Raised once per day when today's steps first cross the goal.</summary>
    public event Action? GoalReached;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSensorOverlay))]
    [NotifyPropertyChangedFor(nameof(OverlayTitle))]
    private bool _sensorAvailable = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSensorOverlay))]
    [NotifyPropertyChangedFor(nameof(OverlayTitle))]
    private bool _permissionGranted = true;

    [ObservableProperty] private string _statusMessage = string.Empty;

    public bool ShowSensorOverlay => !SensorAvailable || !PermissionGranted;

    public string OverlayTitle => !SensorAvailable
        ? "No step sensor on this device"
        : "Step tracking needs permission";

    public List<PlacedBuilding> Placed { get; private set; } = new();
    public (int x, int y)? SelectedPlot { get; private set; }

    // ===== Tile action popup =====
    private enum PopupKind { None, EmptyPlot, Building, Expand }
    private PopupKind _popupKind;
    private PlacedBuilding? _popupBuilding;

    /// <summary>Raised when a popup opens so the page can play the entrance animation.</summary>
    public event Action? PopupOpened;

    [ObservableProperty] private bool _isPopupVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PopupIsBuilding))]
    private bool _popupIsEmptyPlot;

    /// <summary>True for the building (upgrade) popup; drives which header swatch shows.</summary>
    public bool PopupIsBuilding => !PopupIsEmptyPlot;

    [ObservableProperty] private string _popupTitle = string.Empty;
    [ObservableProperty] private string _popupSubtitle = string.Empty;
    [ObservableProperty] private string _popupIconImage = string.Empty;
    [ObservableProperty] private string _popupPrimaryText = string.Empty;
    [ObservableProperty] private bool _popupPrimaryEnabled = true;
    [ObservableProperty] private string _popupCostText = string.Empty;
    [ObservableProperty] private bool _popupCostVisible;

    /// <summary>True only on the building popup — shows the "Move" and "Sell" actions.</summary>
    [ObservableProperty] private bool _popupCanMove;

    /// <summary>Sell-button label (shows the refund; flips to a confirm prompt on first tap).</summary>
    [ObservableProperty] private string _popupSellText = string.Empty;
    [ObservableProperty] private bool _popupSellArmed;

    // ===== Move-a-building mode =====
    private PlacedBuilding? _movingBuilding;

    /// <summary>True while the user is relocating a building (tap a tile to drop it).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MoveHint))]
    private bool _isMoving;

    public string MoveHint => _movingBuilding is { } mb &&
        BuildingCatalog.Find(mb.BuildingId) is { } def
        ? $"Moving {def.Name} — tap an empty tile"
        : "Tap an empty tile to move here";

    private IDispatcherTimer? _timer;
    private bool _timerWired;

    public BaseViewModel(
        IStepService steps,
        IGameDataService data,
        IsometricRenderer renderer,
        SpriteCache sprites,
        SelectionState selection,
        ISettingsService settings,
        IBackgroundTracker tracker,
        IFeedbackService feedback,
        IWidgetService widget,
        IShareService share)
    {
        _steps = steps;
        _data = data;
        Renderer = renderer;
        _sprites = sprites;
        _selection = selection;
        _settings = settings;
        _tracker = tracker;
        _feedback = feedback;
        _widget = widget;
        _share = share;
    }

    /// <summary>Render a shareable card of the town + walking stats and open the system share sheet.</summary>
    [RelayCommand]
    private async Task ShareTownAsync()
    {
        try
        {
            var data = new TownCardData(
                TownName, StepsToday, DistanceTodayText, CurrentStreak,
                Population, Bricks, GridSize, Placed,
                Townsfolk.Walkers, Townsfolk.Pets);
            await _share.ShareTownAsync(data);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't create the share image: {ex.Message}";
        }
    }

    /// <summary>Called on first appearance: request permission, then sync + draw.</summary>
    [RelayCommand]
    private async Task AppearingAsync()
    {
        ShowOnboarding = !_settings.HasOnboarded;
        SensorAvailable = _steps.IsSensorAvailable;

        // On first run, hold the permission prompt until the onboarding CTA shows its rationale.
        // Otherwise request it now (returning users who've already been through onboarding).
        if (!ShowOnboarding)
            await EnsurePermissionAndTrackingAsync();

        await RefreshAsync();
        StartAutoRefresh();
    }

    /// <summary>Polls for new steps every few seconds while the Base tab is visible, so the
    /// header climbs live as you walk (the step sensor flushes counts asynchronously).</summary>
    private void StartAutoRefresh()
    {
        _timer ??= Application.Current?.Dispatcher.CreateTimer();
        if (_timer is null)
            return;

        // Wire the Tick exactly once. Don't gate this on IsRepeating — CreateTimer() may
        // already default it to true, which would skip attaching the handler entirely.
        if (!_timerWired)
        {
            _timerWired = true;
            _timer.Interval = TimeSpan.FromSeconds(3);
            _timer.IsRepeating = true;
            _timer.Tick += async (_, _) => await RefreshAsync();
        }
        if (!_timer.IsRunning)
            _timer.Start();
    }

    [RelayCommand]
    private void Disappearing() => _timer?.Stop();

    /// <summary>Sync steps→bricks and reload placed buildings.</summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        var result = await _steps.SyncAsync();
        SensorAvailable = result.SensorAvailable;
        PermissionGranted = result.PermissionGranted;
        Bricks = result.Balance;
        StepsToday = result.StepsToday;

        var state = await _data.GetPlayerStateAsync();
        GridSize = state.BaseSize > 0 ? state.BaseSize : BaseExpansion.InitialSize;

        // Goal/height can change in Settings; re-raise in case StepsToday was unchanged.
        OnPropertyChanged(nameof(GoalProgress));
        OnPropertyChanged(nameof(GoalText));
        OnPropertyChanged(nameof(DistanceTodayText));

        // Streak + goal-reached celebration (once per day).
        var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
        int goal = _settings.DailyStepGoal;
        var history = await _data.GetDailyHistoryAsync();
        (CurrentStreak, int bestStreak) = StreakCalculator.Compute(history, todayKey, StepsToday, goal);

        if (goal > 0 && StepsToday >= goal && _settings.LastGoalCelebratedDate != todayKey)
        {
            _settings.LastGoalCelebratedDate = todayKey;
            OnPropertyChanged(nameof(CelebrationText));
            Townsfolk.Celebrate();   // the whole town cheers 🎉
            _feedback.Play(FeedbackKind.Goal);
            GoalReached?.Invoke();
        }

        Placed = await _data.GetPlacedBuildingsAsync();
        // Population grows with dwellings (houses/tents add residents); they spawn near buildings.
        int dwellings = Placed.Count(b => b.BuildingId is "house" or "tent");
        var buildingSpots = Placed.Select(b => (b.GridX, b.GridY, b.BuildingId)).ToList();
        Townsfolk.IsNight = DateTime.Now.Hour is < 6 or >= 19;
        Townsfolk.Sync(Math.Clamp(2 + dwellings * 2, 2, 12), GridSize, buildingSpots);
        Population = Townsfolk.Population;

        TownName = _settings.TownName;

        var happiness = HappinessCalculator.Compute(Placed);
        HappinessEmoji = happiness.Emoji;
        HappinessLabel = happiness.Label;

        // How many quest rewards are ready to claim (drives the Goals badge).
        var questMetrics = new QuestMetrics(
            state.LifetimeSteps, Placed.Count,
            Placed.Select(b => b.BuildingId).Distinct().Count(),
            GridSize, bestStreak, history.Count);
        QuestsClaimable = QuestEvaluator.ClaimableCount(questMetrics, QuestEvaluator.ParseClaimed(state.ClaimedQuests));

        // Keep the home-screen widget fresh.
        WidgetSnapshot.Write(StepsToday, Bricks, _settings.DailyStepGoal, TownName);
        _widget.Refresh();

        await PreloadSpritesAsync();
        StateChanged?.Invoke();
    }

    public void SelectPlot(int x, int y)
    {
        SelectedPlot = (x, y);
        _selection.SelectedPlot = (x, y);
        StateChanged?.Invoke();
    }

    public PlacedBuilding? BuildingAt(int x, int y) =>
        Placed.FirstOrDefault(b => b.GridX == x && b.GridY == y);

    /// <summary>Open the "build here" popup for an empty plot and remember the plot.</summary>
    public void ShowEmptyPlotPopup(int x, int y)
    {
        SelectPlot(x, y);
        _popupKind = PopupKind.EmptyPlot;
        _popupBuilding = null;
        PopupIsEmptyPlot = true;

        PopupTitle = "Empty plot";
        PopupSubtitle = "Place a building here";
        PopupCostVisible = false;
        PopupCanMove = false;
        PopupPrimaryText = "Choose building";
        PopupPrimaryEnabled = true;

        OpenPopup();
    }

    /// <summary>Open the upgrade popup for a placed building.</summary>
    public void ShowBuildingPopup(PlacedBuilding pb)
    {
        var def = BuildingCatalog.Find(pb.BuildingId);
        if (def is null)
            return;

        _popupKind = PopupKind.Building;
        _popupBuilding = pb;
        PopupIsEmptyPlot = false;
        PopupCanMove = true;

        long refund = (long)(def.InvestedBricks(pb.Level) * GameDataService.SellRefundRate);
        PopupSellText = $"🗑  Sell · +{refund:N0} 🧱";

        PopupIconImage = $"bld_{def.Id}.png";
        PopupTitle = $"{def.Name} · Level {pb.Level}";

        var nextCost = def.UpgradeCostFrom(pb.Level);
        if (nextCost is null)
        {
            PopupSubtitle = "Fully upgraded — max level";
            PopupCostVisible = false;
            PopupPrimaryText = "Max level";
            PopupPrimaryEnabled = false;
        }
        else
        {
            bool affordable = BuildConfig.FreeBuilds || Bricks >= nextCost.Value;
            PopupSubtitle = affordable ? "Ready to upgrade" : "Keep walking for Bricks";
            PopupCostText = $"{nextCost.Value:N0}";
            PopupCostVisible = true;
            PopupPrimaryText = $"Upgrade to L{pb.Level + 1}";
            PopupPrimaryEnabled = affordable;
        }

        OpenPopup();
    }

    /// <summary>Open the "buy land" popup to grow the base.</summary>
    [RelayCommand]
    private void Expand()
    {
        _popupKind = PopupKind.Expand;
        _popupBuilding = null;
        PopupIsEmptyPlot = true; // reuse the dashed "+" placeholder — it reads as "add"
        PopupCanMove = false;

        if (!BaseExpansion.CanExpand(GridSize))
        {
            PopupTitle = "Town is full";
            PopupSubtitle = $"You've reached the max {BaseExpansion.MaxSize} × {BaseExpansion.MaxSize} town";
            PopupCostVisible = false;
            PopupPrimaryText = "Max size";
            PopupPrimaryEnabled = false;
        }
        else
        {
            long cost = BaseExpansion.CostFor(GridSize);
            PopupTitle = "Buy land";
            PopupSubtitle = $"Expand your town to {GridSize + 1} × {GridSize + 1}";
            PopupCostText = $"{cost:N0}";
            PopupCostVisible = true;
            PopupPrimaryText = "Buy land";
            PopupPrimaryEnabled = BuildConfig.FreeBuilds || Bricks >= cost;
        }

        OpenPopup();
    }

    private void OpenPopup()
    {
        PopupSellArmed = false;   // require a fresh confirm each time the popup opens
        IsPopupVisible = true;
        PopupOpened?.Invoke();
    }

    [RelayCommand]
    private void ClosePopup()
    {
        IsPopupVisible = false;
        _popupKind = PopupKind.None;
        _popupBuilding = null;
    }

    /// <summary>No-op so a tap on the card body doesn't fall through to the scrim's close.</summary>
    [RelayCommand]
    private void PopupNoop() { }

    [RelayCommand]
    private async Task PopupPrimaryAsync()
    {
        switch (_popupKind)
        {
            case PopupKind.EmptyPlot:
                ClosePopup();
                await Shell.Current.GoToAsync("//BuildPage");
                break;

            case PopupKind.Building when _popupBuilding is not null:
                var pb = _popupBuilding;
                ClosePopup();
                if (await _data.TryUpgradeAsync(pb.Id) == PurchaseResult.Success)
                    _feedback.Play(FeedbackKind.Build);
                await RefreshAsync();
                break;

            case PopupKind.Expand:
                ClosePopup();
                if (await _data.TryExpandBaseAsync() == PurchaseResult.Success)
                    _feedback.Play(FeedbackKind.Build);
                await RefreshAsync();
                break;
        }
    }

    /// <summary>Sell the building in the popup. First tap arms a confirm; second tap demolishes.</summary>
    [RelayCommand]
    private async Task SellAsync()
    {
        if (_popupBuilding is null)
            return;

        if (!PopupSellArmed)
        {
            PopupSellArmed = true;
            PopupSellText = "Tap again to confirm sell";
            return;
        }

        var pb = _popupBuilding;
        ClosePopup();
        await _data.SellBuildingAsync(pb.Id);
        _feedback.Play(FeedbackKind.Tap);
        await RefreshAsync();
    }

    /// <summary>Enter move mode from the building popup: pick up the building, await a tile tap.</summary>
    [RelayCommand]
    private void StartMove()
    {
        if (_popupBuilding is null)
            return;
        _movingBuilding = _popupBuilding;
        ClosePopup();
        IsMoving = true;
        OnPropertyChanged(nameof(MoveHint));
        SelectedPlot = (_movingBuilding.GridX, _movingBuilding.GridY); // highlight the source tile
        StateChanged?.Invoke();
    }

    [RelayCommand]
    private void CancelMove()
    {
        IsMoving = false;
        _movingBuilding = null;
        SelectedPlot = null;
        StateChanged?.Invoke();
    }

    /// <summary>Drop the building being moved onto (gx, gy). Tapping its own tile or off-base
    /// cancels; an occupied tile is ignored (move mode stays active to pick another).</summary>
    public async Task TryMoveToAsync(int gx, int gy)
    {
        if (_movingBuilding is null) { CancelMove(); return; }

        // Tap the source tile, or off the base → cancel the move.
        if ((gx == _movingBuilding.GridX && gy == _movingBuilding.GridY) ||
            gx < 0 || gy < 0 || gx >= GridSize || gy >= GridSize)
        {
            CancelMove();
            return;
        }

        // Occupied by another building → stay in move mode so the user can pick again.
        if (BuildingAt(gx, gy) is not null)
            return;

        int id = _movingBuilding.Id;
        IsMoving = false;
        _movingBuilding = null;
        SelectedPlot = null;
        await _data.TryMoveBuildingAsync(id, gx, gy);
        _feedback.Play(FeedbackKind.Tap);
        await RefreshAsync();
    }

    private async Task PreloadSpritesAsync()
    {
        var keys = new List<string?>();
        foreach (var b in Placed)
        {
            var def = BuildingCatalog.Find(b.BuildingId);
            keys.Add(def?.SpriteForLevel(b.Level));
        }
        await _sprites.PreloadAsync(keys);
    }
}
