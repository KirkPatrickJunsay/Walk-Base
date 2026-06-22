using WalkBase.Models;

namespace WalkBase.Services;

public enum PurchaseResult
{
    Success,
    NotEnoughBricks,
    Locked,
    AlreadyBuilt,
    PlotOccupied,
    NoFreePlot,
    UnknownBuilding,
    MaxLevel,
    NotFound,
}

public interface IGameDataService
{
    Task InitializeAsync();

    Task<PlayerState> GetPlayerStateAsync();
    Task SavePlayerStateAsync(PlayerState state);

    Task<List<PlacedBuilding>> GetPlacedBuildingsAsync();

    /// <summary>Record (insert or update) the step total for a local calendar day.</summary>
    Task RecordDailyStepsAsync(string localDate, long steps);

    /// <summary>Most-recent-first daily history (capped at <paramref name="limit"/> days).</summary>
    Task<List<DailyStepRecord>> GetDailyHistoryAsync(int limit = 60);

    /// <summary>True once at least one instance of <paramref name="buildingId"/> is placed.</summary>
    Task<bool> IsBuiltAsync(string buildingId);

    /// <summary>
    /// Atomically (single transaction): re-check balance + unlock, deduct cost,
    /// insert the placed building. <paramref name="gridX"/>/<paramref name="gridY"/>
    /// of -1 means "first free plot".
    /// </summary>
    Task<PurchaseResult> TryPurchaseAsync(string buildingId, int gridX = -1, int gridY = -1);

    /// <summary>Atomically upgrade a placed building to its next level if affordable.</summary>
    Task<PurchaseResult> TryUpgradeAsync(int placedBuildingId);

    /// <summary>Relocate a placed building to an empty in-bounds tile (free). Returns
    /// <see cref="PurchaseResult.PlotOccupied"/> if the target is taken or off the base.</summary>
    Task<PurchaseResult> TryMoveBuildingAsync(int placedBuildingId, int gridX, int gridY);

    /// <summary>Demolish a placed building, crediting back a fraction of its invested Bricks.
    /// Returns the refunded amount (0 on failure).</summary>
    Task<long> SellBuildingAsync(int placedBuildingId);

    /// <summary>Mark a quest claimed and credit its reward Bricks (idempotent — re-claiming
    /// the same quest grants nothing). Returns the Bricks granted (0 if already claimed).</summary>
    Task<long> ClaimQuestAsync(string questId, long reward);

    /// <summary>Snapshot the whole save (player + buildings + history) for backup.</summary>
    Task<BackupData> ExportAsync();

    /// <summary>Replace the entire save with a restored backup (atomic).</summary>
    Task ImportAsync(BackupData data);

    /// <summary>Atomically buy one base-size expansion if affordable and below the max.</summary>
    Task<PurchaseResult> TryExpandBaseAsync();
}
