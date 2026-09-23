namespace BingoHud.Core;

/// <summary>
/// What a Debug build makes of its two override variables, which point it at another credential
/// file and another endpoint so the error states can be forced and seen on screen. Only a Debug
/// build of the shell reads the variables; this decides what their values are allowed to do.
///
/// <para>
/// The endpoint override redirects the request that carries the token. On its own it would send
/// the real token to whatever URL it names, and a Debug build is what the plain build command
/// produces, so "Debug only" is not fence enough. It is therefore honoured only beside a
/// credential override, and only for an http or https server on this machine. A value that
/// breaks those rules stops startup with the variable named, rather than being quietly ignored
/// or quietly obeyed.
/// </para>
/// </summary>
public static class DebugOverrides
{
    public const string CredentialsVariable = "BINGO_CREDENTIALS_PATH";

    public const string EndpointVariable = "BINGO_USAGE_ENDPOINT";

    /// <summary>
    /// The overrides to apply, from the raw variable values. Blank counts as unset.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The endpoint is set without a credential override, is not an http or https URL, or is not
    /// on this machine.
    /// </exception>
    public static (string? CredentialPath, Uri? Endpoint) Resolve(string? credentialPath, string? endpoint)
    {
        var credentials = string.IsNullOrWhiteSpace(credentialPath) ? null : credentialPath;

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return (credentials, null);
        }

        if (credentials is null)
        {
            throw new InvalidOperationException(
                $"{EndpointVariable} is set but {CredentialsVariable} is not, so the real token " +
                "would be sent to the override. Set both, or neither.");
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                $"{EndpointVariable} must be an http or https URL, and \"{endpoint}\" is not.");
        }

        if (!uri.IsLoopback)
        {
            throw new InvalidOperationException(
                $"{EndpointVariable} must point at this machine, and \"{endpoint}\" does not.");
        }

        return (credentials, uri);
    }
}
