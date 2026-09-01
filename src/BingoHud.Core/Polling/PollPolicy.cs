namespace BingoHud.Core.Polling;

/// <summary>
/// Decides how long to wait before the next fetch.
///
/// <para>
/// A pure function over <see cref="PollSignals"/>. It reads no clock, no power state, and no
/// network, which is what makes the entire cadence a table a test can walk in milliseconds
/// rather than a behaviour that can only be observed by waiting.
/// </para>
/// <para>
/// It returns a named reason as well as a delay, and the reason is shown in the detail panel. A
/// user who notices the number has not moved in twenty minutes deserves to be told why, rather
/// than left inferring the existence of a timer.
/// </para>
/// </summary>
public static class PollPolicy
{
    /// <summary>
    /// Never poll faster than this.
    ///
    /// <para>
    /// This is someone else's undocumented service, and utilization only moves while Claude Code
    /// is working. Polling harder cannot make a number arrive before the server changes it; it
    /// can only spend the rate limit the user is trying to watch.
    /// </para>
    /// </summary>
    public static TimeSpan Floor { get; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The slowest cadence Bingo chooses for itself. The server may still ask for longer.
    /// </summary>
    public static TimeSpan Ceiling { get; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How recently the panel must have been open for the user to count as watching.
    /// </summary>
    private static readonly TimeSpan Watching = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How recently Claude Code must have written a transcript for its work to count as
    /// ongoing.
    /// </summary>
    private static readonly TimeSpan Working = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The cadence used while Claude Code is actively working — the one situation where
    /// utilization is genuinely moving.
    /// </summary>
    private static readonly TimeSpan WorkingCadence = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The reasons a delay can be chosen, named so they can be displayed and asserted rather
    /// than reconstructed from a number.
    /// </summary>
    public static class Reasons
    {
        public const string ServerAskedToWait = "the server asked us to wait";
        public const string LastAttemptFailed = "the last attempt failed";
        public const string PowerConstrained = "the machine is on battery";
        public const string UserIsWatching = "the panel is open";
        public const string ClaudeCodeIsWorking = "Claude Code is working";
        public const string NothingIsHappening = "nothing is happening";
    }

    /// <summary>
    /// The delay before the next fetch, and why.
    /// </summary>
    public static (TimeSpan Delay, string Reason) NextDelay(PollSignals signals)
    {
        // Ordered by precedence, most binding first. Each rule is one row of the table.
        if (signals.ServerRetryAfter is { } retryAfter)
        {
            // Honoured even past the ceiling. The ceiling is Bingo's own restraint, not a
            // licence to come back sooner than the service asked.
            return (Max(retryAfter, Floor), Reasons.ServerAskedToWait);
        }

        if (signals.LastAttemptFailed)
        {
            // There is no attempt counter in the signals, so there is no exponential curve to
            // ride. When an undocumented endpoint is not answering, the slowest cadence is both
            // the politest guess and the cheapest one.
            return (Ceiling, Reasons.LastAttemptFailed);
        }

        if (signals.SinceUserOpenedPanel is { } sincePanel && sincePanel <= Watching)
        {
            // Someone is looking at the numbers. The only case where a fast cadence buys
            // anything — and it deliberately outranks the battery rule below. Power saving is
            // for when the app is unattended; a user with the panel open is attending and
            // asking, and the cost is bounded because they close it.
            return (Floor, Reasons.UserIsWatching);
        }

        if (signals.PowerConstrained)
        {
            return (Ceiling, Reasons.PowerConstrained);
        }

        if (signals.SinceLocalTranscriptActivity is { } sinceActivity && sinceActivity <= Working)
        {
            return (WorkingCadence, Reasons.ClaudeCodeIsWorking);
        }

        // Includes the case where every signal is unknown. Absence of evidence is not a reason
        // to poll hard.
        return (Ceiling, Reasons.NothingIsHappening);
    }

    private static TimeSpan Max(TimeSpan first, TimeSpan second) => first > second ? first : second;
}
