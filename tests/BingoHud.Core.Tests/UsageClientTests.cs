using System.Net;
using System.Net.Http.Headers;
using BingoHud.Core.Credentials;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// One HTTP response becomes one <see cref="FetchOutcome"/>.
///
/// <para>
/// This is where the error taxonomy lives, and it is the piece AC-10 was waiting on: before it,
/// nothing decided that a 401 is an authentication failure rather than just a number. The rules
/// are deliberately coarse — authentication, transient, unsupported, or a reading — because the
/// endpoint is undocumented and a finer taxonomy would be invention.
/// </para>
/// <para>
/// Nothing here touches the network. The live endpoint is rate-limited and attached to the
/// user's real quota, so a test that called it would spend the thing the app exists to watch.
/// </para>
/// </summary>
public class UsageClientTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 31, 10, 9, 23, TimeSpan.FromHours(-7));

    private static readonly Credential Token =
        new("sk-ant-oat01-example-token", Now.AddHours(8));

    private static async Task<FetchOutcome> FetchWith(StubHttpMessageHandler handler)
    {
        using var http = new HttpClient(handler);
        var client = new UsageClient(http, new TestClock(Now));

        return await client.FetchAsync(Token);
    }

    private static Task<FetchOutcome> FetchStatus(HttpStatusCode status, string body = "") =>
        FetchWith(StubHttpMessageHandler.Returning(status, body));

    // ---- The reading ----

    [Fact]
    public async Task ATwoHundredBecomesASuccessfulReading()
    {
        var outcome = await FetchStatus(HttpStatusCode.OK, Fixtures.Read(Fixtures.Baseline));

        var success = Assert.IsType<FetchOutcome.Success>(outcome);
        Assert.Equal(2, success.Snapshot.Windows.Count);
    }

    [Fact]
    public async Task TheReadingIsStampedWithTheClocksInstant()
    {
        // Principle 6: every figure carries its age, and the age is measured from here.
        var outcome = await FetchStatus(HttpStatusCode.OK, Fixtures.Read(Fixtures.Baseline));

        var success = Assert.IsType<FetchOutcome.Success>(outcome);
        Assert.Equal(Now, success.Snapshot.ObservedAt);
    }

    [Fact]
    public async Task ATwoHundredCarryingNothingRecognizableIsUnreadable()
    {
        var outcome = await FetchStatus(HttpStatusCode.OK, "{}");

        Assert.IsType<FetchOutcome.Unreadable>(outcome);
    }

    [Fact]
    public async Task ASuccessWithNoBodyAtAllIsUnreadableRatherThanAnEmptyReading()
    {
        var outcome = await FetchStatus(HttpStatusCode.NoContent);

        Assert.IsType<FetchOutcome.Unreadable>(outcome);
    }

    // ---- Authentication ----

    [Fact]
    public async Task AFourOhOneNamingAnAuthenticationErrorIsAnInvalidatedToken()
    {
        var outcome = await FetchStatus(
            HttpStatusCode.Unauthorized,
            Fixtures.Read(Fixtures.AuthFailure));

        var failure = Assert.IsType<FetchOutcome.AuthFailed>(outcome);
        Assert.Equal(AuthFailureKind.Invalidated, failure.Kind);
    }

    [Fact]
    public async Task AFourOhOneWithAnUnreadableBodyStaysUnspecified()
    {
        var outcome = await FetchStatus(HttpStatusCode.Unauthorized, "<html>nope</html>");

        var failure = Assert.IsType<FetchOutcome.AuthFailed>(outcome);
        Assert.Equal(AuthFailureKind.Unspecified, failure.Kind);
    }

    [Fact]
    public async Task AFourOhThreeIsAlsoAnAuthenticationFailure()
    {
        var outcome = await FetchStatus(HttpStatusCode.Forbidden, "{}");

        Assert.IsType<FetchOutcome.AuthFailed>(outcome);
    }

    [Fact]
    public async Task AnAuthenticationFailureCarryingAWindowShapedBodyIsStillAFailure()
    {
        // A rejected request's body can contain anything. It is not a reading, and the status
        // code decides, not the shape of what came back.
        var outcome = await FetchStatus(
            HttpStatusCode.Unauthorized,
            Fixtures.Read(Fixtures.Baseline));

        Assert.IsType<FetchOutcome.AuthFailed>(outcome);
    }

    // ---- Transient ----

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task RateLimitsAndServerErrorsAreTransient(HttpStatusCode status)
    {
        Assert.IsType<FetchOutcome.Transient>(await FetchStatus(status));
    }

    [Fact]
    public async Task ARetryAfterInSecondsIsCarriedThrough()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(90));
            return response;
        });

        var outcome = await FetchWith(handler);

        var transient = Assert.IsType<FetchOutcome.Transient>(outcome);
        Assert.Equal(TimeSpan.FromSeconds(90), transient.RetryAfter);
    }

    [Fact]
    public async Task ARetryAfterGivenAsADateBecomesADuration()
    {
        // The header permits either form. A date is only meaningful relative to now, which is
        // why the client is given a clock.
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(Now.AddMinutes(4));
            return response;
        });

        var outcome = await FetchWith(handler);

        var transient = Assert.IsType<FetchOutcome.Transient>(outcome);
        Assert.Equal(TimeSpan.FromMinutes(4), transient.RetryAfter);
    }

    [Fact]
    public async Task ARetryAfterDateAlreadyInThePastIsNotANegativeWait()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(Now.AddMinutes(-4));
            return response;
        });

        var outcome = await FetchWith(handler);

        var transient = Assert.IsType<FetchOutcome.Transient>(outcome);
        Assert.Equal(TimeSpan.Zero, transient.RetryAfter);
    }

    [Fact]
    public async Task ARateLimitWithNoRetryAfterCarriesNone()
    {
        // Null means the server did not say, which is different from "come back immediately".
        // What to do about it is the poll policy's decision, not the client's.
        var outcome = await FetchStatus(HttpStatusCode.TooManyRequests);

        var transient = Assert.IsType<FetchOutcome.Transient>(outcome);
        Assert.Null(transient.RetryAfter);
    }

    [Fact]
    public async Task ANetworkThatIsNotThereIsTransient()
    {
        var handler = StubHttpMessageHandler.Throwing(new HttpRequestException("no route to host"));

        Assert.IsType<FetchOutcome.Transient>(await FetchWith(handler));
    }

    [Fact]
    public async Task ARequestThatTimesOutIsTransient()
    {
        // HttpClient reports its own timeout as a cancellation, which must not be confused with
        // the caller cancelling.
        var handler = StubHttpMessageHandler.Throwing(
            new TaskCanceledException("timed out", new TimeoutException()));

        Assert.IsType<FetchOutcome.Transient>(await FetchWith(handler));
    }

    [Fact]
    public async Task ACallerCancellingIsNotSwallowedAsATransientFailure()
    {
        // Shutting the app down is not a failed poll, and reporting it as one would leave a
        // spurious backoff behind.
        using var http = new HttpClient(StubHttpMessageHandler.Returning(HttpStatusCode.OK, "{}"));
        var client = new UsageClient(http, new TestClock(Now));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.FetchAsync(Token, cancelled.Token));
    }

    // ---- Unsupported ----

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.UnsupportedMediaType)]
    public async Task AnyOtherStatusMeansTheEndpointIsNotUsableHere(HttpStatusCode status)
    {
        var outcome = await FetchStatus(status);

        var unsupported = Assert.IsType<FetchOutcome.Unsupported>(outcome);
        Assert.Equal((int)status, unsupported.StatusCode);
    }

    // ---- What gets sent ----

    [Fact]
    public async Task TheRequestGoesToTheUsageEndpoint()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, "{}");

        await FetchWith(handler);

        Assert.Equal(
            "https://api.anthropic.com/api/oauth/usage",
            Assert.Single(handler.Requests).RequestUri?.ToString());
    }

    [Fact]
    public async Task TheRequestIsAGet()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, "{}");

        await FetchWith(handler);

        Assert.Equal(HttpMethod.Get, Assert.Single(handler.Requests).Method);
    }

    [Fact]
    public async Task TheTokenIsSentAsABearerCredential()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, "{}");

        await FetchWith(handler);

        var authorization = Assert.Single(handler.Requests).Headers.Authorization;
        Assert.Equal("Bearer", authorization?.Scheme);
        Assert.Equal(Token.AccessToken, authorization?.Parameter);
    }

    [Fact]
    public async Task TheOauthBetaHeaderIsSent()
    {
        // The endpoint is gated behind it. Without it there is no response to parse.
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, "{}");

        await FetchWith(handler);

        Assert.Equal(
            ["oauth-2025-04-20"],
            Assert.Single(handler.Requests).Headers.GetValues("anthropic-beta"));
    }

    [Fact]
    public async Task TheUserAgentIdentifiesAsClaudeCode()
    {
        // Pinned deliberately: prior art reports that a generic agent lands in a stricter rate
        // limit bucket on this endpoint.
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, "{}");

        await FetchWith(handler);

        var userAgent = Assert.Single(handler.Requests).Headers.UserAgent.ToString();
        Assert.StartsWith("claude-code/", userAgent);
    }

    [Fact]
    public async Task JsonIsAccepted()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, "{}");

        await FetchWith(handler);

        Assert.Contains(
            Assert.Single(handler.Requests).Headers.Accept,
            header => header.MediaType == "application/json");
    }

    [Fact]
    public async Task OneFetchMakesExactlyOneRequest()
    {
        // AC-26 in its simplest form: nothing here retries on its own, and nothing reaches for
        // a second endpoint when the first says no. Backing off is the monitor's job.
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.TooManyRequests);

        await FetchWith(handler);

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task TheTokenIsNeverPutInTheUrl()
    {
        // A query string ends up in proxy logs and crash reports. The header does not.
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, "{}");

        await FetchWith(handler);

        Assert.DoesNotContain(
            Token.AccessToken,
            Assert.Single(handler.Requests).RequestUri?.ToString());
    }
}
