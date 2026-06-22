using SkiaSharp;

namespace WalkBase.Rendering;

public enum Season { Spring, Summer, Autumn, Winter }

/// <summary>Northern-hemisphere season from a calendar month (shared by sim + renderer).</summary>
public static class Seasons
{
    public static Season Of(int month) => month switch
    {
        12 or 1 or 2 => Season.Winter,
        3 or 4 or 5 => Season.Spring,
        6 or 7 or 8 => Season.Summer,
        _ => Season.Autumn,
    };

    public static Season Now() => Of(DateTime.Now.Month);
}

/// <summary>Build (and silhouette) of a townsperson — drives scale and posture.</summary>
public enum FolkBody : byte { Adult, Child, Elder }

/// <summary>Optional headwear, for crowd variety.</summary>
public enum FolkHat : byte { None, FlatCap, StrawHat, Beanie, Headscarf }

/// <summary>Optional carried item, for crowd variety.</summary>
public enum FolkProp : byte { None, Satchel, Basket, Cane }

/// <summary>One wandering townsperson, positioned in (fractional) grid coordinates.</summary>
public sealed class Walker
{
    public float X, Y;
    public float TargetX, TargetY;
    public float Speed;
    public float Phase;
    public float PauseLeft;
    public float Facing = 1f;
    public bool VisitingBuilding;
    public string? VisitingBuildingId;   // which building they're at/heading to (for emotes)
    public float LookTimer;
    public string? Emote;
    public float EmoteLeft;
    public SKColor Shirt;

    // Appearance, rolled once at spawn (see TownsfolkSim.RollLook).
    public FolkBody Body;
    public FolkHat Hat;
    public FolkProp Prop;
    public SKColor Accent;               // hat / scarf / bag colour

    public bool Moving => PauseLeft <= 0f;
}

/// <summary>A pet (dog) that trails a townsperson.</summary>
public sealed class Pet
{
    public float X, Y, Phase;
    public int Owner;
    public string? Emote;
    public float EmoteLeft;
}

public enum VisitorKind { Cat, Trader }

/// <summary>A rare special guest (wandering cat or travelling trader) that visits, then leaves.</summary>
public sealed class Visitor
{
    public float X, Y, TargetX, TargetY, Speed, Phase;
    public float Facing = 1f, PauseLeft, EmoteLeft, LifeLeft;
    public bool Leaving;
    public string? Emote;
    public VisitorKind Kind;
    public SKColor Shirt;

    public bool Moving => PauseLeft <= 0f;
}

/// <summary>
/// Wander simulation for the base's little people (and a dog). They emerge near buildings,
/// amble between them, pause at doors, look around, and emote — with context-aware emoji
/// (building type, night-time, goal celebration).
/// </summary>
public sealed class TownsfolkSim
{
    private readonly List<Walker> _walkers = new();
    private readonly List<Pet> _pets = new();
    private readonly Random _rng = new();
    private IReadOnlyList<(int x, int y, string id)> _buildings = Array.Empty<(int, int, string)>();

    private Visitor? _visitor;
    private float _visitorCooldown = 35f;   // a first special guest shows up after a bit

    private float _festivalLeft;            // seconds remaining in the current festival
    private float _festivalCooldown = 120f; // until the next festival may begin

    /// <summary>Set by the view-model from the real clock — biases emotes toward sleepy ones.</summary>
    public bool IsNight { get; set; }

    /// <summary>The current rare guest (cat or trader), or null.</summary>
    public Visitor? SpecialVisitor => _visitor;

    /// <summary>How many townsfolk currently live on the base.</summary>
    public int Population => _walkers.Count;

    /// <summary>True while a festival is on — the town strings up bunting and celebrates.</summary>
    public bool IsFestival => _festivalLeft > 0f;

    private static readonly SKColor[] Shirts =
    {
        new(0xE8, 0x55, 0x2E), new(0x3D, 0xA0, 0xA0), new(0xF2, 0xC8, 0x4B),
        new(0x5B, 0xC4, 0x7A), new(0x8C, 0x6A, 0xD0), new(0xE0, 0x7A, 0xA0),
    };

    private static readonly SKColor[] Accents =
    {
        new(0x6E, 0x4A, 0x2E), new(0x39, 0x5B, 0x86), new(0x8A, 0x3B, 0x3B),
        new(0x47, 0x6B, 0x3E), new(0xC9, 0xA1, 0x55), new(0xD8, 0xD2, 0xC8),
    };

    /// <summary>Roll a townsperson's body/hat/prop/accent with sensible weights.</summary>
    private void RollLook(Walker w)
    {
        float b = _rng.NextSingle();
        w.Body = b < 0.17f ? FolkBody.Child : b < 0.30f ? FolkBody.Elder : FolkBody.Adult;
        w.Accent = Accents[_rng.Next(Accents.Length)];

        // Headwear — children lean beanie, elders lean flat cap.
        float h = _rng.NextSingle();
        w.Hat = w.Body switch
        {
            FolkBody.Child => h < 0.50f ? FolkHat.None : h < 0.80f ? FolkHat.Beanie : FolkHat.Headscarf,
            FolkBody.Elder => h < 0.40f ? FolkHat.None : h < 0.75f ? FolkHat.FlatCap : FolkHat.Headscarf,
            _ => h < 0.45f ? FolkHat.None : h < 0.62f ? FolkHat.FlatCap
               : h < 0.78f ? FolkHat.StrawHat : h < 0.90f ? FolkHat.Beanie : FolkHat.Headscarf,
        };

        // Carried prop — elders often have a cane, children sometimes a basket.
        float p = _rng.NextSingle();
        w.Prop = w.Body switch
        {
            FolkBody.Elder => p < 0.55f ? FolkProp.Cane : p < 0.70f ? FolkProp.Satchel : FolkProp.None,
            FolkBody.Child => p < 0.18f ? FolkProp.Basket : FolkProp.None,
            _ => p < 0.55f ? FolkProp.None : p < 0.78f ? FolkProp.Satchel : p < 0.90f ? FolkProp.Basket : FolkProp.Cane,
        };
    }

    private static readonly string[] General = { "😊", "👋", "💬", "🎵", "⭐", "✨", "🙂" };
    private static readonly string[] NightTime = { "😴", "💤", "🌙" };

    private static string[] FestiveFor(Season s) => s switch
    {
        Season.Winter => new[] { "❄️", "⛄", "🔥", "🎵", "✨", "🏮" },
        Season.Spring => new[] { "🌸", "🌷", "🦋", "🌱", "🎵", "✨" },
        Season.Autumn => new[] { "🍂", "🎃", "🌽", "🍎", "🎵", "✨" },
        _ => new[] { "🎉", "🎊", "🥳", "🍦", "🎵", "✨" }, // Summer
    };

    public IReadOnlyList<Walker> Walkers => _walkers;
    public IReadOnlyList<Pet> Pets => _pets;

    public void Sync(int desired, int gridSize, IReadOnlyList<(int x, int y, string id)> buildings)
    {
        _buildings = buildings;
        desired = Math.Clamp(desired, 0, 12);
        float max = Math.Max(0, gridSize - 1);

        while (_walkers.Count < desired)
        {
            var (x, y, _) = RandomSpot(gridSize);
            var w = new Walker
            {
                X = x, Y = y, TargetX = x, TargetY = y,
                Speed = 0.5f + _rng.NextSingle() * 0.5f,
                Phase = _rng.NextSingle() * MathF.Tau,
                LookTimer = _rng.NextSingle() * 2f,
                Shirt = Shirts[_rng.Next(Shirts.Length)],
            };
            RollLook(w);
            _walkers.Add(w);
        }
        while (_walkers.Count > desired)
            _walkers.RemoveAt(_walkers.Count - 1);
        foreach (var w in _walkers)
        {
            w.X = Math.Clamp(w.X, 0, max);
            w.Y = Math.Clamp(w.Y, 0, max);
        }

        // One dog once the town has a couple of people.
        int wantPets = _walkers.Count >= 2 ? 1 : 0;
        while (_pets.Count < wantPets)
        {
            int o = _rng.Next(_walkers.Count);
            _pets.Add(new Pet { X = _walkers[o].X, Y = _walkers[o].Y, Owner = o });
        }
        while (_pets.Count > wantPets)
            _pets.RemoveAt(_pets.Count - 1);
        foreach (var p in _pets)
            p.Owner = Math.Clamp(p.Owner, 0, Math.Max(0, _walkers.Count - 1));
    }

    public void Update(float dt, int gridSize)
    {
        // Festival comes around occasionally and lasts a while.
        if (_festivalLeft > 0f)
        {
            _festivalLeft -= dt;
        }
        else
        {
            _festivalCooldown -= dt;
            if (_festivalCooldown <= 0f)
            {
                _festivalLeft = 35f + _rng.NextSingle() * 25f;
                _festivalCooldown = 240f + _rng.NextSingle() * 240f;
            }
        }

        foreach (var w in _walkers)
        {
            if (w.EmoteLeft > 0f)
                w.EmoteLeft -= dt;

            if (w.PauseLeft > 0f)
            {
                w.PauseLeft -= dt;
                w.Phase += dt * 2.5f;

                w.LookTimer -= dt;
                if (w.LookTimer <= 0f)
                {
                    w.LookTimer = 0.7f + _rng.NextSingle() * 1.3f;
                    if (_rng.NextSingle() < 0.5f)
                        w.Facing = -w.Facing;
                }

                if (w.EmoteLeft <= 0f && _rng.NextSingle() < (IsFestival ? 0.03f : 0.006f))
                    StartEmote(w);
                continue;
            }

            float dx = w.TargetX - w.X, dy = w.TargetY - w.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist < 0.06f)
            {
                w.PauseLeft = w.VisitingBuilding ? 2f + _rng.NextSingle() * 2.5f
                                                 : _rng.NextSingle() * 1.2f;
                if (w.VisitingBuilding && _rng.NextSingle() < 0.7f)
                    StartEmote(w); // emote about the building they just reached

                var (tx, ty, bid) = RandomSpot(gridSize);
                w.TargetX = tx;
                w.TargetY = ty;
                w.VisitingBuilding = bid is not null;
                w.VisitingBuildingId = bid;
            }
            else
            {
                float step = MathF.Min(w.Speed * dt, dist);
                w.X += dx / dist * step;
                w.Y += dy / dist * step;
                w.Phase += dt * 9f;
                float screenDx = dx - dy;
                if (MathF.Abs(screenDx) > 0.01f)
                    w.Facing = MathF.Sign(screenDx);
            }
        }

        // Dog trails a little behind its owner.
        foreach (var p in _pets)
        {
            if (_walkers.Count == 0)
                continue;
            var o = _walkers[Math.Clamp(p.Owner, 0, _walkers.Count - 1)];
            // Trot just in front of the owner (higher depth → drawn on top, on the ground).
            float tx = o.X + 0.35f, ty = o.Y + 0.35f;
            float k = MathF.Min(1f, dt * 3f);
            p.X += (tx - p.X) * k;
            p.Y += (ty - p.Y) * k;
            p.Phase += dt * 7f;

            if (p.EmoteLeft > 0f)
                p.EmoteLeft -= dt;
            else if (_rng.NextSingle() < 0.004f)
            {
                p.Emote = _rng.NextSingle() < 0.5f ? "🐾" : "🐶";
                p.EmoteLeft = 1.4f + _rng.NextSingle() * 0.8f;
            }
        }

        UpdateVisitor(dt, gridSize);
    }

    private void UpdateVisitor(float dt, int gridSize)
    {
        if (_visitor is null)
        {
            _visitorCooldown -= dt;
            if (_visitorCooldown <= 0f && _buildings.Count > 0)
                SpawnVisitor(gridSize);
            return;
        }

        var v = _visitor;
        v.LifeLeft -= dt;
        if (v.EmoteLeft > 0f) v.EmoteLeft -= dt;

        // Once its visit is over, head for the nearest edge and exit.
        if (v.LifeLeft <= 0f && !v.Leaving)
        {
            v.Leaving = true;
            v.PauseLeft = 0f;
            (v.TargetX, v.TargetY) = RandomEdge(gridSize);
        }

        if (v.PauseLeft > 0f)
        {
            v.PauseLeft -= dt;
            v.Phase += dt * 2.5f;
            if (v.EmoteLeft <= 0f && _rng.NextSingle() < 0.012f)
                StartVisitorEmote(v);
            return;
        }

        float dx = v.TargetX - v.X, dy = v.TargetY - v.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist < 0.06f)
        {
            if (v.Leaving)   // reached the edge → gone, set a long cooldown
            {
                _visitor = null;
                _visitorCooldown = 150f + _rng.NextSingle() * 210f;
                return;
            }
            v.PauseLeft = 1f + _rng.NextSingle() * 2f;
            if (_rng.NextSingle() < 0.6f) StartVisitorEmote(v);
            var (tx, ty, _) = RandomSpot(gridSize);
            v.TargetX = tx; v.TargetY = ty;
        }
        else
        {
            float step = MathF.Min(v.Speed * dt, dist);
            v.X += dx / dist * step;
            v.Y += dy / dist * step;
            v.Phase += dt * 9f;
            float screenDx = dx - dy;
            if (MathF.Abs(screenDx) > 0.01f) v.Facing = MathF.Sign(screenDx);
        }
    }

    private void SpawnVisitor(int gridSize)
    {
        bool cat = _rng.NextSingle() < 0.5f;
        var (ex, ey) = RandomEdge(gridSize);
        var (tx, ty, _) = RandomSpot(gridSize);
        _visitor = new Visitor
        {
            Kind = cat ? VisitorKind.Cat : VisitorKind.Trader,
            X = ex, Y = ey, TargetX = tx, TargetY = ty,
            Speed = cat ? 0.8f + _rng.NextSingle() * 0.5f : 0.45f + _rng.NextSingle() * 0.3f,
            Phase = _rng.NextSingle() * MathF.Tau,
            Shirt = new SKColor(0x7A, 0x53, 0x33),   // trader satchel-brown
            LifeLeft = 22f + _rng.NextSingle() * 18f,
        };
    }

    private (float x, float y) RandomEdge(int gridSize)
    {
        float max = Math.Max(0, gridSize - 1);
        return _rng.Next(4) switch
        {
            0 => (0f, _rng.NextSingle() * max),
            1 => (max, _rng.NextSingle() * max),
            2 => (_rng.NextSingle() * max, 0f),
            _ => (_rng.NextSingle() * max, max),
        };
    }

    private void StartVisitorEmote(Visitor v)
    {
        string[] set = v.Kind == VisitorKind.Cat
            ? new[] { "🐈", "😺", "🐾", "🐟" }
            : new[] { "🧺", "💰", "🛒", "✨", "🍞" };
        v.Emote = set[_rng.Next(set.Length)];
        v.EmoteLeft = 1.8f + _rng.NextSingle() * 1.4f;
    }

    /// <summary>Everyone cheers 🎉 — call when the daily step goal is reached.</summary>
    public void Celebrate()
    {
        foreach (var w in _walkers)
        {
            w.Emote = "🎉";
            w.EmoteLeft = 3.5f;
        }
    }

    private void StartEmote(Walker w)
    {
        string[] set =
            IsFestival && _rng.NextSingle() < 0.75f ? FestiveFor(Seasons.Now()) :
            IsNight && _rng.NextSingle() < 0.6f ? NightTime :
            w.VisitingBuildingId is not null ? EmotesFor(w.VisitingBuildingId) :
            General;
        w.Emote = set[_rng.Next(set.Length)];
        w.EmoteLeft = 1.8f + _rng.NextSingle() * 1.4f;
    }

    private static string[] EmotesFor(string buildingId) => buildingId switch
    {
        "town_hall" => new[] { "🧱", "🎉", "⭐" },
        "farm" => new[] { "🍎", "🌾", "🐔" },
        "house" => new[] { "🏠", "❤️", "☕" },
        "well" => new[] { "💧", "🪣" },
        "market" => new[] { "🛒", "💰", "🍞" },
        "workshop" => new[] { "🔨", "⚙️", "🪵" },
        "watchtower" => new[] { "👀", "🔭" },
        "tent" => new[] { "⛺", "🔥" },
        "path" => new[] { "🚶", "👣" },
        "garden" => new[] { "🌳", "🌷", "🦋", "🌻" },
        "flower_bed" => new[] { "🌷", "🌼", "🐝", "🌸" },
        "hedge" => new[] { "🌿", "✂️" },
        "lamppost" => new[] { "💡", "✨" },
        "bakery" => new[] { "🍞", "🥐", "🧁", "☕" },
        "fountain" => new[] { "⛲", "💧", "🪙" },
        "library" => new[] { "📚", "📖", "✨", "🤔" },
        _ => General,
    };

    private (float x, float y, string? buildingId) RandomSpot(int gridSize)
    {
        float max = Math.Max(0, gridSize - 1);
        if (_buildings.Count > 0 && _rng.NextSingle() < 0.65f)
        {
            var b = _buildings[_rng.Next(_buildings.Count)];
            float jx = (_rng.NextSingle() - 0.5f) * 1.4f;
            float jy = (_rng.NextSingle() - 0.5f) * 1.4f;
            return (Math.Clamp(b.x + jx, 0, max), Math.Clamp(b.y + jy, 0, max), b.id);
        }
        return (_rng.NextSingle() * max, _rng.NextSingle() * max, null);
    }
}
