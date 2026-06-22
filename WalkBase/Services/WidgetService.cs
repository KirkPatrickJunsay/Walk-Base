namespace WalkBase.Services;

/// <summary>Asks the platform to refresh the home-screen widget (no-op where unsupported).</summary>
public interface IWidgetService
{
    void Refresh();
}

public sealed class WidgetService : IWidgetService
{
    public void Refresh()
    {
#if ANDROID
        try { Platforms.Android.WalkTrackWidget.RequestUpdate(global::Android.App.Application.Context!); }
        catch { /* widget not placed / unavailable */ }
#endif
    }
}
