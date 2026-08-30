namespace BingoHud.Core.Usage;

/// <summary>
/// The single discriminated outcome of one attempt to read the usage endpoint.
///
/// <para>
/// There is deliberately no "success with partial data" case. Either a response yielded windows
/// we recognize, or it did not and the app says so and shows nothing. A zero is a reading; the
/// absence of a reading must never be able to look like one.
/// </para>
/// </summary>
public abstract record FetchOutcome
{
    private FetchOutcome() { }

    /// <summary>A response that parsed into at least one recognized window.</summary>
    public sealed record Success(QuotaSnapshot Snapshot) : FetchOutcome;

    /// <summary>
    /// A response arrived but yielded nothing we recognize — either a known window was present
    /// and malformed, or no known window was found at all.
    /// </summary>
    /// <param name="Reason">
    /// What was wrong, for the detail panel. Never contains any part of the credential.
    /// </param>
    public sealed record Unreadable(string Reason) : FetchOutcome;
}
