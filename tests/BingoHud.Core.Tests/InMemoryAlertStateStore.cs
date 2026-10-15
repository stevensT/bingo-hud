using BingoHud.Core.Alerts;

namespace BingoHud.Core.Tests;

/// <summary>
/// The alert record without the file, for tests about what is decided rather than about what
/// survives a restart.
/// </summary>
internal sealed class InMemoryAlertStateStore : IAlertStateStore
{
    private readonly HashSet<AlertKey> _fired = [];

    // Matched the way the real store matches, so engine tests see the same tolerance.
    public bool HasFired(AlertKey key) => _fired.Any(fired => fired.SameOccurrenceAs(key));

    public void MarkFired(AlertKey key)
    {
        if (!HasFired(key))
        {
            _fired.Add(key);
        }
    }

    public int Count => _fired.Count;
}
