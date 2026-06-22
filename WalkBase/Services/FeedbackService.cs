using Microsoft.Maui.Devices;
#if ANDROID
using Android.Media;
#endif

namespace WalkBase.Services;

/// <summary>The kind of action being acknowledged (picks haptic strength + which SFX plays).</summary>
public enum FeedbackKind { Tap, Build, Claim, Goal }

public interface IFeedbackService
{
    /// <summary>Fire a short vibration + sound effect (each gated by its Settings toggle).</summary>
    void Play(FeedbackKind kind);
}

/// <summary>
/// Haptics (cross-platform MAUI) + short sound effects (native Android SoundPool, loaded from
/// bundled WAVs). Both honour the user's Settings toggles and fail silently on unsupported devices.
/// </summary>
public sealed class FeedbackService : IFeedbackService
{
    private readonly ISettingsService _settings;

    private static readonly (FeedbackKind kind, string file)[] Files =
    {
        (FeedbackKind.Tap, "sfx_tap.wav"),
        (FeedbackKind.Build, "sfx_build.wav"),
        (FeedbackKind.Claim, "sfx_claim.wav"),
        (FeedbackKind.Goal, "sfx_goal.wav"),
    };

    public FeedbackService(ISettingsService settings)
    {
        _settings = settings;
        PreloadSounds();
    }

    public void Play(FeedbackKind kind)
    {
        if (_settings.HapticsEnabled)
        {
            try
            {
                HapticFeedback.Default.Perform(
                    kind == FeedbackKind.Goal ? HapticFeedbackType.LongPress : HapticFeedbackType.Click);
            }
            catch { /* device has no vibrator */ }
        }

        if (_settings.SoundEnabled)
            PlaySound(kind);
    }

#if ANDROID
    private SoundPool? _pool;
    private readonly Dictionary<FeedbackKind, int> _ids = new();

    private async void PreloadSounds()
    {
        try
        {
            _pool = new SoundPool.Builder()
                .SetMaxStreams(4)
                .SetAudioAttributes(new AudioAttributes.Builder()
                    .SetUsage(AudioUsageKind.Game)!
                    .SetContentType(AudioContentType.Sonification)!
                    .Build())
                .Build();

            foreach (var (kind, file) in Files)
            {
                var cache = Path.Combine(FileSystem.CacheDirectory, file);
                if (!File.Exists(cache))
                {
                    using var src = await FileSystem.OpenAppPackageFileAsync(file);
                    using var dst = File.Create(cache);
                    await src.CopyToAsync(dst);
                }
                _ids[kind] = _pool!.Load(cache, 1);
            }
        }
        catch { _pool = null; }
    }

    private void PlaySound(FeedbackKind kind)
    {
        if (_pool is not null && _ids.TryGetValue(kind, out var id) && id != 0)
            _pool.Play(id, 0.9f, 0.9f, 1, 0, 1f);
    }
#else
    private void PreloadSounds() { }
    private void PlaySound(FeedbackKind kind) { }
#endif
}
