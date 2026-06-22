using WalkBase.Models;
using WalkBase.Services;

namespace WalkBase.Tests;

/// <summary>Feeds a scripted sequence of raw cumulative sensor readings.</summary>
internal sealed class FakeStepSensor : IStepSensor
{
    private readonly Queue<long?> _readings;
    public bool IsAvailable { get; set; } = true;
    public bool PermissionResult { get; set; } = true;

    public FakeStepSensor(params long?[] readings) => _readings = new Queue<long?>(readings);

    public Task<bool> RequestPermissionAsync() => Task.FromResult(PermissionResult);

    public Task<long?> ReadRawCumulativeAsync() =>
        Task.FromResult(_readings.Count > 0 ? _readings.Dequeue() : (long?)null);

    public int StopCount { get; private set; }
    public void Stop() => StopCount++;
    public void Resume() { }
}

/// <summary>In-memory IGameDataService holding a single mutable PlayerState.</summary>
internal sealed class FakeGameData : IGameDataService
{
    public PlayerState State { get; private set; } = new() { Id = 1 };
    public List<PlacedBuilding> Buildings { get; } = new();

    public int SaveCount { get; private set; }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task<PlayerState> GetPlayerStateAsync() => Task.FromResult(State);
    public Task SavePlayerStateAsync(PlayerState state) { State = state; SaveCount++; return Task.CompletedTask; }
    public Task<List<PlacedBuilding>> GetPlacedBuildingsAsync() => Task.FromResult(Buildings);

    public Dictionary<string, long> DailyRecords { get; } = new();
    public Task RecordDailyStepsAsync(string localDate, long steps)
    {
        DailyRecords[localDate] = steps;
        return Task.CompletedTask;
    }
    public Task<List<DailyStepRecord>> GetDailyHistoryAsync(int limit = 60) =>
        Task.FromResult(DailyRecords
            .OrderByDescending(kv => kv.Key, StringComparer.Ordinal)
            .Take(limit)
            .Select(kv => new DailyStepRecord { Date = kv.Key, Steps = kv.Value })
            .ToList());
    public Task<bool> IsBuiltAsync(string buildingId) =>
        Task.FromResult(Buildings.Any(b => b.BuildingId == buildingId));

    public Task<PurchaseResult> TryPurchaseAsync(string buildingId, int gridX = -1, int gridY = -1) =>
        throw new NotImplementedException();
    public Task<PurchaseResult> TryUpgradeAsync(int placedBuildingId) =>
        throw new NotImplementedException();
    public Task<PurchaseResult> TryMoveBuildingAsync(int placedBuildingId, int gridX, int gridY) =>
        throw new NotImplementedException();
    public Task<long> SellBuildingAsync(int placedBuildingId) =>
        throw new NotImplementedException();
    public Task<long> ClaimQuestAsync(string questId, long reward) =>
        throw new NotImplementedException();
    public Task<BackupData> ExportAsync() =>
        throw new NotImplementedException();
    public Task ImportAsync(BackupData data) =>
        throw new NotImplementedException();
    public Task<PurchaseResult> TryExpandBaseAsync() =>
        throw new NotImplementedException();
}
