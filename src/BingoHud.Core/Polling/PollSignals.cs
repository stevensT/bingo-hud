namespace BingoHud.Core.Polling;

/// <summary>
/// Everything the poll cadence is allowed to depend on, gathered by the caller immediately
/// before a decision is made.
///
/// <para>
/// The point of collecting it into a record is that <see cref="PollPolicy"/> then reads nothing
/// at all — no clock, no power state, no network. The whole cadence becomes a table that can be
/// tested by writing values into this record, with no mocking and no waiting.
/// </para>
/// </summary>
/// <param name="PowerConstrained">
/// The machine is on battery or in a power-saving mode. Polling someone else's undocumented
/// endpoint is not worth spending a battery on.
/// </param>
/// <param name="SinceUserOpenedPanel">
/// How long ago the user last opened the detail panel, or null if they have not. Someone looking
/// at the panel is the one case where a fast cadence is actually useful.
/// </param>
/// <param name="SinceLocalTranscriptActivity">
/// How long ago Claude Code last wrote to a local transcript, or null if unknown. Utilization
/// only moves when Claude Code is working, so this is the best available proxy for "is there
/// anything to see".
/// </param>
/// <param name="LastAttemptFailed">Whether the previous fetch failed for any reason.</param>
/// <param name="ServerRetryAfter">
/// A <c>Retry-After</c> the server sent, or null. When present it outranks everything else here.
/// </param>
public sealed record PollSignals(
    bool PowerConstrained = false,
    TimeSpan? SinceUserOpenedPanel = null,
    TimeSpan? SinceLocalTranscriptActivity = null,
    bool LastAttemptFailed = false,
    TimeSpan? ServerRetryAfter = null);
