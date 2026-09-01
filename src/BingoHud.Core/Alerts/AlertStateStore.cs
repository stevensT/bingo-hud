using System.Text.Json;
using System.Text.Json.Serialization;
using BingoHud.Core.Time;

namespace BingoHud.Core.Alerts;

/// <summary>
/// Which alerts have already fired, held in a small JSON file so a restart does not re-announce
/// them.
///
/// <para>
/// The file holds nothing secret and is written in a shape a person can read, with the window
/// kind spelled out rather than numbered — state recorded as an enum's ordinal would quietly
/// change meaning the day a kind is inserted in the middle of the list, and the symptom would be
/// a missing alert rather than an error.
/// </para>
/// <para>
/// Every file failure degrades to in-memory behaviour instead of throwing. The worst outcome
/// this state can produce is a duplicate notification, which is not worth failing a poll over.
/// </para>
/// </summary>
public sealed class AlertStateStore : IAlertStateStore
{
    private static readonly JsonSerializerOptions Format = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly HashSet<AlertKey> _fired;

    /// <summary>
    /// Loads the file, dropping anything whose window has already reset.
    ///
    /// <para>
    /// Pruning on load is what keeps the file bounded without anything having to own a timer for
    /// it. A window that has reset can never be alerted about under the same key again, so its
    /// entry is dead weight the moment the app next starts.
    /// </para>
    /// </summary>
    public AlertStateStore(string path, IClock clock)
    {
        _path = path;
        _fired = Load(path);

        if (_fired.RemoveWhere(key => key.ResetsAt < clock.Now) > 0)
        {
            Save();
        }
    }

    public bool HasFired(AlertKey key) => _fired.Contains(key);

    public void MarkFired(AlertKey key)
    {
        if (_fired.Add(key))
        {
            Save();
        }
    }

    /// <summary>
    /// Forgets alerts for windows that reset before the given instant. An alert is kept right up
    /// to its reset instant, since the window is still current until then.
    /// </summary>
    public void Prune(DateTimeOffset before)
    {
        if (_fired.RemoveWhere(key => key.ResetsAt < before) > 0)
        {
            Save();
        }
    }

    /// <summary>The file's shape. An object rather than a bare array, so it has somewhere to grow.</summary>
    private sealed record StoredState(List<AlertKey>? Fired);

    private static HashSet<AlertKey> Load(string path)
    {
        try
        {
            var state = JsonSerializer.Deserialize<StoredState>(File.ReadAllText(path), Format);

            return state?.Fired is { } fired ? [.. fired] : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException
            or NotSupportedException or ArgumentException or JsonException)
        {
            // Missing, locked, denied, or in a shape this version does not recognize. All of
            // them mean the same thing: nothing is known to have fired yet.
            return [];
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(new StoredState([.. _fired]), Format));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException
            or NotSupportedException or ArgumentException)
        {
            // deferred: state stays in memory for this session. Add a surfaced warning if losing
            // it ever proves to matter more than a duplicate notification does.
        }
    }
}
