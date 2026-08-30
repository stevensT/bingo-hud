using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// Reading the body of a response that failed authentication, against the real 401 captured on
/// 2026-08-30 with a deliberately invalid bearer token.
///
/// <para>
/// Only one thing is being decided here: whether the server actually said the token was
/// rejected, or whether it just said no. The first is worth telling the user, because it means
/// signing in again will fix it. The second is not worth dressing up as the first — a confident
/// diagnosis that turns out to be wrong sends someone re-authenticating a token that was never
/// the problem.
/// </para>
/// <para>
/// Which status codes count as authentication failures is the client's decision, not this
/// function's. It arrives with the client in a later phase.
/// </para>
/// </summary>
public class UsageNormalizerAuthFailureTests
{
    private static AuthFailureKind ClassifyKind(string body) =>
        Assert.IsType<FetchOutcome.AuthFailed>(UsageNormalizer.ClassifyAuthFailure(body)).Kind;

    [Fact]
    public void TheCapturedRejectionIsReadAsAnInvalidatedToken()
    {
        // The fixture body carries error.type == "authentication_error" and the message
        // "OAuth access token is invalid."
        Assert.Equal(
            AuthFailureKind.Invalidated,
            ClassifyKind(Fixtures.Read(Fixtures.AuthFailure)));
    }

    [Theory]
    [InlineData("""{ "type": "error", "error": { "type": "invalid_request_error" } }""")]
    [InlineData("""{ "type": "error", "error": { "message": "no." } }""")]
    [InlineData("""{ "type": "error", "error": null }""")]
    [InlineData("""{ "type": "error" }""")]
    [InlineData("{}")]
    public void AJsonBodyThatDoesNotNameAnAuthenticationErrorStaysUnspecified(string body)
    {
        Assert.Equal(AuthFailureKind.Unspecified, ClassifyKind(body));
    }

    [Theory]
    [InlineData("<html><body>401 Unauthorized</body></html>")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("null")]
    public void ABodyThatCannotBeReadStaysUnspecified(string body)
    {
        // An intermediary rejecting the request answers with something that is not the API's
        // error shape. That is still an authentication failure — the status code said so — but
        // nothing about the token has been established.
        Assert.Equal(AuthFailureKind.Unspecified, ClassifyKind(body));
    }

    [Fact]
    public void AnAuthFailureIsNeverReadAsASuccessHoweverTheBodyIsShaped()
    {
        // The body of a rejected request can contain anything at all, including something
        // window-shaped. It is not a reading, and it must not become one.
        var body =
            """
            {
              "limits": [
                { "kind": "session", "percent": 0, "severity": "normal", "resets_at": null }
              ]
            }
            """;

        Assert.IsType<FetchOutcome.AuthFailed>(UsageNormalizer.ClassifyAuthFailure(body));
    }
}
