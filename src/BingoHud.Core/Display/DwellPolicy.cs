namespace BingoHud.Core.Display;

/// <summary>
/// When the HUD is solid and when it lets clicks through (AC-21).
///
/// <para>
/// The HUD receives no mouse input while it is click-through, so this is fed by a timer that
/// polls the cursor position rather than by mouse events. A cursor that rests on the HUD for
/// <see cref="Dwell"/> makes it solid; one that passes over, or clicks without pausing, does
/// not. Solid lasts until the cursor leaves, and leaving restarts the clock.
/// </para>
/// </summary>
public sealed class DwellPolicy
{
    /// <summary>
    /// Long enough that a cursor crossing the HUD never trips it; short enough that stopping on
    /// it does not feel like waiting.
    /// </summary>
    public static readonly TimeSpan Dwell = TimeSpan.FromMilliseconds(400);

    private DateTimeOffset? _restingSince;

    /// <summary>
    /// One observation of where the cursor is. Returns whether the HUD should be solid now.
    /// </summary>
    public bool Update(bool cursorOverHud, DateTimeOffset now)
    {
        if (!cursorOverHud)
        {
            _restingSince = null;
            return false;
        }

        _restingSince ??= now;
        return now - _restingSince.Value >= Dwell;
    }
}
