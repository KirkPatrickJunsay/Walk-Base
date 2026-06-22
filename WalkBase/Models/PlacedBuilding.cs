using SQLite;

namespace WalkBase.Models;

/// <summary>An instance of a building placed on the 8×8 iso grid (spec §6).</summary>
public class PlacedBuilding
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>FK to the code-defined <see cref="Building"/> catalog id.</summary>
    public string BuildingId { get; set; } = string.Empty;

    public int GridX { get; set; }
    public int GridY { get; set; }

    /// <summary>Upgrade level, 1–3.</summary>
    public int Level { get; set; } = 1;
}
