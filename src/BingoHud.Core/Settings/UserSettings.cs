using BingoHud.Core.Usage;

namespace BingoHud.Core.Settings;

/// <summary>
/// Which way the percentage reads on screen (AC-2a). Stored values are always consumed; the
/// inversion happens once, at render, and only when this says <see cref="Remaining"/>.
/// </summary>
public enum DisplayDirection
{
    Consumed,
    Remaining,
}

/// <summary>
/// Where the HUD sits, in WPF device-independent units, as the shell reports its window
/// position. Negative values are legitimate on a monitor left of or above the primary one.
/// </summary>
public sealed record HudPosition(double Left, double Top);

/// <summary>
/// Everything the user can change that has to survive a restart (AC-22).
/// </summary>
/// <param name="Position">Null until the user has placed the HUD; the shell picks a spot.</param>
/// <param name="Collapse">Show only the worst window unless both are non-normal (AC-7).</param>
/// <param name="Direction">Consumed or remaining (AC-2a).</param>
/// <param name="Thresholds">Where the warning and critical lines sit.</param>
public sealed record UserSettings(
    HudPosition? Position,
    bool Collapse,
    DisplayDirection Direction,
    Thresholds Thresholds)
{
    /// <summary>
    /// The spec's defaults: not yet placed, both windows shown, consumed, 25 and 10 remaining.
    /// </summary>
    public static UserSettings Default { get; } = new(
        Position: null,
        Collapse: false,
        Direction: DisplayDirection.Consumed,
        Thresholds: Thresholds.Default);
}
