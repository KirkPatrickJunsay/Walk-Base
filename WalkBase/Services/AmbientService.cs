using Microsoft.Maui.Storage;
#if ANDROID
using Android.Media;
#endif

namespace WalkBase.Services;

/// <summary>Which looping ambience suits the current scene.</summary>
public enum AmbientScene { None, Day, Night, Rain, Festival }

public interface IAmbientService
{
    /// <summary>Switch the looping ambience to match the scene (no-op if unchanged).</summary>
    void SetScene(AmbientScene scene);

    /// <summary>Stop the ambience (e.g. when the Town page goes away).</summary>
    void Stop();
}

/// <summary>
/// Looping background ambience tied to time/weather/festival, played via a single Android
/// MediaPlayer. Honours the Sound setting and fails silently. Switches are infrequent.
/// </summary>
public sealed class AmbientService : IAmbientService
{
    private readonly ISettingsService _settings;
    private AmbientScene _current = AmbientScene.None;

    private static readonly Dictionary<AmbientScene, string> Files = new()
    {
        [AmbientScene.Day] = "ambient_day.wav",
        [AmbientScene.Night] = "ambient_night.wav",
        [AmbientScene.Rain] = "ambient_rain.wav",
        [AmbientScene.Festival] = "ambient_festival.wav",
    };

    public AmbientService(ISettingsService settings) => _settings = settings;

    public void SetScene(AmbientScene scene)
    {
        if (!_settings.SoundEnabled || scene == AmbientScene.None)
        {
            Stop();
            return;
        }
        if (scene == _current)
            return;
        _current = scene;
        PlayInternal(scene);
    }

    public void Stop()
    {
        _current = AmbientScene.None;
        StopInternal();
    }

#if ANDROID
    private MediaPlayer? _player;

    private async void PlayInternal(AmbientScene scene)
    {
        try
        {
            if (!Files.TryGetValue(scene, out var file))
            {
                StopInternal();
                return;
            }

            var path = Path.Combine(FileSystem.CacheDirectory, file);
            if (!File.Exists(path))
            {
                using var src = await FileSystem.OpenAppPackageFileAsync(file);
                using var dst = File.Create(path);
                await src.CopyToAsync(dst);
            }

            _player ??= new MediaPlayer();
            _player.Reset();
            _player.SetDataSource(path);
            _player.Looping = true;
            _player.SetVolume(0.45f, 0.45f);
            _player.Prepare();
            _player.Start();
        }
        catch { /* best-effort ambience */ }
    }

    private void StopInternal()
    {
        try { if (_player?.IsPlaying == true) _player.Pause(); }
        catch { }
    }
#else
    private void PlayInternal(AmbientScene scene) { }
    private void StopInternal() { }
#endif
}
