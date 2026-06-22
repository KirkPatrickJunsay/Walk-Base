using WalkBase.Models;

namespace WalkBase.Services;

public sealed class StepService : IStepService
{
    private readonly IStepSensor _sensor;
    private readonly IGameDataService _data;
    private readonly ICurrencyService _currency;
    private bool _permissionGranted;

    public StepService(IStepSensor sensor, IGameDataService data, ICurrencyService currency)
    {
        _sensor = sensor;
        _data = data;
        _currency = currency;
    }

    public bool IsSensorAvailable => _sensor.IsAvailable;

    public async Task<bool> EnsurePermissionAsync()
    {
        _permissionGranted = await _sensor.RequestPermissionAsync();
        return _permissionGranted;
    }

    public async Task<StepSyncResult> SyncAsync()
    {
        var raw = await _sensor.ReadRawCumulativeAsync();
        var (state, delta) = await StepReconciler.ApplyAsync(_data, raw);

        return new StepSyncResult(
            state.LifetimeSteps, state.StepsToday, _currency.Balance(state),
            delta, _sensor.IsAvailable, raw is not null || _permissionGranted);
    }
}
