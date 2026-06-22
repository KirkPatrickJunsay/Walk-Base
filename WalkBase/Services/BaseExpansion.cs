namespace WalkBase.Services;

/// <summary>Rules for buying land to grow the iso base (spec §11 was a fixed 8×8; now expandable).</summary>
public static class BaseExpansion
{
    /// <summary>Side length a brand-new base starts at.</summary>
    public const int InitialSize = 6;

    /// <summary>Largest side length (keeps the diamond within the canvas at 1× zoom).</summary>
    public const int MaxSize = 10;

    /// <summary>Side length legacy saves (created when the base was a fixed 8×8) migrate to.</summary>
    public const int LegacySize = 8;

    public static bool CanExpand(int currentSize) => currentSize < MaxSize;

    /// <summary>Brick cost to grow from <paramref name="currentSize"/> to the next side length.</summary>
    public static long CostFor(int currentSize) => (currentSize - 5) * 100L;
}
