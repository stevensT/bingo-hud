namespace BingoHud.Core.Time;

/// <summary>
/// The real clock. The only implementation of <see cref="IClock"/> that ships in the app.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <summary>
    /// Local time rather than UTC, so the offset needed to render reset times is carried by
    /// the value itself instead of being reapplied later by whoever happens to display it.
    /// </summary>
    public DateTimeOffset Now => DateTimeOffset.Now;
}
