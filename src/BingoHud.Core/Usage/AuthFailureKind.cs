namespace BingoHud.Core.Usage;

/// <summary>
/// Why authentication failed, to the extent it can be established.
///
/// <para>
/// The distinction matters because each kind sends the user somewhere different, and sending
/// them to the wrong place costs more than saying nothing would. The rule is that a kind is
/// only claimed on evidence: when the response says nothing about why, the honest answer is
/// <see cref="Unspecified"/>.
/// </para>
/// </summary>
public enum AuthFailureKind
{
    /// <summary>
    /// Authentication failed and the response did not say why. The user is told to sign in
    /// again, without a diagnosis attached.
    /// </summary>
    Unspecified,

    /// <summary>
    /// The server explicitly rejected the token. Observed 2026-08-30: an <c>error.type</c> of
    /// <c>authentication_error</c> alongside a 401.
    /// </summary>
    Invalidated,
}
