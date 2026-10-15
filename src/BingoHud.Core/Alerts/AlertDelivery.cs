namespace BingoHud.Core.Alerts;

/// <summary>
/// Holds new crossings while a notification is visible, because Windows can drop overlapping
/// notifications. Called on the UI thread; closing or clicking the visible one releases the
/// waiting crossings as one batch.
/// </summary>
public sealed class AlertDelivery
{
    private readonly List<Alert> _waiting = [];
    private bool _showing;

    public IReadOnlyList<Alert> Add(IReadOnlyList<Alert> due)
    {
        _waiting.AddRange(due);

        if (_showing || _waiting.Count == 0)
        {
            return [];
        }

        var next = _waiting.ToArray();
        _waiting.Clear();
        _showing = true;
        return next;
    }

    public void Clear() => _waiting.Clear();

    public IReadOnlyList<Alert> Closed()
    {
        _showing = false;
        return Add([]);
    }
}
