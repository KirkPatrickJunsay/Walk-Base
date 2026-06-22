using WalkBase.Services;

namespace WalkBase.Tests;

public class StepServiceTests
{
    private static StepService Build(FakeStepSensor sensor, FakeGameData data) =>
        new(sensor, data, new CurrencyService());

    [Fact]
    public async Task FirstLaunch_SeedsBaseline_CreditsNothing() // spec §7.1
    {
        var data = new FakeGameData();
        var svc = Build(new FakeStepSensor(5000), data);

        var result = await svc.SyncAsync();

        Assert.Equal(0, result.LifetimeSteps);
        Assert.Equal(0, result.DeltaApplied);
        Assert.Equal(5000, data.State.StepBaselineOffset);
        Assert.True(data.State.BaselineInitialized);
    }

    [Fact]
    public async Task SecondSync_CreditsDelta()
    {
        var data = new FakeGameData();
        var svc = Build(new FakeStepSensor(5000, 5300), data);

        await svc.SyncAsync();           // seed at 5000
        var result = await svc.SyncAsync(); // now 5300 → +300

        Assert.Equal(300, result.DeltaApplied);
        Assert.Equal(300, result.LifetimeSteps);
        Assert.Equal(30, result.Balance); // 300 / 10 steps-per-brick
    }

    [Fact]
    public async Task Reboot_CounterReset_CreditsCurrentAsDelta() // spec §7.2
    {
        var data = new FakeGameData();
        // 5000 (seed) → 5300 (+300) → 80 (reboot: current < baseline → credit 80)
        var svc = Build(new FakeStepSensor(5000, 5300, 80), data);

        await svc.SyncAsync();
        await svc.SyncAsync();
        var afterReboot = await svc.SyncAsync();

        Assert.Equal(80, afterReboot.DeltaApplied);
        Assert.Equal(380, afterReboot.LifetimeSteps); // 300 + 80
        Assert.Equal(80, data.State.StepBaselineOffset);
    }

    [Fact]
    public async Task NoReading_LeavesTotalsUnchanged()
    {
        var data = new FakeGameData();
        var svc = Build(new FakeStepSensor(5000, 5500), data);
        await svc.SyncAsync();
        await svc.SyncAsync(); // lifetime now 500

        var sensor = new FakeStepSensor((long?)null); // sensor returns nothing
        var svc2 = Build(sensor, data);
        var result = await svc2.SyncAsync();

        Assert.Equal(0, result.DeltaApplied);
        Assert.Equal(500, result.LifetimeSteps);
    }

    [Fact]
    public async Task IdleSync_DoesNotWriteTheDatabase()
    {
        var data = new FakeGameData();
        // seed (10000), then +20, then two idle reads at the same raw value.
        var svc = Build(new FakeStepSensor(10000, 10020, 10020, 10020), data);

        await svc.SyncAsync(); // seed → save #1
        await svc.SyncAsync(); // +20 → save #2
        await svc.SyncAsync(); // no change → no save
        await svc.SyncAsync(); // no change → no save

        Assert.Equal(2, data.SaveCount);
    }

    [Fact]
    public async Task StepsToday_AccumulatesForToday()
    {
        var data = new FakeGameData();
        var svc = Build(new FakeStepSensor(1000, 1250, 1400), data);

        await svc.SyncAsync();          // seed
        await svc.SyncAsync();          // +250
        var result = await svc.SyncAsync(); // +150

        Assert.Equal(400, result.StepsToday);
        Assert.Equal(DateTime.Now.ToString("yyyy-MM-dd"), data.State.StepsTodayLocalDate);
    }

    [Fact]
    public async Task StepsToday_ResetsOnNewLocalDay()
    {
        var data = new FakeGameData();
        // Pretend yesterday left 9999 steps on the clock.
        data.State.StepsToday = 9999;
        data.State.StepsTodayLocalDate = "2000-01-01";
        data.State.BaselineInitialized = true;
        data.State.StepBaselineOffset = 1000;

        var svc = Build(new FakeStepSensor(1200), data); // +200 today

        var result = await svc.SyncAsync();

        Assert.Equal(200, result.StepsToday); // not 9999 + 200
    }

    [Fact]
    public async Task DayRollover_ArchivesPreviousDayAndRecordsToday()
    {
        var data = new FakeGameData();
        data.State.StepsToday = 9999;
        data.State.StepsTodayLocalDate = "2000-01-01"; // a past day being left behind
        data.State.BaselineInitialized = true;
        data.State.StepBaselineOffset = 1000;

        var svc = Build(new FakeStepSensor(1200), data); // +200 on the new (today) day
        await svc.SyncAsync();

        // Yesterday's 9999 is archived under its own date...
        Assert.Equal(9999, data.DailyRecords["2000-01-01"]);
        // ...and today's fresh total is recorded under today's date.
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        Assert.Equal(200, data.DailyRecords[today]);
    }

    [Fact]
    public async Task History_RecordsRunningDailyTotal()
    {
        var data = new FakeGameData();
        var svc = Build(new FakeStepSensor(500, 560, 600), data); // seed, +60, +40
        await svc.SyncAsync(); // seed, no steps
        await svc.SyncAsync(); // +60
        await svc.SyncAsync(); // +40

        var today = DateTime.Now.ToString("yyyy-MM-dd");
        Assert.Equal(100, data.DailyRecords[today]); // 60 + 40
    }
}
