using Microsoft.Maui.Storage;

namespace WalkBase.Services;

public sealed class SettingsService : ISettingsService
{
    public const int DefaultGoal = 6000;
    public const int MinGoal = 2000;
    public const int MaxGoal = 20000;
    public const int GoalStep = 1000;

    public const int MinHeight = 100, MaxHeight = 250;
    public const int MinWeight = 30, MaxWeight = 250;

    private const string GoalKey = "daily_step_goal";
    private const string HeightKey = "height_cm";
    private const string WeightKey = "weight_kg";

    public int DailyStepGoal
    {
        get => Preferences.Get(GoalKey, DefaultGoal);
        set => Preferences.Set(GoalKey, Math.Clamp(value, MinGoal, MaxGoal));
    }

    public int HeightCm
    {
        get => Preferences.Get(HeightKey, 0);
        set => Preferences.Set(HeightKey, value == 0 ? 0 : Math.Clamp(value, MinHeight, MaxHeight));
    }

    public int WeightKg
    {
        get => Preferences.Get(WeightKey, 0);
        set => Preferences.Set(WeightKey, value == 0 ? 0 : Math.Clamp(value, MinWeight, MaxWeight));
    }

    public double StrideMeters => HealthMath.StrideMeters(HeightCm);

    public bool HasBodyMetrics => HeightCm > 0 && WeightKg > 0;

    private const string BgKey = "background_tracking";
    public bool BackgroundTrackingEnabled
    {
        get => Preferences.Get(BgKey, true);
        set => Preferences.Set(BgKey, value);
    }

    private const string CelebratedKey = "goal_celebrated_date";
    public string LastGoalCelebratedDate
    {
        get => Preferences.Get(CelebratedKey, string.Empty);
        set => Preferences.Set(CelebratedKey, value);
    }

    private const string SoundKey = "sound_enabled";
    public bool SoundEnabled
    {
        get => Preferences.Get(SoundKey, true);
        set => Preferences.Set(SoundKey, value);
    }

    private const string HapticsKey = "haptics_enabled";
    public bool HapticsEnabled
    {
        get => Preferences.Get(HapticsKey, true);
        set => Preferences.Set(HapticsKey, value);
    }

    private const string OnboardedKey = "has_onboarded";
    public bool HasOnboarded
    {
        get => Preferences.Get(OnboardedKey, false);
        set => Preferences.Set(OnboardedKey, value);
    }

    private const string RemindersKey = "reminders_enabled";
    public bool RemindersEnabled
    {
        get => Preferences.Get(RemindersKey, true);
        set => Preferences.Set(RemindersKey, value);
    }

    private const string TownNameKey = "town_name";
    public string TownName
    {
        get => Preferences.Get(TownNameKey, "My Town");
        set => Preferences.Set(TownNameKey, string.IsNullOrWhiteSpace(value) ? "My Town" : value.Trim());
    }

    private const string ReduceMotionKey = "reduce_motion";
    public bool ReduceMotion
    {
        get => Preferences.Get(ReduceMotionKey, false);
        set => Preferences.Set(ReduceMotionKey, value);
    }

    public Models.BackupSettings ExportSettings() => new()
    {
        DailyStepGoal = DailyStepGoal,
        HeightCm = HeightCm,
        WeightKg = WeightKg,
        SoundEnabled = SoundEnabled,
        HapticsEnabled = HapticsEnabled,
        BackgroundTrackingEnabled = BackgroundTrackingEnabled,
    };

    public void ImportSettings(Models.BackupSettings s)
    {
        DailyStepGoal = s.DailyStepGoal;
        HeightCm = s.HeightCm;
        WeightKg = s.WeightKg;
        SoundEnabled = s.SoundEnabled;
        HapticsEnabled = s.HapticsEnabled;
        BackgroundTrackingEnabled = s.BackgroundTrackingEnabled;
    }
}
