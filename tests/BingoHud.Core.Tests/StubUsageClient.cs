using BingoHud.Core.Credentials;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// A usage client that answers with whatever the test says, and counts how often it was asked.
///
/// <para>
/// Most of the outcomes the monitor has to handle cannot be produced from the live endpoint on
/// demand, and several — a rate limit, a rejected window, a payload that moved — have never been
/// observed at all. This is the only way to drive those transitions.
/// </para>
/// </summary>
internal sealed class StubUsageClient : IUsageClient
{
    private readonly Func<int, FetchOutcome> _outcome;

    public StubUsageClient(Func<int, FetchOutcome> outcome) => _outcome = outcome;

    /// <summary>Always answers the same way.</summary>
    public StubUsageClient(FetchOutcome outcome) : this(_ => outcome) { }

    /// <summary>Answers with each outcome in turn, repeating the last one thereafter.</summary>
    public static StubUsageClient Sequence(params FetchOutcome[] outcomes) =>
        new(call => outcomes[Math.Min(call, outcomes.Length - 1)]);

    public int Fetches { get; private set; }

    /// <summary>
    /// When set, every fetch waits on this before answering, so a test can hold a request open
    /// and make a second call while the first is genuinely in flight.
    /// </summary>
    public TaskCompletionSource? Gate { get; set; }

    public async Task<FetchOutcome> FetchAsync(
        Credential credential,
        CancellationToken cancellationToken = default)
    {
        var call = Fetches;
        Fetches++;

        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return _outcome(call);
    }
}

/// <summary>
/// A credential provider that answers from memory rather than from a file.
/// </summary>
internal sealed class StubCredentialProvider : ICredentialProvider
{
    private readonly Credential? _credential;
    private readonly CredentialAvailability _availability;

    public StubCredentialProvider(
        Credential? credential,
        CredentialAvailability availability = CredentialAvailability.Readable)
    {
        _credential = credential;
        _availability = availability;
    }

    /// <summary>A provider holding a perfectly ordinary token.</summary>
    public static StubCredentialProvider WithToken(DateTimeOffset? expiresAt = null) =>
        new(new Credential("sk-ant-oat01-example-token", expiresAt));

    /// <summary>A provider with no token, and a reason.</summary>
    public static StubCredentialProvider Without(CredentialAvailability availability) =>
        new(null, availability);

    public int Reads { get; private set; }

    public Task<Credential?> GetAsync(CancellationToken cancellationToken = default)
    {
        Reads++;
        return Task.FromResult(_credential);
    }

    public CredentialAvailability Probe() => _availability;
}
