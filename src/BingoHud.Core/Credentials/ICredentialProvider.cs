namespace BingoHud.Core.Credentials;

/// <summary>
/// Where the OAuth token comes from.
///
/// <para>
/// Two methods rather than one, because "there is no usable token" and "here is why" are
/// genuinely different questions. The first is asked on every poll and its answer is always the
/// same shape; the second is asked only once something has gone wrong, and its answer decides
/// what the user is told to do about it.
/// </para>
/// </summary>
public interface ICredentialProvider
{
    /// <summary>The current token, or null when there is not a usable one.</summary>
    Task<Credential?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Why there was not one.</summary>
    CredentialAvailability Probe();
}
