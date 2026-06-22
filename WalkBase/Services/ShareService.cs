using SkiaSharp;
using WalkBase.Models;
using WalkBase.Rendering;

namespace WalkBase.Services;

/// <summary>Everything the share card needs — a snapshot of the town + walking stats.</summary>
public sealed record TownCardData(
    string TownName, long StepsToday, string DistanceText, int Streak,
    int Population, long Bricks, int GridSize,
    IReadOnlyList<PlacedBuilding> Placed,
    IReadOnlyList<Walker> Walkers, IReadOnlyList<Pet> Pets);

public interface IShareService
{
    /// <summary>Render a shareable town card and hand it to the system share sheet (user-initiated).</summary>
    Task ShareTownAsync(TownCardData data);
}

/// <summary>
/// Builds a polished "my town" image card entirely on-device and shares it via the OS share
/// sheet. Nothing is uploaded — the user chooses where the image goes. No network is used.
/// </summary>
public sealed class ShareService : IShareService
{
    private const int W = 1080, H = 1350;   // 4:5 portrait, social-friendly
    private readonly SpriteCache _sprites;

    public ShareService(SpriteCache sprites) => _sprites = sprites;

    public async Task ShareTownAsync(TownCardData data)
    {
        byte[] png = RenderCard(data);
        var path = Path.Combine(FileSystem.CacheDirectory, "walktrack-town.png");
        await File.WriteAllBytesAsync(path, png);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "My Walk Track town",
            File = new ShareFile(path),
        });
    }

    private byte[] RenderCard(TownCardData d)
    {
        var info = new SKImageInfo(W, H, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        // The town scene (sky + buildings + townsfolk) fills the card. A fresh renderer instance
        // shares the preloaded sprite cache but keeps its own state, so the live view is untouched.
        var renderer = new IsometricRenderer(_sprites);
        renderer.Draw(canvas, info, Math.Max(1, d.GridSize), d.Placed, selected: null,
            scale: 1f, panX: 0f, panY: 30f, showExpansion: false,
            walkers: d.Walkers, pets: d.Pets, visitor: null, festival: false);

        DrawHeader(canvas, d);
        DrawStatsPanel(canvas, d);

        using var img = surface.Snapshot();
        using var enc = img.Encode(SKEncodedImageFormat.Png, 100);
        return enc.ToArray();
    }

    private static readonly SKColor Ink = new(0xEA, 0xF0, 0xF7);
    private static readonly SKColor Dim = new(0x90, 0xA1, 0xB7);
    private static readonly SKColor Amber = new(0xF2, 0xC8, 0x4B);
    private static readonly SKColor Green = new(0x5B, 0xC4, 0x7A);

    private static void DrawHeader(SKCanvas c, TownCardData d)
    {
        using (var scrim = new SKPaint())
        {
            scrim.Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, 270),
                new[] { new SKColor(0x0B, 0x11, 0x20, 0xE0), new SKColor(0x0B, 0x11, 0x20, 0x00) },
                null, SKShaderTileMode.Clamp);
            c.DrawRect(0, 0, W, 270, scrim);
        }

        using var bold = SKTypeface.FromFamilyName(null, SKFontStyle.Bold);
        using var titleFont = new SKFont(bold, 74);
        using var markFont = new SKFont(bold, 32);
        using var paint = new SKPaint { IsAntialias = true };

        paint.Color = Ink;
        c.DrawText(d.TownName, W / 2f, 116, SKTextAlign.Center, titleFont, paint);

        // "Walk Track" wordmark, centred (Walk dim, Track amber).
        float wWalk = markFont.MeasureText("Walk "), wTrack = markFont.MeasureText("Track");
        float startX = W / 2f - (wWalk + wTrack) / 2f;
        paint.Color = Dim;  c.DrawText("Walk ", startX, 172, SKTextAlign.Left, markFont, paint);
        paint.Color = Amber; c.DrawText("Track", startX + wWalk, 172, SKTextAlign.Left, markFont, paint);
    }

    private static void DrawStatsPanel(SKCanvas c, TownCardData d)
    {
        const float top = 1148, h = 162, m = 36;
        using (var panel = new SKPaint { IsAntialias = true, Color = new SKColor(0x12, 0x17, 0x26, 0xF2) })
            c.DrawRoundRect(new SKRect(m, top, W - m, top + h), 30, 30, panel);

        (string val, string label, SKColor color)[] stats =
        {
            ($"{d.StepsToday:N0}", "STEPS TODAY", Ink),
            (d.DistanceText,       "DISTANCE",    Green),
            ($"{d.Streak}",        "DAY STREAK",  Amber),
            ($"{d.Bricks:N0}",     "BRICKS",      Amber),
        };

        using var bold = SKTypeface.FromFamilyName(null, SKFontStyle.Bold);
        using var valFont = new SKFont(bold, 50);
        using var labFont = new SKFont(SKTypeface.Default, 25);
        using var paint = new SKPaint { IsAntialias = true };

        float colW = (W - 2 * m) / stats.Length;
        for (int i = 0; i < stats.Length; i++)
        {
            float cx = m + colW * (i + 0.5f);
            paint.Color = stats[i].color;
            c.DrawText(stats[i].val, cx, top + 78, SKTextAlign.Center, valFont, paint);
            paint.Color = Dim;
            c.DrawText(stats[i].label, cx, top + 120, SKTextAlign.Center, labFont, paint);
        }
    }
}
