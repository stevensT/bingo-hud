using BingoHud.Core.Credentials;

namespace BingoHud.Core.Usage;

/// <summary>
/// Reads the usage endpoint once.
///
/// <para>
/// The seam exists so the monitor can be driven through every outcome — rate limits, server
/// errors, invalidated tokens, payloads that moved — none of which can be produced on demand
/// from the live endpoint, and several of which have never been observed at all.
/// </para>
/// </summary>
public interface IUsageClient
{
    Task<FetchOutcome> FetchAsync(Credential credential, CancellationToken cancellationToken = default);
}
