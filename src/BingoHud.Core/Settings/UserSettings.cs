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
/// How each window's reset reads on the HUD. <see cref="ClockTime"/> is 0.1.0's phrase: a clock
/// time while the reset is distant, a countdown inside the last hour. <see cref="Countdown"/> is a
/// short countdown at every distance ("4.5d", "2.3h", "53m"). The detail panel shows the exact
/// time either way.
/// </summary>
public enum ResetFormat
{
    ClockTime,
    Countdown,
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
/// <param name="ResetFormat">Clock time or countdown for each window's reset.</param>
/// <param name="ShowBar">Whether each window's figure has a ten-segment bar beside it.</param>
/// <param name="Products">
/// Which products are shown, or null until the first-start question has been answered
/// (codex-quota AC-8a). Null is not "Claude only": it is what makes Bingo ask, on a new install
/// and on the first start after upgrading from a version that had no such question.
/// </param>
public sealed record UserSettings(
    HudPosition? Position,
    bool Collapse,
    DisplayDirection Direction,
    Thresholds Thresholds,
    ResetFormat ResetFormat,
    bool ShowBar,
    ProductSwitches? Products = null)
{
    /// <summary>
    /// The spec's defaults: not yet placed, both windows shown, consumed, 25 and 10 remaining,
    /// clock-time resets, no bar. The last two keep the HUD exactly as 0.1.0 drew it until the
    /// user asks otherwise.
    /// </summary>
    public static UserSettings Default { get; } = new(
        Position: null,
        Collapse: false,
        Direction: DisplayDirection.Consumed,
        Thresholds: Thresholds.Default,
        ResetFormat: ResetFormat.ClockTime,
        ShowBar: false);

    /// <summary>
    /// The products actually shown: the answer when there is a usable one, else Claude alone, as
    /// 0.4.0 showed it. An answer with nothing on is refused when the file is read, but a value
    /// built in code is not read from a file, and a HUD with nothing on looks like a crash.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ProductSwitches Shown => Products is { AnyOn: true } answered ? answered : ProductSwitches.ClaudeOnly;
}
