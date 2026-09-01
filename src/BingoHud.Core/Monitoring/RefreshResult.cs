namespace BingoHud.Core.Monitoring;

/// <summary>
/// What came of asking for a refresh.
/// </summary>
public abstract record RefreshResult
{
    private RefreshResult() { }

    /// <summary>A fetch happened, and this is the state it produced.</summary>
    public sealed record Performed(ReadingState State) : RefreshResult;

    /// <summary>
    /// The refresh was declined because the backoff has not elapsed.
    ///
    /// <para>
    /// A manual refresh is subject to the same floor as an automatic poll (AC-28), so refusing
    /// is a normal outcome rather than an error. It carries both halves of what a user needs:
    /// why it was refused, and when asking again will work.
    /// </para>
    /// </summary>
    public sealed record Refused(string Reason, DateTimeOffset NextAttemptAt) : RefreshResult;
}
