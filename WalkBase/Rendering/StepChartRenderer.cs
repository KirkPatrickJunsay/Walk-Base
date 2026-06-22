using SkiaSharp;

namespace WalkBase.Rendering;

/// <summary>A point on the daily-steps bar chart.</summary>
public readonly record struct DayBar(DateTime Date, long Steps);

/// <summary>
/// Draws a themed daily-steps bar chart with a dashed goal line (SkiaSharp).
/// Bars that meet the goal are amber; others green.
/// </summary>
public sealed class StepChartRenderer
{
    private static readonly SKColor Bg = new(0x1B, 0x24, 0x33);        // WbSurface
    private static readonly SKColor Green = new(0x5B, 0xC4, 0x7A);
    private static readonly SKColor Amber = new(0xF2, 0xC8, 0x4B);
    private static readonly SKColor TrackDim = new(0x23, 0x2F, 0x43);  // WbSurface2
    private static readonly SKColor TextDim = new(0x90, 0xA1, 0xB7);

    public void Draw(SKCanvas canvas, SKImageInfo info, IReadOnlyList<DayBar> bars, int goal)
    {
        canvas.Clear(Bg);
        if (bars.Count == 0)
            return;

        float density = info.Width / 360f; // scale text/padding to canvas pixels
        float padL = 8 * density, padR = 8 * density;
        float padTop = 14 * density, padBottom = 20 * density;
        float w = info.Width - padL - padR;
        float h = info.Height - padTop - padBottom;
        float baseY = padTop + h;

        long maxSteps = bars.Max(b => b.Steps);
        double scaleMax = Math.Max(maxSteps, goal) * 1.1;
        if (scaleMax <= 0) scaleMax = goal > 0 ? goal : 1000;

        float slot = w / bars.Count;
        float barW = MathF.Min(slot * 0.6f, 26 * density);

        using var barPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var trackPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = TrackDim };
        using var font = new SKFont { Size = 9 * density };
        using var labelPaint = new SKPaint { IsAntialias = true, Color = TextDim };

        for (int i = 0; i < bars.Count; i++)
        {
            float cx = padL + slot * i + slot / 2f;
            float left = cx - barW / 2f;
            float right = cx + barW / 2f;
            float radius = barW / 2f;

            // Track (full-height faint bar) for rhythm.
            using (var track = new SKRoundRect(new SKRect(left, padTop, right, baseY), radius))
                canvas.DrawRoundRect(track, trackPaint);

            var b = bars[i];
            if (b.Steps > 0)
            {
                float barH = (float)(b.Steps / scaleMax * h);
                float top = baseY - MathF.Max(barH, radius * 1.2f);
                barPaint.Color = goal > 0 && b.Steps >= goal ? Amber : Green;
                using var rr = new SKRoundRect(new SKRect(left, top, right, baseY), radius);
                canvas.DrawRoundRect(rr, barPaint);
            }

            // Day-of-month label every other bar (avoid crowding).
            if (bars.Count <= 8 || i % 2 == bars.Count % 2)
            {
                string lbl = b.Date.Day.ToString();
                float tw = font.MeasureText(lbl);
                canvas.DrawText(lbl, cx - tw / 2f, baseY + 14 * density, SKTextAlign.Left, font, labelPaint);
            }
        }

        // Dashed goal line.
        if (goal > 0 && goal <= scaleMax)
        {
            float gy = baseY - (float)(goal / scaleMax * h);
            using var goalPaint = new SKPaint
            {
                IsAntialias = true,
                Color = Amber.WithAlpha(0xCC),
                StrokeWidth = 1.5f * density,
                Style = SKPaintStyle.Stroke,
                PathEffect = SKPathEffect.CreateDash(new[] { 6 * density, 5 * density }, 0),
            };
            canvas.DrawLine(padL, gy, padL + w, gy, goalPaint);
        }
    }
}
