using SkiaSharp;
using WalkBase.Models;

namespace WalkBase.Rendering;

/// <summary>
/// Paints the iso base and placed buildings to an SKCanvas (spec §11). Draw order is
/// back-to-front by (gridX + gridY) so nearer tiles correctly overlap farther ones.
/// </summary>
public sealed class IsometricRenderer
{
    private readonly SpriteCache _sprites;

    public IsometricRenderer(SpriteCache sprites) => _sprites = sprites;

    // Transform of the most recent Draw — needed to map taps back to the grid.
    private SKPoint _origin;
    private SKPoint _pivot;
    private float _scale = 1f;
    private float _panX, _panY;

    public void Draw(
        SKCanvas canvas,
        SKImageInfo info,
        int gridSize,
        IReadOnlyList<PlacedBuilding> placed,
        (int x, int y)? selected,
        float scale = 1f,
        float panX = 0f,
        float panY = 0f,
        bool showExpansion = false,
        IReadOnlyList<Walker>? walkers = null,
        IReadOnlyList<Pet>? pets = null,
        Visitor? visitor = null,
        bool festival = false)
    {
        var now = DateTime.Now;
        float clock = now.Hour + now.Minute / 60f;
        var season = Seasons.Of(now.Month);
        float glow = NightGlow(clock);   // 0 = daytime, 1 = deep night (windows lit)
        ApplySeasonPalette(season);

        DrawSky(canvas, info, clock); // day/night gradient behind the base (replaces flat clear)

        // Advance the animation clock once per frame (drives smoke, fireflies, rain).
        long ticks = now.Ticks;
        float dt = _lastTicks == 0 ? 0f : (ticks - _lastTicks) / 1e7f;
        _lastTicks = ticks;
        if (dt > 0.1f) dt = 0.1f;
        _time += dt;

        _origin = IsoMath.ComputeOrigin(gridSize, info.Width, info.Height);
        _pivot = new SKPoint(info.Width / 2f, info.Height / 2f);
        _scale = scale;
        _panX = panX;
        _panY = panY;

        canvas.Save();
        canvas.Translate(panX, panY);
        canvas.Translate(_pivot.X, _pivot.Y);
        canvas.Scale(scale);
        canvas.Translate(-_pivot.X, -_pivot.Y);

        // 1) Ground tiles (drawn back-to-front, including the translucent "buyable" ring).
        int drawSize = showExpansion ? gridSize + 1 : gridSize;
        for (int sum = 0; sum <= 2 * (drawSize - 1); sum++)
        {
            for (int gx = 0; gx < drawSize; gx++)
            {
                int gy = sum - gx;
                if (gy < 0 || gy >= drawSize)
                    continue;

                bool isPreview = gx >= gridSize || gy >= gridSize;
                bool isSelected = !isPreview && selected is { } s && s.x == gx && s.y == gy;
                DrawGroundTile(canvas, gx, gy, _origin, isSelected, isPreview);
            }
        }

        // 1b) Rain puddles pooled on the ground (drawn over tiles, under everything else).
        if (_puddle > 0.02f)
            DrawPuddles(canvas, gridSize);

        // 2) Buildings + walkers, merged and drawn back-to-front so people are correctly
        //    occluded by (and pass behind) buildings.
        var entities = new List<(float depth, PlacedBuilding? b, Walker? w, Pet? p, Visitor? v)>();
        foreach (var b in placed)
            entities.Add((b.GridX + b.GridY, b, null, null, null));
        if (walkers is not null)
            foreach (var w in walkers)
                entities.Add((w.X + w.Y, null, w, null, null));
        if (pets is not null)
            foreach (var p in pets)
                entities.Add((p.X + p.Y, null, null, p, null));
        if (visitor is not null)
            entities.Add((visitor.X + visitor.Y, null, null, null, visitor));
        entities.Sort((a, c) => a.depth.CompareTo(c.depth));

        foreach (var (_, b, w, p, v) in entities)
        {
            if (b is not null) DrawBuilding(canvas, b, _origin, glow, _time);
            else if (w is not null) DrawWalker(canvas, w, _origin);
            else if (p is not null) DrawPet(canvas, p, _origin);
            else if (v is not null) DrawVisitor(canvas, v, _origin);
        }

        canvas.Restore();

        // Day/night tint over the whole base (shifts with the real clock).
        var (tintColor, tintAlpha) = DayNightTint(clock);
        if (tintAlpha > 0.001f)
        {
            using var tint = new SKPaint { Color = tintColor.WithAlpha((byte)(tintAlpha * 255)) };
            canvas.DrawRect(0, 0, info.Width, info.Height, tint);
        }

        // Atmospherics on top of everything: weather, rain, fireflies, day creatures, festival.
        UpdateAndDrawEffects(canvas, info, season, glow, dt, festival);
    }

    // Sky gradient (top → bottom) keyframed by hour — drawn behind the base.
    private static readonly (float h, SKColor top, SKColor bot)[] SkyGrad =
    {
        (0f,    new(0x0B, 0x12, 0x28), new(0x18, 0x24, 0x46)),
        (5f,    new(0x0B, 0x12, 0x28), new(0x18, 0x24, 0x46)),
        (6.5f,  new(0x2E, 0x3E, 0x72), new(0xD9, 0x8C, 0x4E)),  // dawn
        (8f,    new(0x35, 0x6B, 0xA8), new(0x9F, 0xC2, 0xDC)),
        (12f,   new(0x32, 0x70, 0xB2), new(0x96, 0xBE, 0xDC)),  // midday
        (17f,   new(0x35, 0x6B, 0xA8), new(0x9F, 0xC2, 0xDC)),
        (18.5f, new(0x4A, 0x3A, 0x72), new(0xE0, 0x7A, 0x42)),  // dusk
        (20f,   new(0x16, 0x1C, 0x3E), new(0x33, 0x2A, 0x52)),
        (21.5f, new(0x0B, 0x12, 0x28), new(0x18, 0x24, 0x46)),
        (24f,   new(0x0B, 0x12, 0x28), new(0x18, 0x24, 0x46)),
    };

    private static void DrawSky(SKCanvas canvas, SKImageInfo info, float t)
    {
        SKColor top = SkyGrad[0].top, bot = SkyGrad[0].bot;
        for (int i = 0; i < SkyGrad.Length - 1; i++)
        {
            var a = SkyGrad[i];
            var b = SkyGrad[i + 1];
            if (t < a.h || t > b.h) continue;
            float f = b.h > a.h ? (t - a.h) / (b.h - a.h) : 0f;
            top = Lerp(a.top, b.top, f);
            bot = Lerp(a.bot, b.bot, f);
            break;
        }
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(0, info.Height),
                new[] { top, bot }, null, SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(new SKRect(0, 0, info.Width, info.Height), paint);
    }

    private static SKColor Lerp(SKColor a, SKColor b, float f) => new(
        (byte)(a.Red + (b.Red - a.Red) * f),
        (byte)(a.Green + (b.Green - a.Green) * f),
        (byte)(a.Blue + (b.Blue - a.Blue) * f));

    // Lit windows ramp on at dusk, stay on through the night, fade out after dawn.
    private static float NightGlow(float t)
    {
        if (t >= 20f || t < 5.5f) return 1f;
        if (t >= 18f) return (t - 18f) / 2f;                  // 18→20 : 0→1
        if (t < 7f) return MathF.Min(1f, (7f - t) / 1.5f);    // 5.5→7 : dawn fade
        return 0f;
    }

    private void ApplySeasonPalette(Season season)
    {
        switch (season)
        {
            case Season.Spring:
                _grassLight = new(0x52, 0x8E, 0x58); _grassDark = new(0x46, 0x80, 0x4E); _grassEdge = new(0x33, 0x5C, 0x3B); break;
            case Season.Autumn:
                _grassLight = new(0x74, 0x6C, 0x3E); _grassDark = new(0x64, 0x5C, 0x37); _grassEdge = new(0x4A, 0x42, 0x28); break;
            case Season.Winter:
                _grassLight = new(0xB0, 0xC0, 0xBA); _grassDark = new(0x9E, 0xB0, 0xAA); _grassEdge = new(0x7E, 0x90, 0x8C); break;
            default: // Summer — the original lush turf
                _grassLight = GrassLight; _grassDark = GrassDark; _grassEdge = GrassEdge; break;
        }
    }

    private sealed class Flake { public float X, Y, Vy, Sway, Phase, Size; public int Tone; }
    private readonly List<Flake> _weather = new();     // seasonal snow / leaves
    private readonly List<Flake> _rainDrops = new();   // passing rain shower
    private readonly List<Flake> _fireflies = new();   // summer-evening fireflies
    private readonly List<Flake> _butterflies = new(); // daytime butterflies
    private readonly List<Flake> _birds = new();       // daytime birds gliding past
    private readonly List<Flake> _confetti = new();    // festival confetti
    private readonly Random _wrng = new();
    private long _lastTicks;
    private float _time;

    private static readonly SKColor[] ButterflyColors =
    {
        new(0xE8, 0x8A, 0x3C), new(0xF2, 0xC8, 0x4B), new(0xE6, 0x7A, 0xB0),
        new(0x6F, 0xB8, 0xE8), new(0xF0, 0xF0, 0xF0),
    };
    private static readonly SKColor[] FlagColors =
    {
        new(0xE8, 0x55, 0x2E), new(0xF2, 0xC8, 0x4B), new(0x5B, 0xC4, 0x7A),
        new(0x4F, 0x9D, 0xE0), new(0xB1, 0x7A, 0xE0), new(0xE6, 0x7A, 0xB0),
    };

    // Passing-rain state machine (independent of season).
    private float _rain;          // current intensity 0..1 (eased)
    private float _rainTarget;    // 0 or 1
    private float _rainHold;      // seconds left in the active shower
    private float _rainCooldown;  // seconds until another shower may start
    private float _puddle;        // wetness on the ground (rises in rain, dries slowly)

    /// <summary>True while a rain shower is active (drives rain ambience).</summary>
    public bool IsRaining => _rain > 0.35f;
    private readonly List<(int gx, int gy)> _puddleSpots = new();
    private int _puddleGrid = -1;

    private static readonly SKColor[] LeafTones =
    {
        new(0xC8, 0x6A, 0x2E), new(0xB5, 0x48, 0x2F), new(0xD9, 0x9A, 0x3A), new(0x8C, 0x5A, 0x2A),
    };

    private Flake NewFlake(SKImageInfo info, bool fromTop) => new()
    {
        X = _wrng.NextSingle() * info.Width,
        Y = fromTop ? -_wrng.NextSingle() * 40f : _wrng.NextSingle() * info.Height,
        Vy = 22f + _wrng.NextSingle() * 42f,
        Sway = 8f + _wrng.NextSingle() * 22f,
        Phase = _wrng.NextSingle() * MathF.Tau,
        Size = 2f + _wrng.NextSingle() * 2.5f,
        Tone = _wrng.Next(LeafTones.Length),
    };

    private void UpdateAndDrawEffects(SKCanvas canvas, SKImageInfo info, Season season, float glow, float dt, bool festival)
    {
        // --- rain state machine: rare shower, any season, with a long cooldown ---
        if (_rainCooldown > 0f) _rainCooldown -= dt;
        if (_rainTarget > 0f)
        {
            _rainHold -= dt;
            if (_rainHold <= 0f) _rainTarget = 0f;       // begin clearing
        }
        else if (_rainCooldown <= 0f && _wrng.NextSingle() < 0.00018f)
        {
            _rainTarget = 1f;
            _rainHold = 16f + _wrng.NextSingle() * 22f;   // shower lasts ~16–38s
            _rainCooldown = 150f + _wrng.NextSingle() * 210f;
        }
        _rain += (_rainTarget - _rain) * MathF.Min(1f, dt * 0.5f);  // ease in/out
        if (_rain < 0.01f) _rain = 0f;

        // Puddles form quickly in rain, then dry out slowly afterwards.
        float puddleRate = _rain > _puddle ? 0.5f : 0.05f;
        _puddle += (_rain - _puddle) * MathF.Min(1f, dt * puddleRate);
        if (_puddle < 0.01f) _puddle = 0f;

        bool raining = _rain > 0.02f;

        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        // --- seasonal weather (faded out while it rains) ---
        float seasonScale = 1f - _rain;
        int wantWeather = (int)((season switch { Season.Winter => 30, Season.Autumn => 20, _ => 0 }) * seasonScale);
        SyncFlakes(_weather, wantWeather, info, fromTop: false);
        bool snow = season == Season.Winter;
        foreach (var f in _weather)
        {
            f.Y += f.Vy * dt;
            f.Phase += dt * 1.6f;
            f.X += MathF.Sin(f.Phase) * f.Sway * dt;
            if (f.Y > info.Height + 8f) Respawn(f, info);
            if (snow)
            {
                paint.Color = new SKColor(0xFF, 0xFF, 0xFF, 0xC8);
                canvas.DrawCircle(f.X, f.Y, f.Size, paint);
            }
            else
            {
                paint.Color = LeafTones[f.Tone];
                canvas.DrawOval(new SKRect(f.X - f.Size, f.Y - f.Size * 0.6f, f.X + f.Size, f.Y + f.Size * 0.6f), paint);
            }
        }

        // --- summer fireflies: clear summer evenings/nights, low over the base ---
        bool fireflyTime = season == Season.Summer && glow > 0.35f && !raining;
        SyncFlakes(_fireflies, fireflyTime ? 12 : 0, info, fromTop: false);
        foreach (var f in _fireflies)
        {
            // slow wander + twinkle
            f.Phase += dt * (1.2f + f.Size * 0.2f);
            f.X += MathF.Cos(f.Phase * 0.7f) * 10f * dt;
            f.Y += MathF.Sin(f.Phase) * 7f * dt;
            float twinkle = 0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(f.Phase * 2.3f));
            byte a = (byte)(twinkle * glow * 230f);
            // soft halo + bright core
            paint.Color = new SKColor(0xC6, 0xE8, 0x6A, (byte)(a * 0.4f));
            canvas.DrawCircle(f.X, f.Y, f.Size + 2.5f, paint);
            paint.Color = new SKColor(0xEA, 0xFF, 0x9A, a);
            canvas.DrawCircle(f.X, f.Y, f.Size * 0.7f, paint);
        }

        // --- passing rain: slight cool darken + slanted streaks ---
        if (raining)
        {
            using (var dim = new SKPaint { Color = new SKColor(0x26, 0x32, 0x44, (byte)(_rain * 60f)) })
                canvas.DrawRect(0, 0, info.Width, info.Height, dim);

            int wantRain = (int)(55 * _rain);
            SyncFlakes(_rainDrops, wantRain, info, fromTop: false);
            using var rainPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.4f,
                StrokeCap = SKStrokeCap.Round,
                Color = new SKColor(0xAE, 0xC4, 0xE0, (byte)(170 * _rain)),
            };
            foreach (var f in _rainDrops)
            {
                f.Y += (520f + f.Vy * 4f) * dt;            // fast fall
                f.X += 80f * dt;                            // wind slant
                if (f.Y > info.Height + 8f) Respawn(f, info);
                canvas.DrawLine(f.X, f.Y, f.X - 5f, f.Y - 14f, rainPaint);
            }
        }
        else if (_rainDrops.Count > 0)
        {
            _rainDrops.Clear();
        }

        // --- daytime creatures: butterflies low over the base, birds gliding past ---
        bool daytime = glow < 0.18f && !raining;
        SyncFlakes(_butterflies, daytime && season != Season.Winter ? 5 : 0, info, fromTop: false);
        foreach (var f in _butterflies)
        {
            f.Phase += dt * 2.2f;
            f.X += MathF.Cos(f.Phase * 0.6f) * 16f * dt;
            f.Y += (MathF.Sin(f.Phase) * 9f + MathF.Sin(f.Phase * 4f) * 4f) * dt; // bobbing flutter
            if (f.X < -12f) f.X = info.Width + 12f; else if (f.X > info.Width + 12f) f.X = -12f;
            if (f.Y < info.Height * 0.18f) f.Y = info.Height * 0.18f;
            if (f.Y > info.Height - 8f) f.Y = info.Height - 8f;
            DrawButterfly(canvas, paint, f);
        }

        if (daytime)
        {
            while (_birds.Count < 3)
            {
                var b = NewFlake(info, fromTop: false);
                b.X = -_wrng.NextSingle() * info.Width;
                b.Y = info.Height * (0.05f + _wrng.NextSingle() * 0.20f);
                _birds.Add(b);
            }
        }
        else _birds.Clear();
        using (var birdPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.8f, StrokeCap = SKStrokeCap.Round, Color = new SKColor(0x3A, 0x44, 0x52, 0xC0) })
            foreach (var f in _birds)
            {
                f.X += (46f + f.Size * 8f) * dt;
                f.Phase += dt * 5f;
                if (f.X > info.Width + 20f) { f.X = -20f; f.Y = info.Height * (0.05f + _wrng.NextSingle() * 0.20f); }
                float flap = 2.5f + MathF.Sin(f.Phase) * 2f;
                canvas.DrawLine(f.X - 6f, f.Y, f.X, f.Y - flap, birdPaint);
                canvas.DrawLine(f.X, f.Y - flap, f.X + 6f, f.Y, birdPaint);
            }

        // --- festival: season-themed bunting garland, title + colourful confetti ---
        if (festival)
        {
            var flags = FestivalFlags(season);
            DrawBunting(canvas, info, _time, flags);
            DrawFestivalTitle(canvas, info, season);
            while (_confetti.Count < 40) _confetti.Add(NewFlake(info, fromTop: false));
            foreach (var f in _confetti)
            {
                f.Y += (60f + f.Vy) * dt;
                f.Phase += dt * 3f;
                f.X += MathF.Sin(f.Phase) * f.Sway * dt;
                if (f.Y > info.Height + 8f) Respawn(f, info);
                paint.Color = flags[f.Tone % flags.Length];
                float s = f.Size;
                canvas.DrawOval(new SKRect(f.X - s, f.Y - s * 0.5f, f.X + s, f.Y + s * 0.5f), paint);
            }
        }
        else if (_confetti.Count > 0) _confetti.Clear();
    }

    private static SKColor[] FestivalFlags(Season s) => s switch
    {
        Season.Winter => new[] { new SKColor(0xF0, 0xF6, 0xFF), new SKColor(0x9C, 0xC8, 0xE8), new SKColor(0x6F, 0xA8, 0xD8), new SKColor(0xC8, 0xE0, 0xF0) },
        Season.Spring => new[] { new SKColor(0xF2, 0x9F, 0xC4), new SKColor(0x8E, 0xD0, 0x88), new SKColor(0xF2, 0xC8, 0x4B), new SKColor(0xC9, 0xA6, 0xE6) },
        Season.Autumn => new[] { new SKColor(0xE0, 0x6A, 0x2E), new SKColor(0xF0, 0xB0, 0x3A), new SKColor(0xB5, 0x48, 0x2F), new SKColor(0x8C, 0x6A, 0x2A) },
        _ => FlagColors, // Summer — bright multicolour
    };

    private static string FestivalTitle(Season s) => s switch
    {
        Season.Winter => "WINTER FESTIVAL",
        Season.Spring => "BLOSSOM FESTIVAL",
        Season.Autumn => "HARVEST FESTIVAL",
        _ => "SUMMER FAIR",
    };

    private static void DrawFestivalTitle(SKCanvas canvas, SKImageInfo info, Season season)
    {
        float cx = info.Width / 2f;
        float y = info.Height * 0.15f + 64f;   // just below the garland's sag
        using var font = new SKFont(SKTypeface.FromFamilyName(null, SKFontStyle.Bold), 26f);
        using var shadow = new SKPaint { IsAntialias = true, Color = new SKColor(0, 0, 0, 0xAA) };
        using var text = new SKPaint { IsAntialias = true, Color = new SKColor(0xF7, 0xF2, 0xE4) };
        string title = FestivalTitle(season);
        canvas.DrawText(title, cx + 1.5f, y + 1.5f, SKTextAlign.Center, font, shadow);
        canvas.DrawText(title, cx, y, SKTextAlign.Center, font, text);
    }

    private static void DrawButterfly(SKCanvas canvas, SKPaint paint, Flake f)
    {
        float open = 0.35f + 0.65f * MathF.Abs(MathF.Sin(f.Phase * 6f)); // wing flap
        float wsz = (4f + f.Size) * open;
        var col = ButterflyColors[f.Tone % ButterflyColors.Length];
        paint.Color = col;
        canvas.DrawOval(new SKRect(f.X - wsz, f.Y - 3.4f, f.X - 0.3f, f.Y + 3.4f), paint); // left wings
        canvas.DrawOval(new SKRect(f.X + 0.3f, f.Y - 3.4f, f.X + wsz, f.Y + 3.4f), paint);  // right wings
        paint.Color = new SKColor(0x2A, 0x22, 0x1A);
        canvas.DrawRect(new SKRect(f.X - 0.5f, f.Y - 2.6f, f.X + 0.5f, f.Y + 2.6f), paint);  // body
    }

    private void DrawBunting(SKCanvas canvas, SKImageInfo info, float time, SKColor[] flags)
    {
        float w = info.Width;
        float topY = info.Height * 0.15f;
        float sag = 44f;
        float Y(float t) => topY + sag * (1f - (2f * t - 1f) * (2f * t - 1f)); // sags in the middle

        using (var cord = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, Color = new SKColor(0xEC, 0xEC, 0xEC, 0xB0) })
        using (var path = new SKPath())
        {
            path.MoveTo(0, Y(0));
            for (int i = 1; i <= 40; i++) { float t = i / 40f; path.LineTo(t * w, Y(t)); }
            canvas.DrawPath(path, cord);
        }

        using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        const int n = 14;
        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f) / n;
            float x = t * w, y = Y(t);
            float flutter = MathF.Sin(time * 2f + i) * 2f;
            fill.Color = flags[i % flags.Length];
            using var tri = Tri(new SKPoint(x - 7f, y), new SKPoint(x + 7f, y), new SKPoint(x + flutter, y + 17f));
            canvas.DrawPath(tri, fill);
        }
    }

    private void SyncFlakes(List<Flake> list, int want, SKImageInfo info, bool fromTop)
    {
        while (list.Count < want) list.Add(NewFlake(info, fromTop));
        while (list.Count > want) list.RemoveAt(list.Count - 1);
    }

    private void Respawn(Flake f, SKImageInfo info)
    {
        var n = NewFlake(info, fromTop: true);
        f.X = n.X; f.Y = n.Y; f.Vy = n.Vy; f.Sway = n.Sway; f.Phase = n.Phase; f.Size = n.Size; f.Tone = n.Tone;
    }

    // Sky tint control points: (hour, color, alpha). Lerped to fake dawn/day/dusk/night.
    private static readonly (float h, SKColor c, float a)[] SkyStops =
    {
        (0f,    new(0x0A, 0x14, 0x30), 0.42f),
        (5f,    new(0x0A, 0x14, 0x30), 0.40f),
        (6.5f,  new(0xF0, 0x9A, 0x4A), 0.26f),  // sunrise warm
        (8f,    new(0xF0, 0x9A, 0x4A), 0.08f),
        (10f,   new(0x00, 0x00, 0x00), 0.00f),  // daytime — no tint
        (16f,   new(0x00, 0x00, 0x00), 0.00f),
        (18f,   new(0xE8, 0x6A, 0x3A), 0.20f),  // dusk
        (19.5f, new(0xC8, 0x4A, 0x5A), 0.30f),  // sunset
        (21f,   new(0x0A, 0x14, 0x30), 0.40f),
        (24f,   new(0x0A, 0x14, 0x30), 0.42f),
    };

    private static (SKColor color, float alpha) DayNightTint(float t)
    {
        for (int i = 0; i < SkyStops.Length - 1; i++)
        {
            var a = SkyStops[i];
            var b = SkyStops[i + 1];
            if (t < a.h || t > b.h)
                continue;
            float f = b.h > a.h ? (t - a.h) / (b.h - a.h) : 0f;
            var c = new SKColor(
                (byte)(a.c.Red + (b.c.Red - a.c.Red) * f),
                (byte)(a.c.Green + (b.c.Green - a.c.Green) * f),
                (byte)(a.c.Blue + (b.c.Blue - a.c.Blue) * f));
            return (c, a.a + (b.a - a.a) * f);
        }
        return (SKColors.Black, 0f);
    }

    /// <summary>Map a screen-pixel tap (post-zoom/pan) back to a grid cell.</summary>
    public (int gx, int gy) GridFromScreen(SKPoint screen)
    {
        // Invert: screen = pan + pivot + (world - pivot) * scale
        var world = new SKPoint(
            (screen.X - _panX - _pivot.X) / _scale + _pivot.X,
            (screen.Y - _panY - _pivot.Y) / _scale + _pivot.Y);
        return IsoMath.ScreenToGrid(world, _origin);
    }

    // Grass palette: two shades in a checker so the field reads as tiled turf.
    private static readonly SKColor GrassLight = new(0x46, 0x7A, 0x52);
    private static readonly SKColor GrassDark = new(0x3C, 0x6B, 0x49);
    private static readonly SKColor GrassSelected = new(0x6E, 0xA8, 0x6A);
    private static readonly SKColor GrassEdge = new(0x2C, 0x4F, 0x37);

    // Live palette — swapped per season each Draw (defaults to summer turf).
    private SKColor _grassLight = GrassLight, _grassDark = GrassDark, _grassEdge = GrassEdge;

    private static readonly SKColor PreviewFill = new(0x5B, 0xC4, 0x7A, 0x40);  // translucent green
    private static readonly SKColor PreviewEdge = new(0x5B, 0xC4, 0x7A, 0xB0);  // dashed green outline

    private void DrawGroundTile(SKCanvas canvas, int gx, int gy, SKPoint origin, bool selected, bool preview = false)
    {
        var top = IsoMath.GridToScreen(gx, gy, origin);
        using var path = DiamondPath(top);

        if (preview)
        {
            // Ghosted "available land" — translucent fill + dashed outline (tap to buy).
            using var pfill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = PreviewFill };
            using var pstroke = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
                Color = PreviewEdge,
                PathEffect = SKPathEffect.CreateDash(new[] { 8f, 6f }, 0),
            };
            canvas.DrawPath(path, pfill);
            canvas.DrawPath(path, pstroke);
            return;
        }

        using var fill = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = selected ? GrassSelected : ((gx + gy) % 2 == 0 ? _grassLight : _grassDark),
        };
        using var stroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.25f,
            Color = _grassEdge,
        };
        canvas.DrawPath(path, fill);
        canvas.DrawPath(path, stroke);
    }

    private void DrawPuddles(SKCanvas canvas, int gridSize)
    {
        if (_puddleGrid != gridSize)   // pick stable random tiles once per base size
        {
            _puddleGrid = gridSize;
            _puddleSpots.Clear();
            int n = Math.Clamp(gridSize, 5, 8);
            for (int i = 0; i < n; i++)
                _puddleSpots.Add((_wrng.Next(gridSize), _wrng.Next(gridSize)));
        }

        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        float hw = IsoMath.TileWidth * 0.30f, hh = IsoMath.TileHeight * 0.30f;
        foreach (var (gx, gy) in _puddleSpots)
        {
            var t = IsoMath.GridToScreen(gx, gy, _origin);
            float cxp = t.X, cyp = t.Y + IsoMath.TileHeight / 2f;
            paint.Color = new SKColor(0x24, 0x3A, 0x57, (byte)(_puddle * 150f));
            canvas.DrawOval(new SKRect(cxp - hw, cyp - hh, cxp + hw, cyp + hh), paint);
            paint.Color = new SKColor(0x9A, 0xBC, 0xD4, (byte)(_puddle * 55f));   // sky glint
            canvas.DrawOval(new SKRect(cxp - hw * 0.55f, cyp - hh * 0.55f, cxp + hw * 0.1f, cyp - hh * 0.05f), paint);
        }
    }

    private void DrawBuilding(SKCanvas canvas, PlacedBuilding b, SKPoint origin, float glow, float time)
    {
        var def = BuildingCatalogLookup(b.BuildingId);
        var top = IsoMath.GridToScreen(b.GridX, b.GridY, origin);

        var bmp = _sprites.Get(def?.SpriteForLevel(b.Level));
        if (bmp is not null)
        {
            // Anchor the sprite so its base diamond sits on the tile (bottom = tile's front vertex).
            float w = IsoMath.TileWidth;
            float h = bmp.Height * (w / bmp.Width);
            var dest = new SKRect(
                top.X - w / 2f,
                top.Y + IsoMath.TileHeight - h,
                top.X + w / 2f,
                top.Y + IsoMath.TileHeight);

            // Foliage sways gently in the breeze; each plant offset by its grid position so they
            // don't all lean together. The skew is anchored at the base, so trunks stay planted.
            if (FoliageBuildings.Contains(def!.Id))
            {
                float phase = b.GridX * 0.7f + b.GridY * 1.3f;
                float kx = 0.05f * MathF.Sin(time * 1.5f + phase);
                canvas.Save();
                canvas.Translate(dest.MidX, dest.Bottom);
                canvas.Skew(kx, 0f);
                canvas.Translate(-dest.MidX, -dest.Bottom);
                canvas.DrawBitmap(bmp, dest);
                canvas.Restore();
            }
            else
            {
                canvas.DrawBitmap(bmp, dest);
            }

            // Water features ripple and sparkle on their surface.
            if (WaterBuildings.TryGetValue(def.Id, out float surfaceFrac))
                DrawFountainWater(canvas, dest, time, b.GridX + b.GridY, surfaceFrac);

            // Sprite buildings join the day/night cycle: windows light up + chimneys smoke at night.
            if (glow > 0.02f)
                DrawSpriteNightLights(canvas, dest, def.Id, b.Level, glow, time);
            return;
        }

        // No sprite bundled — draw a designed vector building per type.
        DrawDesignedBuilding(canvas, top, def, b.Level, glow, time);
    }

    // Plants that sway in the breeze.
    private static readonly HashSet<string> FoliageBuildings = new() { "garden", "hedge" };

    // Ripple rings + sparkle glints over a water feature's surface (surfaceFrac = height up from base).
    private static void DrawFountainWater(SKCanvas canvas, SKRect dest, float time, float seed, float surfaceFrac = 0.73f)
    {
        float cx = dest.MidX;
        float cy = dest.Bottom - dest.Height * surfaceFrac; // centre of the iso top diamond
        float baseR = dest.Width * 0.18f;

        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke };
        for (int i = 0; i < 3; i++)                          // concentric ripples expanding outward
        {
            float p = (time * 0.5f + i / 3f + seed * 0.13f) % 1f;
            float r = baseR * (0.25f + p * 1.0f);
            float a = (1f - p) * 0.8f;
            paint.StrokeWidth = 1.6f;
            paint.Color = new SKColor(0xE6, 0xF6, 0xFF, (byte)(a * 230f));
            canvas.DrawOval(new SKRect(cx - r, cy - r * 0.5f, cx + r, cy + r * 0.5f), paint);  // iso-flattened
        }

        paint.Style = SKPaintStyle.Fill;
        for (int i = 0; i < 3; i++)                          // twinkling highlights on the surface
        {
            float tw = 0.5f + 0.5f * MathF.Sin(time * 3.2f + i * 2.1f + seed);
            float gx = cx + (i - 1) * dest.Width * 0.13f;
            float gy = cy + (i % 2 == 0 ? -1f : 1f) * dest.Height * 0.03f;
            paint.Color = new SKColor(0xFF, 0xFF, 0xFF, (byte)(tw * 220f));
            canvas.DrawCircle(gx, gy, 1.0f + tw * 1.0f, paint);
        }
    }

    // Buildings whose windows glow warm after dark (inhabited / working structures).
    private static readonly HashSet<string> LitBuildings = new()
    { "house", "town_hall", "farm", "bakery", "workshop", "market", "library", "watchtower",
      "cottage", "cafe", "chapel", "manor" };
    // A subset with hearths/ovens that also send up chimney smoke.
    private static readonly HashSet<string> HearthBuildings = new()
    { "house", "town_hall", "farm", "bakery", "workshop", "cottage", "cafe", "manor" };
    // Water features whose surface ripples and sparkles. Value = surface height (fraction up from base).
    private static readonly Dictionary<string, float> WaterBuildings = new()
    { ["fountain"] = 0.73f, ["pond"] = 0.52f };

    // Overlay warm window panes (with a soft halo) and chimney smoke onto a sprite building at night.
    private static void DrawSpriteNightLights(SKCanvas canvas, SKRect dest, string id, int level, float glow, float time)
    {
        if (!LitBuildings.Contains(id)) return;

        float w = dest.Width, h = dest.Height, midX = dest.MidX;
        int rows = id == "watchtower" ? 3 : Math.Clamp(level, 1, 3);   // one window row per storey
        bool single = id == "watchtower";                              // the tower is a narrow single column

        using var paint = new SKPaint { IsAntialias = true };
        for (int r = 0; r < rows; r++)
        {
            float wy = dest.Bottom - h * (0.15f + r * 0.17f);          // rows climb the building body
            for (int k = single ? 0 : -1; k <= 1; k += 2)
            {
                float wx = midX + k * w * 0.17f;
                paint.Shader = SKShader.CreateRadialGradient(
                    new SKPoint(wx, wy), w * 0.17f,
                    new[] { new SKColor(0xFF, 0xD6, 0x78, (byte)(glow * 120f)), new SKColor(0xFF, 0xD6, 0x78, 0) },
                    null, SKShaderTileMode.Clamp);
                canvas.DrawCircle(wx, wy, w * 0.17f, paint);
                paint.Shader = null;

                paint.Color = new SKColor(0xFF, 0xE2, 0x95, (byte)(110 + glow * 120f));
                canvas.DrawRoundRect(new SKRect(wx - w * 0.045f, wy - h * 0.032f, wx + w * 0.045f, wy + h * 0.032f), 1.5f, 1.5f, paint);
                if (single) break;
            }
        }

        if (glow > 0.25f && HearthBuildings.Contains(id))
            DrawChimneySmoke(canvas, new SKPoint(midX - w * 0.18f, dest.Top + h * 0.14f), glow, time);
    }

    private enum Shape { House, Tent, Paver, Well, Garden, Lamp, Flowers, Hedge }

    private readonly record struct BuildingStyle(
        Shape Shape, SKColor Wall, SKColor Roof, float Footprint, float WallHeight, float RoofHeight);

    // Hand-picked look per building so each reads as a distinct structure.
    private static BuildingStyle StyleFor(string id) => id switch
    {
        "town_hall" => new(Shape.House, new(0xEA, 0xDB, 0xB5), new(0xB5, 0x48, 0x2F), 0.92f, 60f, 44f),
        "tent"      => new(Shape.Tent,  new(0x3D, 0xA0, 0xA0), new(0x2F, 0x86, 0x86), 0.80f, 0f, 66f),
        "path"      => new(Shape.Paver, new(0x8B, 0x90, 0x98), new(0x6E, 0x74, 0x7C), 0.96f, 10f, 0f),
        "house"     => new(Shape.House, new(0xD8, 0xB9, 0x8C), new(0x8C, 0x5A, 0x3C), 0.82f, 48f, 34f),
        "well"      => new(Shape.Well,  new(0x9B, 0xA1, 0xA9), new(0x2E, 0x5A, 0x86), 0.60f, 26f, 0f),
        "farm"      => new(Shape.House, new(0xC4, 0x55, 0x40), new(0x6E, 0x4D, 0x36), 0.90f, 40f, 28f),
        "workshop"  => new(Shape.House, new(0x8C, 0x97, 0xA3), new(0x45, 0x51, 0x60), 0.86f, 54f, 30f),
        "watchtower"=> new(Shape.House, new(0xAE, 0xB4, 0xBC), new(0x4A, 0x55, 0x60), 0.54f, 94f, 50f),
        "market"    => new(Shape.House, new(0xE0, 0xC0, 0x89), new(0xD2, 0x4F, 0x4F), 0.96f, 34f, 24f),
        "bakery"    => new(Shape.House, new(0xEA, 0xD8, 0xB0), new(0x9C, 0x5A, 0x33), 0.84f, 46f, 32f),
        "library"   => new(Shape.House, new(0x6E, 0x7C, 0xA6), new(0x39, 0x42, 0x5C), 0.86f, 56f, 30f),
        "fountain"  => new(Shape.Well,  new(0xB0, 0xB6, 0xBE), new(0x3D, 0x7A, 0xB0), 0.62f, 22f, 0f),
        "garden"    => new(Shape.Garden, new(0x4E, 0x8C, 0x46), new(0x6E, 0x4A, 0x2E), 0.70f, 0f, 0f),
        "hedge"     => new(Shape.Hedge,  new(0x3E, 0x6E, 0x3A), new(0x4E, 0x86, 0x46), 0.86f, 11f, 0f),
        "flower_bed"=> new(Shape.Flowers, new(0x5A, 0x42, 0x30), new(0x4E, 0x86, 0x46), 0.80f, 0f, 0f),
        "lamppost"  => new(Shape.Lamp,   new(0x3A, 0x42, 0x4A), new(0xFF, 0xE6, 0x9C), 0.40f, 30f, 0f),
        // New buildings (sprite-backed; these are vector fallbacks only).
        "cottage"   => new(Shape.House, new(0xE6, 0xCF, 0xA6), new(0x9C, 0x6A, 0x44), 0.78f, 40f, 30f),
        "cafe"      => new(Shape.House, new(0xEA, 0xD8, 0xB0), new(0x6E, 0x8A, 0x4E), 0.84f, 44f, 26f),
        "chapel"    => new(Shape.House, new(0xE0, 0xDA, 0xCE), new(0xC9, 0x9A, 0x55), 0.70f, 52f, 46f),
        "manor"     => new(Shape.House, new(0xD8, 0xC0, 0x9C), new(0x6E, 0x4A, 0x36), 0.90f, 60f, 36f),
        "pond"      => new(Shape.Well,  new(0xB0, 0xB6, 0xBE), new(0x3D, 0x7A, 0xB0), 0.66f, 8f, 0f),
        "monument"  => new(Shape.Well,  new(0xB8, 0xAE, 0x9E), new(0x8A, 0x7C, 0x66), 0.46f, 40f, 0f),
        _           => new(Shape.House, ColorFor(id), new(0x55, 0x44, 0x55), 0.80f, 46f, 32f),
    };

    private static void DrawDesignedBuilding(SKCanvas canvas, SKPoint top, Building? def, int level, float glow, float time)
    {
        var style = StyleFor(def?.Id ?? "");
        // Center of the tile diamond (the building's footprint sits here).
        var c = new SKPoint(top.X, top.Y + IsoMath.TileHeight / 2f);
        float wallH = style.WallHeight * (1f + 0.16f * (level - 1)); // taller per upgrade

        DrawShadow(canvas, c, style.Footprint);

        switch (style.Shape)
        {
            case Shape.Paver: DrawSlab(canvas, c, style); break;
            case Shape.Tent: DrawPyramid(canvas, c, style.Footprint, style.RoofHeight, style.Wall, 0f); break;
            case Shape.Well: DrawWell(canvas, c, style, wallH); break;
            case Shape.Garden: DrawGarden(canvas, c, style, level); break;
            case Shape.Hedge: DrawHedge(canvas, c, style); break;
            case Shape.Flowers: DrawFlowerBed(canvas, c, style); break;
            case Shape.Lamp: DrawLampPost(canvas, c, glow); break;
            default: DrawHouse(canvas, c, style, wallH, glow, level); break;
        }

        // Chimney smoke curls up from lit houses at night.
        if (glow > 0.25f && style.Shape == Shape.House)
            DrawChimneySmoke(canvas, new SKPoint(c.X, c.Y - wallH - style.RoofHeight), glow, time);

        // Lantern posts glow over paths after dark.
        if (glow > 0.25f && style.Shape == Shape.Paver)
            DrawPathLamp(canvas, c, glow);

        DrawLevelPips(canvas, c, wallH + style.RoofHeight, level);
    }

    // A little lamp post with a warm glowing orb — lights paths at night.
    private static void DrawPathLamp(SKCanvas canvas, SKPoint c, float glow)
    {
        float postH = 22f;
        var baseP = new SKPoint(c.X + IsoMath.TileWidth * 0.22f, c.Y - 2f);
        var orb = new SKPoint(baseP.X, baseP.Y - postH);

        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        // post
        paint.Color = new SKColor(0x3A, 0x33, 0x2A);
        canvas.DrawRect(new SKRect(baseP.X - 1.1f, orb.Y, baseP.X + 1.1f, baseP.Y), paint);
        // warm halo
        using (var halo = new SKPaint { IsAntialias = true })
        {
            halo.Shader = SKShader.CreateRadialGradient(
                orb, 14f,
                new[] { new SKColor(0xFF, 0xCE, 0x6E, (byte)(glow * 150f)), new SKColor(0xFF, 0xCE, 0x6E, 0) },
                null, SKShaderTileMode.Clamp);
            canvas.DrawCircle(orb.X, orb.Y, 14f, halo);
        }
        // orb
        paint.Color = new SKColor(0xFF, 0xE6, 0x9C, (byte)(160 + glow * 95f));
        canvas.DrawCircle(orb.X, orb.Y, 2.6f, paint);
    }

    // Three soft puffs that rise, drift and fade from the roof apex.
    private static void DrawChimneySmoke(SKCanvas canvas, SKPoint apex, float glow, float time)
    {
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        const int puffs = 3;
        for (int i = 0; i < puffs; i++)
        {
            float phase = (time * 0.35f + i / (float)puffs) % 1f;   // 0 → 1 over the cycle
            float rise = phase * 34f;
            float drift = MathF.Sin(phase * 3.4f + i) * 6f;
            float r = 2.6f + phase * 5.5f;
            float fade = (1f - phase) * 0.5f * glow;
            paint.Color = new SKColor(0xD8, 0xDE, 0xE6, (byte)(fade * 255f));
            canvas.DrawCircle(apex.X + drift - 1.5f, apex.Y - 4f - rise, r, paint);
        }
    }

    // Footprint diamond corners around center c, scaled by f. Returns (top,right,bottom,left).
    private static (SKPoint t, SKPoint r, SKPoint b, SKPoint l) Footprint(SKPoint c, float f)
    {
        float hw = IsoMath.TileWidth / 2f * f;
        float hh = IsoMath.TileHeight / 2f * f;
        return (new(c.X, c.Y - hh), new(c.X + hw, c.Y), new(c.X, c.Y + hh), new(c.X - hw, c.Y));
    }

    private static SKPoint Up(SKPoint p, float h) => new(p.X, p.Y - h);

    private static SKPath Quad(SKPoint a, SKPoint b, SKPoint cc, SKPoint d)
    {
        var p = new SKPath();
        p.MoveTo(a); p.LineTo(b); p.LineTo(cc); p.LineTo(d); p.Close();
        return p;
    }

    private static SKPath Tri(SKPoint a, SKPoint b, SKPoint cc)
    {
        var p = new SKPath();
        p.MoveTo(a); p.LineTo(b); p.LineTo(cc); p.Close();
        return p;
    }

    private static void Fill(SKCanvas canvas, SKPath path, SKColor color)
    {
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = color };
        canvas.DrawPath(path, paint);
    }

    // Subtle warm-dark edge so building faces read as crisply "drawn".
    private static readonly SKColor EdgeColor = new(0x20, 0x18, 0x12, 0x4D);

    /// <summary>Fill a building face with a soft top-lit gradient and a thin outline.</summary>
    private static void FillFace(SKCanvas canvas, SKPath path, SKColor color, float topF = 1.09f, float botF = 0.88f)
    {
        var b = path.Bounds;
        using (var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill })
        {
            paint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, b.Top), new SKPoint(0, b.Bottom),
                new[] { Lighten(color, topF), Darken(color, botF) },
                null, SKShaderTileMode.Clamp);
            canvas.DrawPath(path, paint);
        }
        using var edge = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, Color = EdgeColor };
        canvas.DrawPath(path, edge);
    }

    private static void DrawShadow(SKCanvas canvas, SKPoint c, float f)
    {
        using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(0, 0, 0, 60) };
        canvas.DrawOval(new SKRect(
            c.X - IsoMath.TileWidth / 2f * f, c.Y - IsoMath.TileHeight / 2f * f,
            c.X + IsoMath.TileWidth / 2f * f, c.Y + IsoMath.TileHeight / 2f * f), paint);
    }

    // Two visible walls (front-left, front-right) rising from the footprint.
    private static void DrawWalls(SKCanvas canvas, (SKPoint t, SKPoint r, SKPoint b, SKPoint l) fp, float h, SKColor wall)
    {
        using (var left = Quad(fp.l, fp.b, Up(fp.b, h), Up(fp.l, h)))
            FillFace(canvas, left, Darken(wall, 0.66f));   // shaded side
        using (var right = Quad(fp.b, fp.r, Up(fp.r, h), Up(fp.b, h)))
            FillFace(canvas, right, Darken(wall, 0.86f));  // lit side
    }

    private static void DrawHouse(SKCanvas canvas, SKPoint c, BuildingStyle s, float wallH, float glow = 0f, int level = 1)
    {
        var fp = Footprint(c, s.Footprint);
        DrawWalls(canvas, fp, wallH, s.Wall);

        // Door on the front-left wall.
        var dl = new SKPoint(fp.l.X + (fp.b.X - fp.l.X) * 0.45f, fp.l.Y + (fp.b.Y - fp.l.Y) * 0.45f);
        var dr = new SKPoint(fp.l.X + (fp.b.X - fp.l.X) * 0.72f, fp.l.Y + (fp.b.Y - fp.l.Y) * 0.72f);
        float doorH = wallH * 0.55f;
        using (var door = Quad(dl, dr, Up(dr, doorH), Up(dl, doorH)))
            Fill(canvas, door, new SKColor(0x3A, 0x26, 0x18));

        // Upgrades add windows on the front-right wall: L2 = one, L3+ = two.
        if (level == 2)
            DrawWallWindow(canvas, fp, wallH, 0.50f);
        else if (level >= 3)
        {
            DrawWallWindow(canvas, fp, wallH, 0.34f);
            DrawWallWindow(canvas, fp, wallH, 0.66f);
        }

        // Warm lantern-lit window on the front-right wall after dark.
        if (glow > 0.02f)
            DrawLitWindow(canvas, fp, wallH, glow);

        // Hip roof: pyramid from the wall-top corners to an apex.
        DrawPyramid(canvas, c, s.Footprint, s.RoofHeight, s.Roof, wallH);

        // A pennant flag crowns a fully-upgraded building.
        if (level >= 3)
            DrawRoofFlag(canvas, new SKPoint(c.X, c.Y - wallH - s.RoofHeight));
    }

    // A small framed window pane on the front-right wall (decorative, always visible).
    private static void DrawWallWindow(SKCanvas canvas, (SKPoint t, SKPoint r, SKPoint b, SKPoint l) fp, float wallH, float along)
    {
        float wx = fp.b.X + (fp.r.X - fp.b.X) * along;
        float wy = fp.b.Y + (fp.r.Y - fp.b.Y) * along - wallH * 0.55f;
        var edge = new SKPoint(fp.r.X - fp.b.X, fp.r.Y - fp.b.Y);
        float len = MathF.Sqrt(edge.X * edge.X + edge.Y * edge.Y);
        var u = new SKPoint(edge.X / len, edge.Y / len);
        SKPoint P(float a, float up) => new(wx + u.X * a, wy + u.Y * a - up);

        const float hw = 3.4f, hh = 4.6f;
        using (var frame = Quad(P(-hw - 1f, hh + 1f), P(hw + 1f, hh + 1f), P(hw + 1f, -hh - 1f), P(-hw - 1f, -hh - 1f)))
            Fill(canvas, frame, new SKColor(0xE6, 0xDE, 0xC8));
        using (var glass = Quad(P(-hw, hh), P(hw, hh), P(hw, -hh), P(-hw, -hh)))
            Fill(canvas, glass, new SKColor(0x34, 0x4A, 0x66));
    }

    private static void DrawRoofFlag(SKCanvas canvas, SKPoint apex)
    {
        using var pole = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = new SKColor(0x4A, 0x42, 0x38) };
        canvas.DrawLine(apex.X, apex.Y, apex.X, apex.Y - 13f, pole);
        using var tri = Tri(new SKPoint(apex.X, apex.Y - 13f), new SKPoint(apex.X + 9f, apex.Y - 10f), new SKPoint(apex.X, apex.Y - 7f));
        Fill(canvas, tri, new SKColor(0xE8, 0x55, 0x2E));
    }

    // A glowing window pane (with a soft halo) on the front-right wall — reads as a lit lantern.
    private static void DrawLitWindow(SKCanvas canvas, (SKPoint t, SKPoint r, SKPoint b, SKPoint l) fp, float wallH, float glow)
    {
        // Centre of the window: ~half-way along the b→r wall edge, ~55% up the wall.
        float wx = fp.b.X + (fp.r.X - fp.b.X) * 0.52f;
        float wy = fp.b.Y + (fp.r.Y - fp.b.Y) * 0.52f - wallH * 0.52f;

        // Soft warm halo.
        using (var halo = new SKPaint { IsAntialias = true })
        {
            halo.Shader = SKShader.CreateRadialGradient(
                new SKPoint(wx, wy), 17f,
                new[] { new SKColor(0xFF, 0xD8, 0x7A, (byte)(glow * 150f)), new SKColor(0xFF, 0xD8, 0x7A, 0) },
                null, SKShaderTileMode.Clamp);
            canvas.DrawCircle(wx, wy, 17f, halo);
        }

        // Bright pane, oriented along the wall (parallelogram).
        var edge = new SKPoint(fp.r.X - fp.b.X, fp.r.Y - fp.b.Y);
        float len = MathF.Sqrt(edge.X * edge.X + edge.Y * edge.Y);
        var u = new SKPoint(edge.X / len, edge.Y / len);   // along-wall unit
        const float hw = 4.2f, hh = 5.5f;                   // half extents (along, up)
        SKPoint P(float a, float up) => new(wx + u.X * a, wy + u.Y * a - up);
        using var pane = Quad(P(-hw, hh), P(hw, hh), P(hw, -hh), P(-hw, -hh));
        Fill(canvas, pane, new SKColor(0xFF, 0xE2, 0x95, (byte)(140 + glow * 115f)));
    }

    // Pyramid roof/tent: base = footprint raised by baseH, apex above center.
    private static void DrawPyramid(SKCanvas canvas, SKPoint c, float f, float roofH, SKColor roof, float baseH)
    {
        var fp = Footprint(c, f);
        var t = Up(fp.t, baseH); var r = Up(fp.r, baseH); var b = Up(fp.b, baseH); var l = Up(fp.l, baseH);
        var apex = new SKPoint(c.X, c.Y - baseH - roofH);

        // Back faces first, then front, for correct overlap.
        using (var p = Tri(apex, t, l)) FillFace(canvas, p, Darken(roof, 0.72f));
        using (var p = Tri(apex, t, r)) FillFace(canvas, p, Darken(roof, 0.90f));
        using (var p = Tri(apex, l, b)) FillFace(canvas, p, Darken(roof, 0.80f));
        using (var p = Tri(apex, b, r)) FillFace(canvas, p, Lighten(roof, 1.06f)); // sun-facing

        // Shingle rows on the two front faces + a bright ridge down the near corner.
        DrawShingles(canvas, apex, l, b, roof);
        DrawShingles(canvas, apex, b, r, roof);
        using var ridge = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.3f, Color = Lighten(roof, 1.25f) };
        canvas.DrawLine(apex.X, apex.Y, b.X, b.Y, ridge);
    }

    private static void DrawShingles(SKCanvas canvas, SKPoint apex, SKPoint p1, SKPoint p2, SKColor roof)
    {
        using var line = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.8f,
            Color = Darken(roof, 0.66f).WithAlpha(0x70),
        };
        for (int i = 1; i <= 3; i++)
        {
            float tt = i / 4f;                       // bottom edge → apex
            var a = Mix(p1, apex, tt);
            var b = Mix(p2, apex, tt);
            canvas.DrawLine(a.X, a.Y, b.X, b.Y, line);
        }
    }

    private static void DrawSlab(SKCanvas canvas, SKPoint c, BuildingStyle s)
    {
        var fp = Footprint(c, s.Footprint);
        DrawWalls(canvas, fp, s.WallHeight, s.Wall);
        using var topFace = Quad(Up(fp.t, s.WallHeight), Up(fp.r, s.WallHeight), Up(fp.b, s.WallHeight), Up(fp.l, s.WallHeight));
        Fill(canvas, topFace, s.Wall);
    }

    // A leafy tree on a grassy mound (s.Wall = foliage, s.Roof = trunk). Grows with level.
    private static void DrawGarden(SKCanvas canvas, SKPoint c, BuildingStyle s, int level)
    {
        var fp = Footprint(c, s.Footprint);
        using (var mound = Quad(fp.t, fp.r, fp.b, fp.l))
            Fill(canvas, mound, new SKColor(0x4A, 0x80, 0x4E));   // grass patch

        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        float trunkH = 13f + 2f * (level - 1);
        paint.Color = s.Roof;                                     // trunk
        canvas.DrawRect(new SKRect(c.X - 2f, c.Y - trunkH, c.X + 2f, c.Y), paint);

        float r = 9f + 1.6f * (level - 1);                        // foliage
        paint.Color = Darken(s.Wall, 0.82f);
        canvas.DrawCircle(c.X - 6f, c.Y - trunkH - 1f, r * 0.78f, paint);
        canvas.DrawCircle(c.X + 6f, c.Y - trunkH - 1f, r * 0.78f, paint);
        paint.Color = s.Wall;
        canvas.DrawCircle(c.X, c.Y - trunkH - r * 0.6f, r, paint);
    }

    // A low leafy hedge — a short green box.
    private static void DrawHedge(SKCanvas canvas, SKPoint c, BuildingStyle s)
    {
        var fp = Footprint(c, s.Footprint);
        DrawShadow(canvas, c, s.Footprint);
        DrawWalls(canvas, fp, s.WallHeight, s.Wall);
        using var top = Quad(Up(fp.t, s.WallHeight), Up(fp.r, s.WallHeight), Up(fp.b, s.WallHeight), Up(fp.l, s.WallHeight));
        Fill(canvas, top, s.Roof);
    }

    private static readonly SKColor[] FlowerColors =
    {
        new(0xE8, 0x55, 0x2E), new(0xF2, 0xC8, 0x4B), new(0xE6, 0x7A, 0xB0), new(0xB1, 0x7A, 0xE0), new(0xF0, 0xF0, 0xF0),
    };

    // A soil patch dotted with little flowers.
    private static void DrawFlowerBed(SKCanvas canvas, SKPoint c, BuildingStyle s)
    {
        var fp = Footprint(c, s.Footprint);
        DrawShadow(canvas, c, s.Footprint);
        using (var soil = Quad(fp.t, fp.r, fp.b, fp.l))
            Fill(canvas, soil, s.Wall);

        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        (float dx, float dy, int col)[] spots =
        {
            (-7f, 0f, 0), (0f, -3f, 1), (7f, 0f, 2), (-3.5f, 3f, 3), (3.5f, 3f, 4), (0f, 1.5f, 0),
        };
        foreach (var (dx, dy, col) in spots)
        {
            paint.Color = new SKColor(0x2E, 0x5A, 0x32);                 // stem dot
            canvas.DrawCircle(c.X + dx, c.Y + dy + 1.5f, 1.1f, paint);
            paint.Color = FlowerColors[col];                            // petals
            canvas.DrawCircle(c.X + dx, c.Y + dy, 2.4f, paint);
            paint.Color = new SKColor(0xF7, 0xE8, 0x9A);                // center
            canvas.DrawCircle(c.X + dx, c.Y + dy, 0.9f, paint);
        }
    }

    // A standalone lamp post; its orb glows warm after dark.
    private static void DrawLampPost(SKCanvas canvas, SKPoint c, float glow)
    {
        DrawShadow(canvas, c, 0.4f);
        float h = 30f;
        var orb = new SKPoint(c.X, c.Y - h - 2f);

        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        paint.Color = new SKColor(0x3A, 0x42, 0x4A);                    // post
        canvas.DrawRect(new SKRect(c.X - 1.6f, c.Y - h, c.X + 1.6f, c.Y), paint);

        if (glow > 0.25f)
        {
            using var halo = new SKPaint { IsAntialias = true };
            halo.Shader = SKShader.CreateRadialGradient(orb, 15f,
                new[] { new SKColor(0xFF, 0xCE, 0x6E, (byte)(glow * 150f)), new SKColor(0xFF, 0xCE, 0x6E, 0) },
                null, SKShaderTileMode.Clamp);
            canvas.DrawCircle(orb.X, orb.Y, 15f, halo);
        }

        paint.Color = new SKColor(0x2A, 0x30, 0x38);                    // cap
        canvas.DrawRect(new SKRect(c.X - 3f, orb.Y - 5.5f, c.X + 3f, orb.Y - 3f), paint);
        paint.Color = glow > 0.25f ? new SKColor(0xFF, 0xE6, 0x9C) : new SKColor(0xC6, 0xCC, 0xD4); // orb (lit / off)
        canvas.DrawCircle(orb.X, orb.Y, 3.2f, paint);
    }

    private static void DrawWell(SKCanvas canvas, SKPoint c, BuildingStyle s, float wallH)
    {
        var fp = Footprint(c, s.Footprint);
        DrawWalls(canvas, fp, wallH, s.Wall);
        // Dark "water" surface on top.
        using var water = Quad(Up(fp.t, wallH), Up(fp.r, wallH), Up(fp.b, wallH), Up(fp.l, wallH));
        Fill(canvas, water, s.Roof);
    }

    private static void DrawLevelPips(SKCanvas canvas, SKPoint c, float totalH, int level)
    {
        if (level < 2) return;
        using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(0xF2, 0xC8, 0x4B) };
        using var ring = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = new SKColor(0x6B, 0x52, 0x12) };
        float y = c.Y - totalH - 14f;
        float startX = c.X - (level - 1) * 6f;
        for (int i = 0; i < level - 1; i++)
        {
            float x = startX + i * 12f;
            canvas.DrawCircle(x, y, 4.5f, paint);
            canvas.DrawCircle(x, y, 4.5f, ring);
        }
    }

    private static readonly SKColor WalkerSkin = new(0xE6, 0xB8, 0x8A);
    private static readonly SKColor WalkerPants = new(0x37, 0x42, 0x57);
    private static readonly SKColor FolkInk = new(0x2A, 0x1C, 0x12, 150);  // soft sketch outline

    // Per-person variety, picked deterministically from each walker's shirt colour.
    private static readonly SKColor[] SkinTones =
    {
        new(0xF1, 0xC9, 0xA5), new(0xE6, 0xB8, 0x8A),
        new(0xC9, 0x96, 0x6B), new(0x9A, 0x6B, 0x47),
    };
    private static readonly SKColor[] HairColors =
    {
        new(0x3A, 0x2A, 0x1E), new(0x1E, 0x16, 0x12), new(0x6B, 0x45, 0x28),
        new(0x8A, 0x55, 0x2B), new(0xC9, 0xA1, 0x55), new(0x9E, 0x9A, 0x96),
    };

    private static SKTypeface? _emojiTypeface;
    private static SKTypeface? EmojiTypeface =>
        _emojiTypeface ??= SKFontManager.Default.MatchCharacter(0x1F60A); // 😊

    private static void DrawWalker(SKCanvas canvas, Walker w, SKPoint origin)
    {
        var top = IsoMath.GridToScreen(w.X, w.Y, origin);
        float cx = top.X;
        float cy = top.Y + IsoMath.TileHeight / 2f;        // tile centre = feet
        float feetY = DrawFigure(canvas, cx, cy, w.Facing, w.Phase, w.Moving, w.Shirt, w.Body, w.Hat, w.Prop, w.Accent);

        if (w.EmoteLeft > 0f && w.Emote is not null)
            DrawEmoteBubble(canvas, w.Emote, cx, feetY - 28f, MathF.Min(1f, w.EmoteLeft / 0.4f));
    }

    // Shared little-person renderer (townsfolk + the trader). Returns the bobbed feet Y.
    // Drawn in a scaled local space anchored at the feet, so body type just changes the scale.
    private static float DrawFigure(SKCanvas canvas, float cx, float cy, float facing, float phase, bool moving, SKColor shirt,
        FolkBody body = FolkBody.Adult, FolkHat hat = FolkHat.None, FolkProp prop = FolkProp.None, SKColor accent = default)
    {
        if (accent.Alpha == 0) accent = new SKColor(0x6E, 0x4A, 0x2E);

        // Bob: a clear hop while walking; a gentle sway while idle.
        float bob = MathF.Abs(MathF.Sin(phase)) * (moving ? 1.8f : 0.6f);
        float feetY = cy - bob;
        float scale = body switch { FolkBody.Child => 0.72f, FolkBody.Elder => 0.93f, _ => 1f };
        float lean = facing * 0.8f + (body == FolkBody.Elder ? facing * 1.1f : 0f);  // elders stoop forward
        float s = moving ? (MathF.Sin(phase) >= 0f ? 1f : -1f) : 0f;                  // 2-frame leg shuffle
        float arm = moving ? MathF.Sin(phase) * 1.7f : 0f;                            // arms swing opposite the legs

        // Deterministic colouring per person, derived from the shirt colour (stable per walker).
        int seed = shirt.Red * 3 + shirt.Green * 5 + shirt.Blue * 7;
        SKColor skin = SkinTones[seed % SkinTones.Length];
        SKColor hair = body == FolkBody.Elder
            ? (seed % 2 == 0 ? new SKColor(0xCF, 0xCB, 0xC4) : new SKColor(0x9E, 0x9A, 0x96))   // grey/white
            : HairColors[(seed / 4) % HairColors.Length];
        bool child = body == FolkBody.Child;

        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var ink = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.8f, Color = FolkInk };

        // Contact shadow on the ground (drawn in world space, before the body transform).
        paint.Color = new SKColor(0, 0, 0, 55);
        canvas.DrawOval(new SKRect(cx - 6.5f * scale, cy - 2.5f * scale, cx + 6.5f * scale, cy + 2.5f * scale), paint);

        canvas.Save();
        canvas.Translate(cx, feetY);
        canvas.Scale(scale);                                // feet now at local (0,0)

        // Legs — scissoring by s.
        paint.Color = WalkerPants;
        var legL = new SKRect(-3.5f + s * 1.4f, -7.5f, -0.3f + s * 1.4f, -1f);
        var legR = new SKRect(0.3f - s * 1.4f, -7.5f, 3.5f - s * 1.4f, -1f);
        canvas.DrawRoundRect(legL, 1.5f, 1.5f, paint); canvas.DrawRoundRect(legL, 1.5f, 1.5f, ink);
        canvas.DrawRoundRect(legR, 1.5f, 1.5f, paint); canvas.DrawRoundRect(legR, 1.5f, 1.5f, ink);

        // Arms — beside the torso, swinging opposite the legs; little skin hands.
        paint.Color = Darken(shirt, 0.9f);
        canvas.DrawRoundRect(new SKRect(-6f + lean - arm, -15f, -4f + lean - arm, -7.5f), 1.4f, 1.4f, paint);
        canvas.DrawRoundRect(new SKRect(4f + lean + arm, -15f, 6f + lean + arm, -7.5f), 1.4f, 1.4f, paint);
        paint.Color = skin;
        canvas.DrawCircle(-5f + lean - arm, -7f, 1.3f, paint);
        canvas.DrawCircle(5f + lean + arm, -7f, 1.3f, paint);

        // Torso — base shirt, lit on the sun (right) side, shaded along the bottom, then outlined.
        var torso = new SKRect(-4.5f + lean, -16.5f, 4.5f + lean, -6f);
        paint.Color = shirt;
        canvas.DrawRoundRect(torso, 3f, 3f, paint);
        paint.Color = Lighten(shirt, 1.10f);
        canvas.DrawRoundRect(new SKRect(0.5f + lean, -16f, 4.5f + lean, -9f), 3f, 3f, paint);
        paint.Color = Darken(shirt, 0.80f);
        canvas.DrawRoundRect(new SKRect(-4.5f + lean, -9.5f, 4.5f + lean, -6f), 3f, 3f, paint);
        canvas.DrawRoundRect(torso, 3f, 3f, ink);

        // Neck.
        paint.Color = Darken(skin, 0.9f);
        canvas.DrawRect(new SKRect(-1.3f + lean, -17.5f, 1.3f + lean, -15.8f), paint);

        // Head — hair cap behind, face circle slightly lower so hair reads as a crown.
        // Children get a proportionally bigger head.
        float hx = lean, hy = -20.5f;
        float hairR = child ? 5.0f : 4.4f;
        float fr = child ? 4.4f : 3.9f;
        float fy = hy + 1.3f;
        paint.Color = hair; canvas.DrawCircle(hx, hy, hairR, paint);
        paint.Color = skin; canvas.DrawCircle(hx, fy, fr, paint);
        paint.Color = Lighten(skin, 1.12f);                 // cheek highlight (lit side)
        canvas.DrawCircle(hx + 1.3f, fy + 0.2f, 1.5f, paint);
        canvas.DrawCircle(hx, hy, hairR, ink);

        DrawHeadwear(canvas, paint, ink, hat, hx, hy, fy, fr, facing, skin, accent);

        // Two little eyes, shifted toward the way they're looking.
        paint.Color = new SKColor(0x2A, 0x1C, 0x12);
        float ex = hx + facing * 1.2f;
        canvas.DrawCircle(ex - 1f, fy - 0.2f, 0.65f, paint);
        canvas.DrawCircle(ex + 1f, fy - 0.2f, 0.65f, paint);

        DrawProp(canvas, paint, ink, prop, lean, facing, accent);

        canvas.Restore();
        return feetY;
    }

    // Headwear sits over the crown; the headscarf also re-reveals the face so it peeks out.
    private static void DrawHeadwear(SKCanvas canvas, SKPaint paint, SKPaint ink, FolkHat hat,
        float hx, float hy, float fy, float fr, float facing, SKColor skin, SKColor accent)
    {
        switch (hat)
        {
            case FolkHat.FlatCap:
            {
                using var cap = new SKPath();
                cap.AddArc(new SKRect(hx - 4.9f, hy - 4.6f, hx + 4.9f, hy + 1.8f), 180f, 180f);
                cap.Close();
                paint.Color = accent; canvas.DrawPath(cap, paint); canvas.DrawPath(cap, ink);
                float px = hx + facing * 4.2f;              // short peak toward facing
                paint.Color = Darken(accent, 0.9f);
                canvas.DrawOval(new SKRect(px - 3f, hy - 1.1f, px + 3f, hy + 0.5f), paint);
                break;
            }
            case FolkHat.StrawHat:
            {
                var straw = new SKColor(0xC9, 0xA1, 0x55);
                paint.Color = straw;
                canvas.DrawOval(new SKRect(hx - 6f, hy - 4f, hx + 6f, hy - 1.5f), paint);
                canvas.DrawOval(new SKRect(hx - 6f, hy - 4f, hx + 6f, hy - 1.5f), ink);
                canvas.DrawRoundRect(new SKRect(hx - 3f, hy - 7f, hx + 3f, hy - 3f), 2f, 2f, paint);
                break;
            }
            case FolkHat.Beanie:
            {
                using var dome = new SKPath();
                dome.AddArc(new SKRect(hx - 4.6f, hy - 4.8f, hx + 4.6f, hy + 1.4f), 180f, 180f);
                dome.Close();
                paint.Color = accent; canvas.DrawPath(dome, paint); canvas.DrawPath(dome, ink);
                paint.Color = Darken(accent, 0.78f);        // folded band
                canvas.DrawRoundRect(new SKRect(hx - 4.6f, hy - 0.8f, hx + 4.6f, hy + 1f), 1.2f, 1.2f, paint);
                paint.Color = Lighten(accent, 1.12f);       // pom
                canvas.DrawCircle(hx, hy - 5.2f, 1.2f, paint);
                break;
            }
            case FolkHat.Headscarf:
            {
                paint.Color = accent;                       // wrap over the whole head
                canvas.DrawCircle(hx, hy, 4.8f, paint);
                canvas.DrawCircle(hx, hy, 4.8f, ink);
                paint.Color = skin;                         // face peeks out at the front
                canvas.DrawCircle(hx + facing * 0.6f, fy + 0.5f, fr - 0.4f, paint);
                paint.Color = Lighten(skin, 1.12f);
                canvas.DrawCircle(hx + 1.3f, fy + 0.5f, 1.3f, paint);
                paint.Color = accent;                       // knot at the back
                canvas.DrawCircle(hx - facing * 4.4f, hy + 1.4f, 1.5f, paint);
                break;
            }
        }
    }

    // A carried item, drawn over the body.
    private static void DrawProp(SKCanvas canvas, SKPaint paint, SKPaint ink, FolkProp prop, float lean, float facing, SKColor accent)
    {
        switch (prop)
        {
            case FolkProp.Satchel:
            {
                using var strap = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f, Color = Darken(accent, 0.7f) };
                canvas.DrawLine(lean - 3.2f, -16.5f, lean + 3.2f, -9f, strap);
                float bx = lean - facing * 3.8f;            // bag on the trailing hip
                var bag = new SKRect(bx - 2.3f, -10.5f, bx + 2.3f, -6.2f);
                paint.Color = accent; canvas.DrawRoundRect(bag, 1.2f, 1.2f, paint);
                paint.Color = Darken(accent, 0.82f);        // flap
                canvas.DrawRoundRect(new SKRect(bx - 2.3f, -10.5f, bx + 2.3f, -8.6f), 1.2f, 1.2f, paint);
                canvas.DrawRoundRect(bag, 1.2f, 1.2f, ink);
                break;
            }
            case FolkProp.Basket:
            {
                var wicker = new SKColor(0xC8, 0x9B, 0x5C);
                float bx = lean + facing * 3.6f;            // held out front
                using var handle = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.9f, Color = Darken(wicker, 0.7f) };
                canvas.DrawArc(new SKRect(bx - 3f, -12f, bx + 3f, -7f), 180f, 180f, false, handle);
                var basket = new SKRect(bx - 3f, -9.5f, bx + 3f, -6f);
                paint.Color = wicker; canvas.DrawRoundRect(basket, 1.4f, 1.4f, paint);
                canvas.DrawRoundRect(basket, 1.4f, 1.4f, ink);
                break;
            }
            case FolkProp.Cane:
            {
                using var wood = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.3f, Color = new SKColor(0x8A, 0x5A, 0x2B), StrokeCap = SKStrokeCap.Round };
                float cxh = lean + facing * 5.6f;
                canvas.DrawLine(cxh, -8f, cxh + facing * 0.6f, -0.4f, wood);
                canvas.DrawArc(new SKRect(cxh - 1.7f, -9.6f, cxh + 1.7f, -7f), 180f, 180f, false, wood);
                break;
            }
        }
    }

    private static void DrawVisitor(SKCanvas canvas, Visitor v, SKPoint origin)
    {
        var top = IsoMath.GridToScreen(v.X, v.Y, origin);
        float cx = top.X;
        float cy = top.Y + IsoMath.TileHeight / 2f;

        if (v.Kind == VisitorKind.Cat)
        {
            float bob = MathF.Abs(MathF.Sin(v.Phase)) * 1.1f;
            using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
            paint.Color = new SKColor(0, 0, 0, 55);
            canvas.DrawOval(new SKRect(cx - 5f, cy - 2f, cx + 5f, cy + 2f), paint);
            using var font = new SKFont(EmojiTypeface ?? SKTypeface.Default, 15f);
            canvas.DrawText("🐈", cx, cy + 1f - bob, SKTextAlign.Center, font, paint);
            if (v.EmoteLeft > 0f && v.Emote is not null)
                DrawEmoteBubble(canvas, v.Emote, cx, cy - 13f, MathF.Min(1f, v.EmoteLeft / 0.4f));
        }
        else // Trader — a townsperson with a straw hat + a satchel over the shoulder
        {
            float feetY = DrawFigure(canvas, cx, cy, v.Facing, v.Phase, v.Moving, v.Shirt,
                FolkBody.Adult, FolkHat.StrawHat, FolkProp.Satchel, new SKColor(0x7A, 0x53, 0x33));
            if (v.EmoteLeft > 0f && v.Emote is not null)
                DrawEmoteBubble(canvas, v.Emote, cx, feetY - 27f, MathF.Min(1f, v.EmoteLeft / 0.4f));
        }
    }

    private static void DrawPet(SKCanvas canvas, Pet p, SKPoint origin)
    {
        var top = IsoMath.GridToScreen(p.X, p.Y, origin);
        float cx = top.X;
        float cy = top.Y + IsoMath.TileHeight / 2f;
        float bob = MathF.Abs(MathF.Sin(p.Phase)) * 1.2f;

        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        paint.Color = new SKColor(0, 0, 0, 55);            // shadow
        canvas.DrawOval(new SKRect(cx - 5f, cy - 2f, cx + 5f, cy + 2f), paint);

        using var font = new SKFont(EmojiTypeface ?? SKTypeface.Default, 15f);
        canvas.DrawText("🐕", cx, cy + 1f - bob, SKTextAlign.Center, font, paint);

        if (p.EmoteLeft > 0f && p.Emote is not null)
            DrawEmoteBubble(canvas, p.Emote, cx, cy - 13f, MathF.Min(1f, p.EmoteLeft / 0.4f));
    }

    private static void DrawEmoteBubble(SKCanvas canvas, string emote, float cx, float anchorY, float alpha)
    {
        byte a = (byte)(alpha * 255);
        float w = 19f, h = 17f;
        float bottom = anchorY - 3f, topY = bottom - h;
        var rect = new SKRect(cx - w / 2f, topY, cx + w / 2f, bottom);

        using var bubble = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xF5, 0xF7, 0xFA, a) };
        canvas.DrawRoundRect(new SKRoundRect(rect, 6f), bubble);
        using var tail = new SKPath();
        tail.MoveTo(cx - 3f, bottom - 1f);
        tail.LineTo(cx, anchorY + 1.5f);
        tail.LineTo(cx + 3f, bottom - 1f);
        tail.Close();
        canvas.DrawPath(tail, bubble);

        var tf = EmojiTypeface;
        using var font = new SKFont(tf ?? SKTypeface.Default, 13f);
        using var text = new SKPaint { IsAntialias = true, Color = new SKColor(0x1A, 0x1A, 0x1A, a) };
        canvas.DrawText(emote, cx, topY + h * 0.72f, SKTextAlign.Center, font, text);
    }

    private static SKPath DiamondPath(SKPoint top)
    {
        var path = new SKPath();
        path.MoveTo(top.X, top.Y);
        path.LineTo(top.X + IsoMath.TileWidth / 2f, top.Y + IsoMath.TileHeight / 2f);
        path.LineTo(top.X, top.Y + IsoMath.TileHeight);
        path.LineTo(top.X - IsoMath.TileWidth / 2f, top.Y + IsoMath.TileHeight / 2f);
        path.Close();
        return path;
    }

    private static SKColor Darken(SKColor c, float factor) =>
        new((byte)(c.Red * factor), (byte)(c.Green * factor), (byte)(c.Blue * factor), c.Alpha);

    private static SKColor Lighten(SKColor c, float factor) => new(
        (byte)Math.Min(255f, c.Red * factor),
        (byte)Math.Min(255f, c.Green * factor),
        (byte)Math.Min(255f, c.Blue * factor), c.Alpha);

    private static SKPoint Mix(SKPoint a, SKPoint b, float t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    // Deterministic, pleasant placeholder color per building id.
    private static SKColor ColorFor(string id)
    {
        unchecked
        {
            int hash = 17;
            foreach (var ch in id)
                hash = hash * 31 + ch;
            float hue = (uint)hash % 360u;
            return SKColor.FromHsl(hue, 55f, 60f);
        }
    }

    private static Building? BuildingCatalogLookup(string id) => Services.BuildingCatalog.Find(id);
}
