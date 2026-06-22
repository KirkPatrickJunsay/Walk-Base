using SQLite;
using WalkBase.Models;

namespace WalkBase.Services;

/// <summary>
/// SQLite persistence for <see cref="PlayerState"/> and <see cref="PlacedBuilding"/>.
/// The DB file lives in the app sandbox (FileSystem.AppDataDirectory) — all data is
/// on-device, no network (spec §3). Spends are transactional (spec §8).
/// </summary>
public sealed class GameDataService : IGameDataService
{
    private readonly ICurrencyService _currency;
    private SQLiteAsyncConnection? _db;

    public GameDataService(ICurrencyService currency) => _currency = currency;

    public async Task InitializeAsync()
    {
        if (_db is not null)
            return;

        var path = Path.Combine(FileSystem.AppDataDirectory, "walkbase.db3");
        _db = new SQLiteAsyncConnection(path,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

        // WAL + a busy timeout let the UI and the background tracking service share the
        // DB file (two connections, same app) without "database is locked" errors.
        // These PRAGMAs return a row, so they must use ExecuteScalar, not ExecuteNonQuery.
        await _db.ExecuteScalarAsync<string>("PRAGMA journal_mode=WAL");
        await _db.ExecuteScalarAsync<int>("PRAGMA busy_timeout=3000");

        await _db.CreateTableAsync<PlayerState>();
        await _db.CreateTableAsync<PlacedBuilding>();
        await _db.CreateTableAsync<DailyStepRecord>();

        // Migrate legacy saves (made when the base was a fixed 8×8) to that size so their
        // existing buildings stay in bounds. The freshly-added column is NULL on old rows,
        // so the IS NULL check matters. (UPDATE returns no row, ExecuteAsync is fine.)
        await _db.ExecuteAsync(
            "UPDATE PlayerState SET BaseSize = ? WHERE BaseSize IS NULL OR BaseSize < ?",
            BaseExpansion.LegacySize, BaseExpansion.InitialSize);

        // Ensure the singleton player row exists.
        var existing = await _db.FindAsync<PlayerState>(1);
        if (existing is null)
            await _db.InsertAsync(new PlayerState
            {
                Id = 1,
                LastSyncUtc = DateTime.UtcNow,
                BaseSize = BaseExpansion.InitialSize,
            });
    }

    private SQLiteAsyncConnection Db =>
        _db ?? throw new InvalidOperationException("GameDataService.InitializeAsync() not called.");

    public async Task<PlayerState> GetPlayerStateAsync()
    {
        await InitializeAsync();
        return await Db.FindAsync<PlayerState>(1)
               ?? new PlayerState { Id = 1 };
    }

    public async Task SavePlayerStateAsync(PlayerState state)
    {
        await InitializeAsync();
        await Db.InsertOrReplaceAsync(state);
    }

    public async Task<List<PlacedBuilding>> GetPlacedBuildingsAsync()
    {
        await InitializeAsync();
        return await Db.Table<PlacedBuilding>().ToListAsync();
    }

    public async Task RecordDailyStepsAsync(string localDate, long steps)
    {
        await InitializeAsync();
        await Db.InsertOrReplaceAsync(new DailyStepRecord { Date = localDate, Steps = steps });
    }

    public async Task<List<DailyStepRecord>> GetDailyHistoryAsync(int limit = 60)
    {
        await InitializeAsync();
        return await Db.Table<DailyStepRecord>()
            .OrderByDescending(d => d.Date)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<bool> IsBuiltAsync(string buildingId)
    {
        await InitializeAsync();
        var count = await Db.Table<PlacedBuilding>()
            .Where(b => b.BuildingId == buildingId)
            .CountAsync();
        return count > 0;
    }

    public async Task<PurchaseResult> TryPurchaseAsync(string buildingId, int gridX = -1, int gridY = -1)
    {
        await InitializeAsync();

        var def = BuildingCatalog.Find(buildingId);
        if (def is null)
            return PurchaseResult.UnknownBuilding;

        var result = PurchaseResult.Success;

        // sqlite-net runs the body synchronously inside one transaction; we read and
        // write on the same connection so the balance check and the deduction are atomic.
        await Db.RunInTransactionAsync(conn =>
        {
            var state = conn.Find<PlayerState>(1) ?? new PlayerState { Id = 1 };
            var placed = conn.Table<PlacedBuilding>().ToList();

            // Unlock gating (spec §10): prerequisite building must already exist.
            if (def.UnlockRequirement is not null &&
                !placed.Any(b => b.BuildingId == def.UnlockRequirement))
            {
                result = PurchaseResult.Locked;
                return;
            }

            // Uniqueness: landmarks may only be placed once.
            if (def.IsUnique && placed.Any(b => b.BuildingId == def.Id))
            {
                result = PurchaseResult.AlreadyBuilt;
                return;
            }

            int size = state.BaseSize > 0 ? state.BaseSize : BaseExpansion.InitialSize;

            // Resolve target plot.
            int x = gridX, y = gridY;
            if (x < 0 || y < 0)
            {
                if (!TryFindFreePlot(placed, size, out x, out y))
                {
                    result = PurchaseResult.NoFreePlot;
                    return;
                }
            }
            else if (x >= size || y >= size || placed.Any(b => b.GridX == x && b.GridY == y))
            {
                result = PurchaseResult.PlotOccupied;
                return;
            }

            // Affordability (balance derived from steps − spent). Free in Debug builds.
            long cost = BuildConfig.FreeBuilds ? 0 : def.BaseCost;
            var balance = _currency.Balance(state);
            if (balance < cost)
            {
                result = PurchaseResult.NotEnoughBricks;
                return;
            }

            state.TotalBricksSpent += cost;
            conn.InsertOrReplace(state);
            conn.Insert(new PlacedBuilding
            {
                BuildingId = def.Id,
                GridX = x,
                GridY = y,
                Level = 1,
            });
        });

        return result;
    }

    public async Task<PurchaseResult> TryMoveBuildingAsync(int placedBuildingId, int gridX, int gridY)
    {
        await InitializeAsync();

        var result = PurchaseResult.Success;

        await Db.RunInTransactionAsync(conn =>
        {
            var pb = conn.Find<PlacedBuilding>(placedBuildingId);
            if (pb is null)
            {
                result = PurchaseResult.NotFound;
                return;
            }

            var state = conn.Find<PlayerState>(1) ?? new PlayerState { Id = 1 };
            int size = state.BaseSize > 0 ? state.BaseSize : BaseExpansion.InitialSize;

            // Target must be on the base and not already occupied by a different building.
            if (gridX < 0 || gridY < 0 || gridX >= size || gridY >= size ||
                conn.Table<PlacedBuilding>().ToList().Any(b => b.Id != placedBuildingId && b.GridX == gridX && b.GridY == gridY))
            {
                result = PurchaseResult.PlotOccupied;
                return;
            }

            pb.GridX = gridX;
            pb.GridY = gridY;
            conn.Update(pb);
        });

        return result;
    }

    /// <summary>Fraction of invested Bricks returned when a building is sold.</summary>
    public const double SellRefundRate = 0.5;

    public async Task<long> SellBuildingAsync(int placedBuildingId)
    {
        await InitializeAsync();

        long refund = 0;

        await Db.RunInTransactionAsync(conn =>
        {
            var pb = conn.Find<PlacedBuilding>(placedBuildingId);
            if (pb is null)
                return;

            var def = BuildingCatalog.Find(pb.BuildingId);
            if (def is not null)
                refund = (long)(def.InvestedBricks(pb.Level) * SellRefundRate);

            var state = conn.Find<PlayerState>(1) ?? new PlayerState { Id = 1 };
            // Crediting back = lowering lifetime spend (balance = earned − spent).
            state.TotalBricksSpent = Math.Max(0, state.TotalBricksSpent - refund);
            conn.InsertOrReplace(state);
            conn.Delete(pb);
        });

        return refund;
    }

    public async Task<BackupData> ExportAsync()
    {
        await InitializeAsync();
        return new BackupData
        {
            ExportedUtc = DateTime.UtcNow.ToString("o"),
            Player = await GetPlayerStateAsync(),
            Buildings = await GetPlacedBuildingsAsync(),
            History = await Db.Table<DailyStepRecord>().ToListAsync(),
        };
    }

    public async Task ImportAsync(BackupData data)
    {
        await InitializeAsync();

        await Db.RunInTransactionAsync(conn =>
        {
            conn.DeleteAll<PlacedBuilding>();
            conn.DeleteAll<DailyStepRecord>();

            var player = data.Player ?? new PlayerState { Id = 1 };
            player.Id = 1;
            conn.InsertOrReplace(player);

            foreach (var b in data.Buildings)
                conn.InsertOrReplace(b);
            foreach (var h in data.History)
                conn.InsertOrReplace(h);
        });
    }

    public async Task<long> ClaimQuestAsync(string questId, long reward)
    {
        await InitializeAsync();

        long granted = 0;

        await Db.RunInTransactionAsync(conn =>
        {
            var state = conn.Find<PlayerState>(1) ?? new PlayerState { Id = 1 };
            var claimed = QuestEvaluator.ParseClaimed(state.ClaimedQuests);
            if (!claimed.Add(questId))   // already claimed → idempotent no-op
                return;

            state.ClaimedQuests = string.Join(",", claimed);
            state.BonusBricks += reward;
            granted = reward;
            conn.InsertOrReplace(state);
        });

        return granted;
    }

    public async Task<PurchaseResult> TryUpgradeAsync(int placedBuildingId)
    {
        await InitializeAsync();

        var result = PurchaseResult.Success;

        await Db.RunInTransactionAsync(conn =>
        {
            var pb = conn.Find<PlacedBuilding>(placedBuildingId);
            if (pb is null)
            {
                result = PurchaseResult.NotFound;
                return;
            }

            var def = BuildingCatalog.Find(pb.BuildingId);
            if (def is null)
            {
                result = PurchaseResult.UnknownBuilding;
                return;
            }

            var upgradeCost = def.UpgradeCostFrom(pb.Level);
            if (upgradeCost is null)
            {
                result = PurchaseResult.MaxLevel;
                return;
            }

            long cost = BuildConfig.FreeBuilds ? 0 : upgradeCost.Value;
            var state = conn.Find<PlayerState>(1) ?? new PlayerState { Id = 1 };
            var balance = _currency.Balance(state);
            if (balance < cost)
            {
                result = PurchaseResult.NotEnoughBricks;
                return;
            }

            state.TotalBricksSpent += cost;
            pb.Level += 1;
            conn.InsertOrReplace(state);
            conn.Update(pb);
        });

        return result;
    }

    public async Task<PurchaseResult> TryExpandBaseAsync()
    {
        await InitializeAsync();
        var result = PurchaseResult.Success;

        await Db.RunInTransactionAsync(conn =>
        {
            var state = conn.Find<PlayerState>(1) ?? new PlayerState { Id = 1 };
            int size = state.BaseSize > 0 ? state.BaseSize : BaseExpansion.InitialSize;

            if (!BaseExpansion.CanExpand(size))
            {
                result = PurchaseResult.MaxLevel; // base already at max size
                return;
            }

            long cost = BuildConfig.FreeBuilds ? 0 : BaseExpansion.CostFor(size);
            var balance = _currency.Balance(state);
            if (balance < cost)
            {
                result = PurchaseResult.NotEnoughBricks;
                return;
            }

            state.TotalBricksSpent += cost;
            state.BaseSize = size + 1;
            conn.InsertOrReplace(state);
        });

        return result;
    }

    private static bool TryFindFreePlot(List<PlacedBuilding> placed, int size, out int x, out int y)
    {
        var occupied = new HashSet<(int, int)>(placed.Select(b => (b.GridX, b.GridY)));
        for (int gy = 0; gy < size; gy++)
        {
            for (int gx = 0; gx < size; gx++)
            {
                if (!occupied.Contains((gx, gy)))
                {
                    x = gx;
                    y = gy;
                    return true;
                }
            }
        }
        x = y = -1;
        return false;
    }
}
