namespace BingoHud.Core.Usage;

/// <summary>
/// Everything one successfully parsed response told us.
///
/// <para>
/// <see cref="ObservedAt"/> and <see cref="RawBody"/> are not diagnostics bolted on the side;
/// they are what makes the numbers admissible. Every figure Bingo displays has to be traceable
/// to a response actually received and carry its age, so the snapshot holds both the instant it
/// was taken and the body it came from.
/// </para>
/// <para>
/// The raw body is held in memory only. It is an authenticated response and is never written to
/// disk.
/// </para>
/// </summary>
/// <param name="Windows">The windows recognized in this response, in the order reported.</param>
/// <param name="ObservedAt">When this response was received.</param>
/// <param name="RawBody">The response body exactly as it arrived.</param>
public sealed record QuotaSnapshot(
    IReadOnlyList<QuotaWindow> Windows,
    DateTimeOffset ObservedAt,
    string RawBody);
