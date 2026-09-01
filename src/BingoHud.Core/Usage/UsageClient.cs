using System.Net;
using System.Net.Http.Headers;
using BingoHud.Core.Credentials;
using BingoHud.Core.Time;

namespace BingoHud.Core.Usage;

/// <summary>
/// Fetches the usage endpoint and turns one HTTP response into one <see cref="FetchOutcome"/>.
///
/// <para>
/// It makes exactly one request and never retries. Deciding when to come back is the poll
/// policy's job and carrying that decision is the monitor's, which keeps this class a
/// translation from HTTP to outcome and nothing else.
/// </para>
/// <para>
/// The taxonomy is deliberately coarse — authenticate, back off, give up, or read — because the
/// endpoint is undocumented. A finer set of categories would be invention rather than knowledge,
/// and each one would be a branch nothing has ever exercised.
/// </para>
/// <para>
/// A 429 is answered by backing off and nothing else. It never triggers a compensating request
/// against another endpoint (AC-26): the fallback that would do so is a non-goal precisely
/// because it would spend real quota to read a number, while adding load to the very limit that
/// caused the failure.
/// </para>
/// </summary>
public sealed class UsageClient : IUsageClient
{
    private readonly HttpClient _http;
    private readonly IClock _clock;

    public UsageClient(HttpClient http, IClock clock)
    {
        _http = http;
        _clock = clock;
    }

    /// <summary>
    /// The only endpoint Bingo reads. It is undocumented and reverse-engineered from shipping
    /// clients, which is why the payload it returns is pinned by a contract test.
    /// </summary>
    public const string Endpoint = "https://api.anthropic.com/api/oauth/usage";

    /// <summary>
    /// The beta gate the endpoint sits behind.
    /// </summary>
    private const string OauthBeta = "oauth-2025-04-20";

    /// <summary>
    /// Sent as the User-Agent.
    ///
    /// <para>
    /// Pinned deliberately: prior art reports that a generic agent lands in a stricter rate
    /// limit bucket on this endpoint. It is a constant rather than a value read from the
    /// installed CLI, because probing for the CLI adds a filesystem dependency and a failure
    /// mode to every poll. The cost is that it must be bumped by hand, and it will drift from
    /// the real Claude Code version between bumps.
    /// </para>
    /// </summary>
    private const string UserAgent = "claude-code/2.1.251";

    /// <summary>
    /// Reads current usage with the given credential.
    /// </summary>
    public async Task<FetchOutcome> FetchAsync(
        Credential credential,
        CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(credential);

        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller is shutting us down. That is not a failed poll, and reporting it as one
            // would leave a spurious backoff behind.
            throw;
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException)
        {
            // No network, or HttpClient's own timeout — which it reports as a cancellation.
            return new FetchOutcome.Transient(RetryAfter: null);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            return Classify(response, body);
        }
    }

    private static HttpRequestMessage BuildRequest(Credential credential)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);

        // The token goes in a header and never in the URL: a query string ends up in proxy logs
        // and crash reports, and a header does not.
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.AccessToken);
        request.Headers.Add("anthropic-beta", OauthBeta);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return request;
    }

    private FetchOutcome Classify(HttpResponseMessage response, string body)
    {
        var status = (int)response.StatusCode;

        if (response.IsSuccessStatusCode)
        {
            return UsageNormalizer.Normalize(body, _clock.Now);
        }

        if (status is (int)HttpStatusCode.Unauthorized or (int)HttpStatusCode.Forbidden)
        {
            // The status decides that this is an authentication failure; the body is only
            // consulted for why. A rejected request's body can contain anything at all,
            // including something window-shaped.
            return UsageNormalizer.ClassifyAuthFailure(body);
        }

        if (status == (int)HttpStatusCode.TooManyRequests || status >= 500)
        {
            return new FetchOutcome.Transient(ReadRetryAfter(response));
        }

        return new FetchOutcome.Unsupported(status);
    }

    /// <summary>
    /// Reads <c>Retry-After</c>, which the server may send either as a number of seconds or as a
    /// date. A date only means anything relative to now, which is why this class holds a clock.
    /// Null means the server did not say — not that returning immediately is acceptable.
    /// </summary>
    private TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;

        if (retryAfter is null)
        {
            return null;
        }

        if (retryAfter.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter.Date is { } date)
        {
            var wait = date - _clock.Now;

            // A date already in the past is the server saying "now", not a negative wait.
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        return null;
    }
}
