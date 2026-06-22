using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WalkBase.Services;

namespace WalkBase.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IBackgroundTracker _tracker;
    private readonly IGameDataService _data;
    private bool _loading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDataStatus))]
    private string _dataStatus = string.Empty;
    public bool HasDataStatus => !string.IsNullOrEmpty(DataStatus);

    [ObservableProperty] private bool _backgroundTracking;
    public bool BackgroundSupported => _tracker.IsSupported;

    [ObservableProperty] private bool _soundEnabled;
    [ObservableProperty] private bool _hapticsEnabled;
    [ObservableProperty] private bool _remindersEnabled;
    [ObservableProperty] private bool _reduceMotion;
    [ObservableProperty] private string _townName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MetricsPreview))]
    private string _heightText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MetricsPreview))]
    private string _weightText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GoalDisplay))]
    private double _dailyGoal;

    private int SnappedGoal =>
        (int)(Math.Round(DailyGoal / SettingsService.GoalStep) * SettingsService.GoalStep);

    public string GoalDisplay => $"{SnappedGoal:N0} steps";

    /// <summary>Live feedback so the user sees what their numbers do.</summary>
    public string MetricsPreview
    {
        get
        {
            int h = ParseOrZero(HeightText), w = ParseOrZero(WeightText);
            if (h <= 0 && w <= 0)
                return "Set your height and weight to estimate distance and calories.";
            double stride = HealthMath.StrideMeters(h);
            if (w <= 0)
                return $"Stride ≈ {stride:N2} m. Add weight to estimate calories.";
            int per1000 = HealthMath.Calories(1000, h, w);
            return $"Stride ≈ {stride:N2} m · about {per1000} kcal per 1,000 steps.";
        }
    }

    public SettingsViewModel(ISettingsService settings, IBackgroundTracker tracker, IGameDataService data)
    {
        _settings = settings;
        _tracker = tracker;
        _data = data;
    }

    /// <summary>Write the whole save to a JSON file and open the system share sheet.</summary>
    [RelayCommand]
    private async Task ExportDataAsync()
    {
        try
        {
            var backup = await _data.ExportAsync();
            backup.Settings = _settings.ExportSettings();
            var json = BackupSerializer.Serialize(backup);

            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmm");
            var path = Path.Combine(FileSystem.CacheDirectory, $"walktrack-backup-{stamp}.json");
            await File.WriteAllTextAsync(path, json);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Walk Track backup",
                File = new ShareFile(path),
            });
            DataStatus = "Backup file created — save it somewhere safe.";
        }
        catch (Exception ex)
        {
            DataStatus = $"Export failed: {ex.Message}";
        }
    }

    /// <summary>Pick a backup file and restore the whole save from it.</summary>
    [RelayCommand]
    private async Task ImportDataAsync()
    {
        try
        {
            var pick = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choose a Walk Track backup" });
            if (pick is null)
                return;

            var json = await File.ReadAllTextAsync(pick.FullPath);
            var data = BackupSerializer.TryDeserialize(json);
            if (data is null)
            {
                DataStatus = "That file isn't a valid Walk Track backup.";
                return;
            }

            await _data.ImportAsync(data);
            if (data.Settings is not null)
            {
                _settings.ImportSettings(data.Settings);
                Load(); // refresh the toggles/fields on screen
            }
            DataStatus = "Backup restored. Reopen the Town tab to see it.";
        }
        catch (Exception ex)
        {
            DataStatus = $"Import failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public void Load()
    {
        _loading = true;
        HeightText = _settings.HeightCm > 0 ? _settings.HeightCm.ToString() : string.Empty;
        WeightText = _settings.WeightKg > 0 ? _settings.WeightKg.ToString() : string.Empty;
        DailyGoal = _settings.DailyStepGoal;
        BackgroundTracking = _settings.BackgroundTrackingEnabled;
        SoundEnabled = _settings.SoundEnabled;
        HapticsEnabled = _settings.HapticsEnabled;
        RemindersEnabled = _settings.RemindersEnabled;
        TownName = _settings.TownName;
        ReduceMotion = _settings.ReduceMotion;
        _loading = false;
    }

    partial void OnTownNameChanged(string value)
    {
        if (!_loading) _settings.TownName = value;
    }

    partial void OnReduceMotionChanged(bool value)
    {
        if (!_loading) _settings.ReduceMotion = value;
    }

    partial void OnRemindersEnabledChanged(bool value)
    {
        if (!_loading) _settings.RemindersEnabled = value;
    }

    [RelayCommand]
    private static Task OpenPrivacy() => Shell.Current.GoToAsync("privacy");

    partial void OnSoundEnabledChanged(bool value)
    {
        if (!_loading) _settings.SoundEnabled = value;
    }

    partial void OnHapticsEnabledChanged(bool value)
    {
        if (!_loading) _settings.HapticsEnabled = value;
    }

    async partial void OnBackgroundTrackingChanged(bool value)
    {
        if (_loading)
            return;
        _settings.BackgroundTrackingEnabled = value;
        if (value && _tracker.IsSupported)
        {
            await _tracker.EnsureNotificationPermissionAsync();
            _tracker.Start();
        }
        else
        {
            _tracker.Stop();
        }
    }

    partial void OnHeightTextChanged(string value)
    {
        if (!_loading) _settings.HeightCm = ParseOrZero(value);
    }

    partial void OnWeightTextChanged(string value)
    {
        if (!_loading) _settings.WeightKg = ParseOrZero(value);
    }

    partial void OnDailyGoalChanged(double value)
    {
        if (!_loading) _settings.DailyStepGoal = SnappedGoal;
    }

    private static int ParseOrZero(string? s) =>
        int.TryParse(s, out var n) && n > 0 ? n : 0;
}
