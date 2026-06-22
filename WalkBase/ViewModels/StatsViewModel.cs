using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WalkBase.Services;

namespace WalkBase.ViewModels;

public partial class StatsViewModel : ObservableObject
{
    private readonly IGameDataService _data;
    private readonly ICurrencyService _currency;
    private readonly ISettingsService _settings;

    [ObservableProperty] private long _lifetimeSteps;
    [ObservableProperty] private string _distanceText = "0.00 km";
    [ObservableProperty] private long _bricksEarned;
    [ObservableProperty] private long _bricksSpent;
    [ObservableProperty] private long _bricksBalance;
    [ObservableProperty] private int _buildingCount;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsMetrics))]
    private bool _hasMetrics;
    public bool NeedsMetrics => !HasMetrics;

    [ObservableProperty] private string _caloriesText = "0";

    public StatsViewModel(IGameDataService data, ICurrencyService currency, ISettingsService settings)
    {
        _data = data;
        _currency = currency;
        _settings = settings;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var state = await _data.GetPlayerStateAsync();
        var placed = await _data.GetPlacedBuildingsAsync();

        LifetimeSteps = state.LifetimeSteps;
        BricksEarned = _currency.TotalBricksEarned(state.LifetimeSteps);
        BricksSpent = state.TotalBricksSpent;
        BricksBalance = _currency.Balance(state);
        BuildingCount = placed.Count;

        double km = HealthMath.DistanceKm(state.LifetimeSteps, _settings.StrideMeters);
        DistanceText = $"{km:N2} km";

        HasMetrics = _settings.HasBodyMetrics;
        CaloriesText = HasMetrics
            ? $"{HealthMath.Calories(state.LifetimeSteps, _settings.HeightCm, _settings.WeightKg):N0}"
            : "—";
    }

    [RelayCommand]
    private static Task OpenSettings() => Shell.Current.GoToAsync("//SettingsPage");
}
