using SkiaSharp;

namespace WalkBase.Rendering;

/// <summary>
/// Grid ↔ screen coordinate conversion for a 2:1 isometric diamond projection (spec §11).
/// Tile dimensions are calibrated to the Kenney isometric tile PNGs (width ≈ 2 × height).
/// </summary>
public static class IsoMath
{
    public const float TileWidth = 128f;
    public const float TileHeight = 64f;

    /// <summary>Top-vertex screen position of the diamond for grid cell (gridX, gridY).</summary>
    public static SKPoint GridToScreen(int gridX, int gridY, SKPoint origin) =>
        GridToScreen((float)gridX, (float)gridY, origin);

    /// <summary>Top-vertex screen position for a fractional grid position (e.g. a walker).</summary>
    public static SKPoint GridToScreen(float gridX, float gridY, SKPoint origin)
    {
        float x = origin.X + (gridX - gridY) * (TileWidth / 2f);
        float y = origin.Y + (gridX + gridY) * (TileHeight / 2f);
        return new SKPoint(x, y);
    }

    /// <summary>Inverse of <see cref="GridToScreen"/> — maps a tap point back to a grid cell.</summary>
    public static (int gridX, int gridY) ScreenToGrid(SKPoint screen, SKPoint origin)
    {
        float dx = screen.X - origin.X;
        float dy = screen.Y - origin.Y;
        float a = dx / (TileWidth / 2f);
        float b = dy / (TileHeight / 2f);
        int gridX = (int)MathF.Floor((a + b) / 2f);
        int gridY = (int)MathF.Floor((b - a) / 2f);
        return (gridX, gridY);
    }

    /// <summary>
    /// Centers an <paramref name="gridSize"/>×<paramref name="gridSize"/> base in a canvas
    /// of the given pixel dimensions, leaving headroom at top for tall building sprites.
    /// </summary>
    public static SKPoint ComputeOrigin(int gridSize, float canvasWidth, float canvasHeight)
    {
        float centerX = canvasWidth / 2f;
        // The diamond's vertical extent is gridSize * TileHeight; nudge it down a little.
        float topY = (canvasHeight - gridSize * TileHeight) / 2f + TileHeight;
        return new SKPoint(centerX, MathF.Max(TileHeight, topY));
    }
}
