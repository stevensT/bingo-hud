using System.Globalization;
using System.Text.Json;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Codex;

/// <summary>
/// Reads one line of a Codex session log as a rate-limit record (codex-quota AC-1).
///
/// <para>
/// A record is a line whose <c>type</c> is <c>event_msg</c> and whose <c>payload.type</c> is
/// <c>token_count</c>; its <c>payload.rate_limits</c> holds the windows. Every other line in the
/// log is the user's conversation, so a line is only parsed at all once it contains the text
/// <c>"rate_limits"</c>, and only the record's timestamp and rate-limit object are taken from it.
/// The text test is a prefilter, not the decision: a message that mentions rate limits passes it
/// and is then rejected by its structure.
/// </para>
/// <para>
/// The format is undocumented, so this reads it the way <see cref="UsageNormalizer"/> reads the
/// Claude endpoint: tolerant of fields it does not use, strict about the ones it does. A window
/// that is present and broken makes the record unreadable rather than being skipped, because
/// skipping it would show the other window as a complete reading.
/// </para>
/// </summary>
public static class CodexRecord
{
    private const string RateLimitsKey = "rate_limits";

    // The two window lengths observed in the spike, in minutes. Windows are matched by length
    // because the names primary and secondary say nothing about which window is which.
    private const int SessionMinutes = 300;
    private const int WeeklyMinutes = 10080;

    private static readonly string[] WindowNames = ["primary", "secondary"];

    public static CodexRecordResult Parse(string line)
    {
        if (!line.Contains($"\"{RateLimitsKey}\"", StringComparison.Ordinal))
        {
            return new CodexRecordResult.NotARecord();
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return new CodexRecordResult.Unreadable(
                "A line that mentions rate limits was not valid JSON.");
        }

        using (document)
        {
            var root = document.RootElement;

            if (!IsRateLimitRecord(root, out var payload))
            {
                return new CodexRecordResult.NotARecord();
            }

            if (!payload.TryGetProperty(RateLimitsKey, out var rateLimits))
            {
                return new CodexRecordResult.Unreadable(
                    "A token count record carried no rate_limits field.");
            }

            if (rateLimits.ValueKind == JsonValueKind.Null)
            {
                return new CodexRecordResult.NoReading();
            }

            if (rateLimits.ValueKind != JsonValueKind.Object)
            {
                return new CodexRecordResult.Unreadable("The rate_limits field was not an object.");
            }

            if (!TryReadTimestamp(root, out var observedAt))
            {
                return new CodexRecordResult.Unreadable(
                    "The record had no timestamp Bingo could read, so its age is unknown.");
            }

            return ReadWindows(rateLimits, observedAt);
        }
    }

    /// <summary>
    /// Whether this line is a rate-limit record by its structure, and its payload if so.
    /// </summary>
    private static bool IsRateLimitRecord(JsonElement root, out JsonElement payload)
    {
        payload = default;

        return root.ValueKind == JsonValueKind.Object
            && IsString(root, "type", "event_msg")
            && root.TryGetProperty("payload", out payload)
            && payload.ValueKind == JsonValueKind.Object
            && IsString(payload, "type", "token_count");
    }

    private static bool IsString(JsonElement element, string property, string expected) =>
        element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() == expected;

    private static bool TryReadTimestamp(JsonElement root, out DateTimeOffset observedAt)
    {
        observedAt = default;

        return root.TryGetProperty("timestamp", out var timestamp)
            && timestamp.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(
                timestamp.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out observedAt);
    }

    private static CodexRecordResult ReadWindows(JsonElement rateLimits, DateTimeOffset observedAt)
    {
        var windows = new List<QuotaWindow>();
        var ignored = new List<string>();

        foreach (var name in WindowNames)
        {
            // An absent or null window is a window Codex did not report, not a broken one.
            if (!rateLimits.TryGetProperty(name, out var window) || window.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            if (window.ValueKind != JsonValueKind.Object)
            {
                return Unreadable(name, "was not an object");
            }

            if (!TryReadNumber(window, "window_minutes", out var minutes))
            {
                return Unreadable(name, "had no window length");
            }

            if (!TryReadNumber(window, "used_percent", out var usedPercent))
            {
                return Unreadable(name, "had no percentage");
            }

            if (!TryReadReset(window, out var resetsAt))
            {
                return Unreadable(name, "had a reset time Bingo could not read");
            }

            WindowKind? kind = minutes switch
            {
                SessionMinutes => WindowKind.Session,
                WeeklyMinutes => WindowKind.WeeklyAll,
                _ => null,
            };

            if (kind is not { } known)
            {
                ignored.Add($"{name} ({minutes.ToString(CultureInfo.InvariantCulture)} minutes)");
                continue;
            }

            if (windows.Any(w => w.Kind == known))
            {
                return new CodexRecordResult.Unreadable(
                    "The record reported two windows of the same length.");
            }

            windows.Add(new QuotaWindow(known, usedPercent, resetsAt, ServerSeverity.Unknown));
        }

        if (windows.Count == 0)
        {
            return new CodexRecordResult.Unreadable(
                "The record reported no 5-hour or weekly window.");
        }

        return new CodexRecordResult.Figures(
            new QuotaSnapshot(windows, observedAt, rateLimits.GetRawText()),
            ReachedType(rateLimits),
            ignored);
    }

    private static CodexRecordResult.Unreadable Unreadable(string window, string problem) =>
        new($"The {window} window {problem}.");

    private static bool TryReadNumber(JsonElement window, string property, out double value)
    {
        value = default;

        return window.TryGetProperty(property, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetDouble(out value);
    }

    /// <summary>
    /// The reset time from Unix seconds. Null is allowed, as a Claude window may report none: the
    /// window then shows no countdown rather than a guessed one. Anything else that is not a
    /// whole number of seconds is unreadable.
    /// </summary>
    private static bool TryReadReset(JsonElement window, out DateTimeOffset? resetsAt)
    {
        resetsAt = null;

        if (!window.TryGetProperty("resets_at", out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out var seconds))
        {
            return false;
        }

        try
        {
            resetsAt = DateTimeOffset.FromUnixTimeSeconds(seconds);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    /// <summary>
    /// <c>rate_limit_reached_type</c> as written: the string itself, or the raw text of any other
    /// non-null value, so the panel shows what Codex sent rather than nothing.
    /// </summary>
    private static string? ReachedType(JsonElement rateLimits)
    {
        if (!rateLimits.TryGetProperty("rate_limit_reached_type", out var reached)
            || reached.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return reached.ValueKind == JsonValueKind.String ? reached.GetString() : reached.GetRawText();
    }
}
