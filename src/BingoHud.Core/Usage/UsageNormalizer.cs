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
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(rawBody);
        }
        catch (JsonException)
        {
            // Realistically an intermediary answering instead of the API — a proxy error page,
            // a captive portal. Nothing about it is worth showing beyond the fact of it.
            return new FetchOutcome.Unreadable("The response body was not valid JSON.");
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new FetchOutcome.Unreadable("The response body was not a JSON object.");
            }

            var reading = ReadLimitsArray(root) ?? ReadFlatWindows(root);

            if (reading.MalformedReason is not null)
            {
                return new FetchOutcome.Unreadable(reading.MalformedReason);
            }

            if (reading.Windows.Count == 0)
            {
                return new FetchOutcome.Unreadable(
                    "The response carried no quota window this version recognizes.");
            }

            return new FetchOutcome.Success(
                new QuotaSnapshot(reading.Windows, observedAt, rawBody));
        }
    }

    /// <summary>
    /// Classifies the body of a response that failed authentication.
    /// </summary>
    /// <param name="rawBody">The response body exactly as it arrived.</param>
    public static FetchOutcome ClassifyAuthFailure(string rawBody)
    {
        return new FetchOutcome.AuthFailed(ReadAuthFailureKind(rawBody));
    }

    /// <summary>
    /// Looks for the server's own statement that the token was rejected. Anything else — a
    /// different error type, a body from an intermediary, no body at all — leaves the kind
    /// unspecified rather than assuming.
    /// </summary>
    private static AuthFailureKind ReadAuthFailureKind(string rawBody)
    {
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(rawBody);
        }
        catch (JsonException)
        {
            return AuthFailureKind.Unspecified;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("error", out var error)
                || error.ValueKind != JsonValueKind.Object
                || !error.TryGetProperty("type", out var type)
                || type.ValueKind != JsonValueKind.String)
            {
                return AuthFailureKind.Unspecified;
            }

            return type.GetString() == "authentication_error"
                ? AuthFailureKind.Invalidated
                : AuthFailureKind.Unspecified;
        }
    }

    /// <summary>
    /// The result of reading one parse path: the windows found, or the reason the payload was
    /// rejected.
    ///
    /// <para>
    /// The two are kept apart deliberately. "No windows found" and "a window was found and it
    /// was broken" both end as <see cref="FetchOutcome.Unreadable"/>, but they mean different
    /// things — one is an account with nothing to report, the other is a payload that has
    /// moved — so the detail panel can tell them apart.
    /// </para>
    /// </summary>
    private sealed record WindowReading(List<QuotaWindow> Windows, string? MalformedReason)
    {
        public static WindowReading Found(List<QuotaWindow> windows) => new(windows, null);

        public static WindowReading Malformed(string reason) => new([], reason);
    }

    /// <summary>
    /// Reads the <c>limits</c> array, or null when it is absent — which is the signal to fall
    /// back to the flat keys. An empty array is present, not absent: it means the server
    /// reported no windows, and no fallback can improve on that.
    /// </summary>
    private static WindowReading? ReadLimitsArray(JsonElement root)
    {
        if (!root.TryGetProperty("limits", out var limits)
            || limits.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var windows = new List<QuotaWindow>();

        foreach (var limit in limits.EnumerateArray())
        {
            if (limit.ValueKind != JsonValueKind.Object)
            {
                return WindowReading.Malformed("An entry of the limits array was not an object.");
            }

            if (ReadWindowKind(limit) is not { } kind)
            {
                // A window this version does not know. Skipped, not rejected: the endpoint has
                // already been observed carrying features that do not exist yet.
                continue;
            }

            if (!TryReadPercent(limit, "percent", out var percent))
            {
                return WindowReading.Malformed(
                    $"The {Describe(kind)} window reported no usable percentage.");
            }

            if (!TryReadResetsAt(limit, out var resetsAt))
            {
                return WindowReading.Malformed(
                    $"The {Describe(kind)} window reported an unreadable reset time.");
            }

            windows.Add(new QuotaWindow(kind, percent, resetsAt, ReadSeverity(limit)));
        }

        return WindowReading.Found(windows);
    }

    /// <summary>
    /// Reads the flat window keys. At most one window per kind: a payload carrying two aliases
    /// for the same window describes one window twice, not two windows.
    /// </summary>
    private static WindowReading ReadFlatWindows(JsonElement root)
    {
        var windows = new List<QuotaWindow>();

        foreach (var (kind, keys) in FlatWindowKeys)
        {
            foreach (var key in keys)
            {
                // A key that is absent, or present and null, reported nothing under this
                // alias. Both are ordinary — the live payload nulls several window keys — so
                // the next alias gets its turn.
                if (!root.TryGetProperty(key, out var flat)
                    || flat.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!TryReadPercent(flat, "utilization", out var percent))
                {
                    return WindowReading.Malformed(
                        $"The {Describe(kind)} window reported no usable percentage.");
                }

                if (!TryReadResetsAt(flat, out var resetsAt))
                {
                    return WindowReading.Malformed(
                        $"The {Describe(kind)} window reported an unreadable reset time.");
                }

                windows.Add(new QuotaWindow(kind, percent, resetsAt, ReadSeverity(flat)));

                break;
            }
        }

        return WindowReading.Found(windows);
    }

    /// <summary>
    /// Maps an entry's <c>kind</c> string, or null when it names a window Bingo does not know.
    /// </summary>
    private static WindowKind? ReadWindowKind(JsonElement limit)
    {
        if (!limit.TryGetProperty("kind", out var kind)
            || kind.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return kind.GetString() switch
        {
            "session" => WindowKind.Session,
            "weekly_all" => WindowKind.WeeklyAll,
            _ => null,
        };
    }

    /// <summary>
    /// How a window is named in a message the user may end up reading.
    /// </summary>
    private static string Describe(WindowKind kind) => kind switch
    {
        WindowKind.Session => "5-hour",
        WindowKind.WeeklyAll => "weekly",
        _ => "per-model weekly",
    };

    /// <summary>
    /// Reads a percentage. Absent or non-numeric is a failure rather than a zero: the window
    /// declared itself, so a number that cannot be read is a broken reading and not an empty
    /// one.
    /// </summary>
    private static bool TryReadPercent(JsonElement window, string property, out double percent)
    {
        percent = 0;

        return window.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out percent);
    }

    /// <summary>
    /// Reads <c>resets_at</c>. Absent and null are the same thing — a window with no reset time
    /// — and both succeed, yielding null. A string that will not parse is a failure, because
    /// the server did report a reset time and we cannot show it.
    /// </summary>
    private static bool TryReadResetsAt(JsonElement window, out DateTimeOffset? resetsAt)
    {
        resetsAt = null;

        if (!window.TryGetProperty("resets_at", out var value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.String
            || !DateTimeOffset.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return false;
        }

        resetsAt = parsed;
        return true;
    }

    /// <summary>
    /// Reads <c>severity</c>. Anything not recognized — an unfamiliar spelling, a null, a
    /// non-string, or no field at all — is <see cref="ServerSeverity.Unknown"/>. Never
    /// <see cref="ServerSeverity.Normal"/>: a default of "fine" would hide the escalation this
    /// field exists to report.
    /// </summary>
    private static ServerSeverity ReadSeverity(JsonElement window)
    {
        if (!window.TryGetProperty("severity", out var severity)
            || severity.ValueKind != JsonValueKind.String)
        {
            return ServerSeverity.Unknown;
        }

        return severity.GetString() switch
        {
            "normal" => ServerSeverity.Normal,
            "warning" => ServerSeverity.Warning,
            "critical" => ServerSeverity.Critical,
            "rejected" => ServerSeverity.Rejected,
            _ => ServerSeverity.Unknown,
        };
    }
}
