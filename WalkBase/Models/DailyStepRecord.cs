using SQLite;

namespace WalkBase.Models;

/// <summary>One row per local calendar day the user walked (for the History page).</summary>
public class DailyStepRecord
{
    /// <summary>Local date, yyyy-MM-dd (primary key — one row per day).</summary>
    [PrimaryKey]
    public string Date { get; set; } = string.Empty;

    public long Steps { get; set; }
}
