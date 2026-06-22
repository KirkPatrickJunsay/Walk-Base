namespace WalkBase.Services;

/// <summary>
/// Distance + calorie approximations. Stride is derived from height; calories from
/// weight × distance. These are rough estimates, not medical figures.
/// </summary>
public static class HealthMath
{
    public const double DefaultStrideMeters = 0.78;
    private const double StrideHeightFactor = 0.415;  // stride ≈ height × 0.415
    private const double KcalPerKgKm = 0.75;          // gross walking energy estimate

    public static double StrideMeters(int heightCm) =>
        heightCm > 0 ? heightCm * StrideHeightFactor / 100.0 : DefaultStrideMeters;

    public static double DistanceKm(long steps, double strideMeters) =>
        steps * strideMeters / 1000.0;

    /// <summary>Estimated kcal burned for the given steps; 0 if weight is unset.</summary>
    public static int Calories(long steps, int heightCm, int weightKg)
    {
        if (weightKg <= 0 || steps <= 0)
            return 0;
        var km = DistanceKm(steps, StrideMeters(heightCm));
        return (int)Math.Round(weightKg * km * KcalPerKgKm);
    }
}
