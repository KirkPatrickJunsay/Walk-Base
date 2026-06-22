namespace WalkBase.Models;

/// <summary>A complete, portable snapshot of a player's save (exported to / imported from a file).</summary>
public sealed class BackupData
{
    public int Version { get; set; } = 1;
    public string ExportedUtc { get; set; } = string.Empty;

    public PlayerState? Player { get; set; }
    public List<PlacedBuilding> Buildings { get; set; } = new();
    public List<DailyStepRecord> History { get; set; } = new();
    public BackupSettings? Settings { get; set; }
}

/// <summary>The user preferences carried inside a <see cref="BackupData"/>.</summary>
public sealed class BackupSettings
{
    public int DailyStepGoal { get; set; }
    public int HeightCm { get; set; }
    public int WeightKg { get; set; }
    public bool SoundEnabled { get; set; }
    public bool HapticsEnabled { get; set; }
    public bool BackgroundTrackingEnabled { get; set; }
}
