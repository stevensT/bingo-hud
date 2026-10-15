using BingoHud.Core.Time;

namespace BingoHud.Core.Alerts;

/// <summary>
/// Holds new crossings while a notification is visible, because Windows can drop overlapping
/// notifications. Called on the UI thread; closing or clicking the visible one releases the
/// waiting crossings as one batch.
///
/// <para>
/// Windows does not promise to say when a notification ends. One that was suppressed, or went
/// straight to the notification centre, may never report closing, so a notification older than
/// <see cref="ShowingLimit"/> is treated as ended. Without that, one silent notification would
/// hold every later alert for the rest of the session.
/// </para>
/// </summary>
public sealed class AlertDelivery(IClock clock)
{
    /// <summary>
    /// How long a notification is waited on before it is taken to have ended unreported. Longer
    /// than Windows shows one by default, so an ordinary notification still ends itself first.
    /// </summary>
    public static TimeSpan ShowingLimit { get; } = TimeSpan.FromMinutes(1);

    private readonly IClock _clock = clock;
    private readonly List<Alert> _waiting = [];
    private DateTimeOffset? _showingSince;

    public IReadOnlyList<Alert> Add(IReadOnlyList<Alert> due)
    {
        _waiting.AddRange(due);

        if (_showingSince is { } since && _clock.Now - since < ShowingLimit)
        {
            return [];
        }

        if (_waiting.Count == 0)
        {
            _showingSince = null;
            return [];
        }

        var next = _waiting.ToArray();
        _waiting.Clear();
        _showingSince = _clock.Now;
        return next;
    }

    public void Clear() => _waiting.Clear();

    public IReadOnlyList<Alert> Closed()
    {
        _showingSince = null;
        return Add([]);
    }
}
