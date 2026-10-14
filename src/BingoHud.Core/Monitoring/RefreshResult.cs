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
    /// The refresh was declined because the floor or a backoff has not elapsed.
    ///
    /// <para>
    /// A manual refresh is subject to the same floor as an automatic poll (AC-28), so refusing
    /// is a normal outcome rather than an error. It carries both halves of what a user needs:
    /// why it was refused, and when asking again will work.
    /// </para>
    /// </summary>
    /// <param name="Reason">Why, as a clause that reads after "because".</param>
    /// <param name="NextAttemptAt">
    /// When asking again will be accepted. Not <see cref="QuotaMonitor.NextAttemptAt"/>, which
    /// is when the cadence will next poll and can be later.
    /// </param>
    public sealed record Refused(string Reason, DateTimeOffset NextAttemptAt) : RefreshResult;

    /// <summary>
    /// The refresh was declined because the endpoint has said it will not serve this account.
    /// Nothing is fetched again until a restart, which is what the status advice promises.
    /// </summary>
    public sealed record Stopped : RefreshResult;
}
