namespace WalkBase.Services;

/// <summary>
/// Tiny shared singleton: when the user taps an empty plot on the Base tab and jumps
/// to Build, this carries the chosen cell across. Null means "use the next free plot".
/// </summary>
public sealed class SelectionState
{
    public (int x, int y)? SelectedPlot { get; set; }
}
