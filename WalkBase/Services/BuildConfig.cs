namespace WalkBase.Services;

/// <summary>Compile-time build switches. <see cref="FreeBuilds"/> is on only in Debug so you
/// can place, upgrade and expand without spending Bricks while testing; Release stays paid.</summary>
public static class BuildConfig
{
#if DEBUG
    public const bool FreeBuilds = true;
#else
    public const bool FreeBuilds = false;
#endif
}
