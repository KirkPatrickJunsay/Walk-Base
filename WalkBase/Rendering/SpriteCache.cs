using SkiaSharp;

namespace WalkBase.Rendering;

/// <summary>
/// Loads sprite PNGs (MauiAsset under Resources/Raw/Sprites/) once into SKBitmaps and
/// caches them by filename (spec §11). Missing files cache as null so the renderer can
/// fall back to placeholder diamonds — the app runs with zero art assets present.
/// </summary>
public sealed class SpriteCache
{
    private readonly Dictionary<string, SKBitmap?> _cache = new(StringComparer.Ordinal);

    /// <summary>Returns the cached bitmap, or null if not loaded / missing.</summary>
    public SKBitmap? Get(string? key) =>
        key is not null && _cache.TryGetValue(key, out var bmp) ? bmp : null;

    public bool IsLoaded(string key) => _cache.ContainsKey(key);

    /// <summary>Loads any keys not already cached. Safe to call repeatedly.</summary>
    public async Task PreloadAsync(IEnumerable<string?> keys)
    {
        foreach (var key in keys.Where(k => k is not null).Distinct())
        {
            if (_cache.ContainsKey(key!))
                continue;
            _cache[key!] = await LoadAsync(key!);
        }
    }

    private static async Task<SKBitmap?> LoadAsync(string fileName)
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync($"Sprites/{fileName}");
            return SKBitmap.Decode(stream);
        }
        catch (FileNotFoundException)
        {
            return null; // Art not bundled yet — placeholder will be drawn.
        }
    }
}
