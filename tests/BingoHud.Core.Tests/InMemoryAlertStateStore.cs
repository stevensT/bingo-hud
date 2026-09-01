using BingoHud.Core.Alerts;

namespace BingoHud.Core.Tests;

/// <summary>
/// The alert record without the file, for tests about what is decided rather than about what
/// survives a restart.
/// </summary>
internal sealed class InMemoryAlertStateStore : IAlertStateStore
{
    private readonly HashSet<AlertKey> _fired = [];

    public bool HasFired(AlertKey key) => _fired.Contains(key);

    public void MarkFired(AlertKey key) => _fired.Add(key);

    public void Prune(DateTimeOffset before) => _fired.RemoveWhere(key => key.ResetsAt < before);
}
