namespace BingoHud.Core.Credentials;

/// <summary>
/// An OAuth access token read from Claude Code's credential file, with its expiry if one was
/// recorded.
///
/// <para>
/// <see cref="ToString"/> is overridden to redact the token, and that is not decoration. A
/// positional record generates a <c>ToString</c> that prints every property, so the default
/// behaviour here would be to write the token into any log line, exception message, or debugger
/// watch that happened to touch the object. Redacting it at the type means the leak cannot
/// happen by accident somewhere else.
/// </para>
/// </summary>
/// <param name="AccessToken">The bearer token. Never logged, never written anywhere.</param>
/// <param name="ExpiresAt">
/// When the token expires, if the file recorded it. Null means the file did not say — not that
/// the token lasts forever.
/// </param>
public sealed record Credential(string AccessToken, DateTimeOffset? ExpiresAt)
{
    /// <summary>
    /// Whether the token has passed its expiry as of <paramref name="instant"/>.
    ///
    /// <para>
    /// A token with no recorded expiry is never reported as expired. The file did not say, and
    /// declaring it dead would invent a sign-in prompt out of a missing field — the server's
    /// answer to an actual request is the only evidence that settles it.
    /// </para>
    /// <para>
    /// The instant is passed in rather than read from the clock, because in real use this
    /// crossing happens about once a day and in a test run it happens never.
    /// </para>
    /// </summary>
    public bool HasExpiredAt(DateTimeOffset instant) =>
        ExpiresAt is { } expiry && expiry <= instant;

    public override string ToString() =>
        $"Credential {{ AccessToken = <redacted>, ExpiresAt = {ExpiresAt?.ToString("o") ?? "null"} }}";
}
