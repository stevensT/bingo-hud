using System.Globalization;
using System.Text.Json;

namespace BingoHud.Core.Usage;

/// <summary>
/// Turns a usage-endpoint response body into a <see cref="FetchOutcome"/>.
///
/// <para>
/// Tolerant in, strict out. Unknown top-level keys are ignored, because the live payload
/// carries several of them and any rule that failed on an unrecognized key would fail on every
/// real response. What is not tolerated is a known window that is present and malformed: that
/// is <see cref="FetchOutcome.Unreadable"/>, never a zero.
/// </para>
/// <para>
/// Two parse paths. The <c>limits</c> array is preferred: it is self-describing, needs no alias
/// map, and carries the server's own severity. The flat window keys are read only when
/// <c>limits</c> is absent entirely — they are a fallback, not a supplement, and reading both
/// would double every window.
/// </para>
/// <para>
/// A pure function over the body and the instant it was observed. The clock belongs to the
/// caller.
/// </para>
/// </summary>
public static class UsageNormalizer
{
    /// <summary>
    /// The flat keys each window has been seen under, in the order they are preferred.
    ///
    /// <para>
    /// This is an allow-list, and that is the whole point of it. The live payload carries keys
    /// for unreleased features whose values are shaped exactly like a window —
    /// <c>nimbus_quill</c> reports a utilization of 0 — so a fallback that recognized windows
    /// by their shape would invent a window nobody has a quota for and put an unbacked 0% on
    /// screen. Only these names are windows.
    /// </para>
    /// </summary>
    private static readonly (WindowKind Kind, string[] Keys)[] FlatWindowKeys =
    [
        (WindowKind.Session, ["five_hour", "5_hour", "session", "primary"]),
        (WindowKind.WeeklyAll, ["seven_day", "7_day", "weekly", "week", "secondary"]),
    ];

    /// <summary>
    /// Parses a response body into windows.
    /// </summary>
    /// <param name="rawBody">The response body exactly as it arrived.</param>
    /// <param name="observedAt">When the response was received.</param>
    public static FetchOutcome Normalize(string rawBody, DateTimeOffset observedAt)
    {
        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;

        var windows = ReadLimitsArray(root) ?? ReadFlatWindows(root);

        if (windows.Count == 0)
        {
            return new FetchOutcome.Unreadable(
                "The response carried no quota window this version recognizes.");
        }

        return new FetchOutcome.Success(new QuotaSnapshot(windows, observedAt, rawBody));
    }

    /// <summary>
    /// Reads the <c>limits</c> array, or null when it is absent — which is the signal to fall
    /// back to the flat keys. An empty array is present, not absent: it means the server
    /// reported no windows, and no fallback can improve on that.
    /// </summary>
    private static List<QuotaWindow>? ReadLimitsArray(JsonElement root)
    {
        if (!root.TryGetProperty("limits", out var limits)
            || limits.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var windows = new List<QuotaWindow>();

        foreach (var limit in limits.EnumerateArray())
        {
            var window = ReadLimit(limit);

            if (window is not null)
            {
                windows.Add(window);
            }
        }

        return windows;
    }

    /// <summary>
    /// Reads one entry of the <c>limits</c> array, or null if it describes a window this
    /// version does not understand.
    /// </summary>
    private static QuotaWindow? ReadLimit(JsonElement limit)
    {
        if (!limit.TryGetProperty("kind", out var kind))
        {
            return null;
        }

        var windowKind = ReadWindowKind(kind.GetString());

        if (windowKind is null)
        {
            return null;
        }

        return new QuotaWindow(
            windowKind.Value,
            limit.GetProperty("percent").GetDouble(),
            ReadResetsAt(limit));
    }

    /// <summary>
    /// Maps the server's <c>kind</c> string, or null when it names a window Bingo does not
    /// know. Skipping is the right answer rather than failing: the endpoint has already been
    /// observed carrying keys for features that do not exist yet.
    /// </summary>
    private static WindowKind? ReadWindowKind(string? kind) => kind switch
    {
        "session" => WindowKind.Session,
        "weekly_all" => WindowKind.WeeklyAll,
        _ => null,
    };

    /// <summary>
    /// Reads the flat window keys. At most one window per kind: a payload carrying two aliases
    /// for the same window describes one window twice, not two windows.
    /// </summary>
    private static List<QuotaWindow> ReadFlatWindows(JsonElement root)
    {
        var windows = new List<QuotaWindow>();

        foreach (var (kind, keys) in FlatWindowKeys)
        {
            foreach (var key in keys)
            {
                if (!root.TryGetProperty(key, out var flat)
                    || flat.ValueKind != JsonValueKind.Object
                    || !flat.TryGetProperty("utilization", out var utilization)
                    || utilization.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                windows.Add(new QuotaWindow(
                    kind,
                    utilization.GetDouble(),
                    ReadResetsAt(flat)));

                break;
            }
        }

        return windows;
    }

    /// <summary>
    /// Reads <c>resets_at</c>. Absent and null are the same thing — a window with no reset
    /// time — and both are ordinary, not malformed.
    /// </summary>
    private static DateTimeOffset? ReadResetsAt(JsonElement window)
    {
        if (!window.TryGetProperty("resets_at", out var resetsAt)
            || resetsAt.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTimeOffset.Parse(
            resetsAt.GetString()!,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
    }
}
