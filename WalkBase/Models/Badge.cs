namespace WalkBase.Models;

/// <summary>A collectible achievement badge (status only — no Brick reward; see quests for those).</summary>
public sealed record Badge
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Emoji { get; init; }
    public required QuestMetric Metric { get; init; }
    public required long Threshold { get; init; }
}
