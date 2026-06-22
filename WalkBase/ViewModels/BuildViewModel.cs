using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WalkBase.Models;
using WalkBase.Services;

namespace WalkBase.ViewModels;

/// <summary>One row in the Build list: a catalog building with computed state.</summary>
public sealed class BuildingOption
{
    public required Building Definition { get; init; }
    public required bool IsLocked { get; init; }
    public required bool IsAffordable { get; init; }
    public required bool IsOwned { get; init; }
    public required string Subtitle { get; init; }

    public string Name => Definition.Name;
    public string CostText => Definition.BaseCost == 0 ? "Free" : $"{Definition.BaseCost:N0}";
    public string TierBadge => $"TIER {Definition.Tier}";
    public bool CanBuy => !IsLocked && !IsOwned && IsAffordable;
    public double RowOpacity => IsLocked ? 0.5 : 1.0;
    public string BuyText => IsOwned ? "✓ Built" : IsLocked ? "Locked" : "Build";

    /// <summary>Mini iso building icon (generated to match the renderer); see Resources/Images.</summary>
    public string IconImage => $"bld_{Definition.Id}.png";
}

public partial class BuildViewModel : ObservableObject
{
    private readonly IGameDataService _data;
    private readonly ICurrencyService _currency;
    private readonly SelectionState _selection;
    private readonly IFeedbackService _feedback;

    [ObservableProperty] private ObservableCollection<BuildingOption> _options = new();
    [ObservableProperty] private long _bricks;
    [ObservableProperty] private string _plotText = "Placing on: next free plot";

    [ObservableProperty] private string _toastMessage = string.Empty;
    [ObservableProperty] private bool _toastIsSuccess;

    /// <summary>Raised after a successful purchase so the Base tab can redraw on return.</summary>
    public event Action? Purchased;

    /// <summary>Raised to show a themed, auto-dismissing toast (the page animates it).</summary>
    public event Action? ToastRequested;

    public BuildViewModel(IGameDataService data, ICurrencyService currency, SelectionState selection, IFeedbackService feedback)
    {
        _data = data;
        _currency = currency;
        _selection = selection;
        _feedback = feedback;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var state = await _data.GetPlayerStateAsync();
        var placed = await _data.GetPlacedBuildingsAsync();
        var balance = _currency.Balance(state);
        Bricks = balance;

        PlotText = _selection.SelectedPlot is { } p
            ? $"Placing on: plot ({p.x}, {p.y})"
            : "Placing on: next free plot";

        var built = placed.Select(b => b.BuildingId).ToHashSet(StringComparer.Ordinal);
        var rows = new List<BuildingOption>();
        foreach (var def in BuildingCatalog.All)
        {
            bool owned = def.IsUnique && built.Contains(def.Id);
            bool locked = def.UnlockRequirement is not null && !built.Contains(def.UnlockRequirement);
            bool affordable = BuildConfig.FreeBuilds || balance >= def.BaseCost;
            string subtitle =
                owned ? "Already on your base" :
                locked ? $"🔒 Needs {BuildingCatalog.Find(def.UnlockRequirement!)?.Name ?? def.UnlockRequirement}" :
                affordable ? "Ready to build" : "Keep walking for Bricks";

            rows.Add(new BuildingOption
            {
                Definition = def,
                IsLocked = locked,
                IsAffordable = affordable,
                IsOwned = owned,
                Subtitle = subtitle,
            });
        }

        Options = new ObservableCollection<BuildingOption>(rows);
    }

    [RelayCommand]
    private async Task BuyAsync(BuildingOption? option)
    {
        if (option is null)
            return;

        var plot = _selection.SelectedPlot;
        var result = plot is { } p
            ? await _data.TryPurchaseAsync(option.Definition.Id, p.x, p.y)
            : await _data.TryPurchaseAsync(option.Definition.Id);

        var message = result switch
        {
            PurchaseResult.Success => $"Built {option.Name}!",
            PurchaseResult.NotEnoughBricks => "Not enough Bricks — keep walking!",
            PurchaseResult.Locked => "That building is still locked.",
            PurchaseResult.AlreadyBuilt => $"You can only build one {option.Name}.",
            PurchaseResult.PlotOccupied => "That plot is taken.",
            PurchaseResult.NoFreePlot => "No free plots left on the base.",
            _ => "Couldn't build that.",
        };

        if (result == PurchaseResult.Success)
        {
            _feedback.Play(FeedbackKind.Build);
            _selection.SelectedPlot = null; // consume the chosen plot
            Purchased?.Invoke();
            await LoadAsync();
        }

        ToastIsSuccess = result == PurchaseResult.Success;
        ToastMessage = message;
        ToastRequested?.Invoke();
    }
}
