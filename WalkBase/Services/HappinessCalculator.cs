using WalkBase.Models;

namespace WalkBase.Services;

/// <summary>A town's mood: a 0–100 score with a friendly label and emoji.</summary>
public readonly record struct Happiness(int Score, string Label, string Emoji);

/// <summary>
/// Pure "town happiness" score from the placed buildings — rewards amenities, upgrades and
/// variety so the catalog feels meaningful. No I/O; unit-tested.
/// </summary>
public static class HappinessCalculator
{
    // Per-building contribution. Amenities (garden, fountain, market…) cheer residents most.
    private static int Weight(string id) => id switch
    {
        "fountain" => 10,
        "town_hall" => 10,
        "market" => 9,
        "garden" => 8,
        "library" => 8,
        "bakery" => 7,
        "well" => 6,
        "house" => 5,
        "workshop" => 4,
        "watchtower" => 4,
        "tent" => 3,
        "path" => 2,
        _ => 3,
    };

    public static Happiness Compute(IEnumerable<PlacedBuilding> placed)
    {
        var list = placed as ICollection<PlacedBuilding> ?? placed.ToList();
        if (list.Count == 0)
            return new Happiness(0, "Empty", "🏚️");

        long pts = 0;
        foreach (var b in list)
            pts += Weight(b.BuildingId) + (b.Level - 1) * 2;   // upgrades add a little

        int distinct = list.Select(b => b.BuildingId).Distinct().Count();
        pts += distinct * 4;                                    // variety bonus

        int score = (int)Math.Clamp(pts, 0, 100);
        var (label, emoji) = score switch
        {
            >= 85 => ("Thriving", "🤩"),
            >= 60 => ("Happy", "😄"),
            >= 35 => ("Content", "🙂"),
            >= 15 => ("Getting by", "😐"),
            _ => ("Struggling", "😟"),
        };
        return new Happiness(score, label, emoji);
    }
}
