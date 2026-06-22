namespace WalkBase.Services;

/// <summary>
/// Shared declaration; per-platform implementations live in
/// Platforms/Android/StepSensor.android.cs and Platforms/iOS/StepSensor.ios.cs.
/// </summary>
public partial class StepSensor : IStepSensor
{
    public partial bool IsAvailable { get; }
    public partial Task<bool> RequestPermissionAsync();
    public partial Task<long?> ReadRawCumulativeAsync();
    public partial void Stop();
    public partial void Resume();
}
