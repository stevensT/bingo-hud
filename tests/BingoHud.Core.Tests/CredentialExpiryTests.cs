using BingoHud.Core.Credentials;

namespace BingoHud.Core.Tests;

/// <summary>
/// Reading <c>expiresAt</c>, which arrives as a bare number with no unit attached.
///
/// <para>
/// The live file stores milliseconds. Prior art has seen seconds. Nothing in the file says
/// which, so the unit is inferred from magnitude: ten billion seconds lands in the year 2286
/// and ten billion milliseconds lands in 1970, so any value at or above that boundary is
/// milliseconds and anything below it is seconds. The boundary is not arbitrary — it is the only
/// place the two interpretations cannot both be plausible.
/// </para>
/// <para>
/// Guessing wrong is quiet rather than loud: reading milliseconds as seconds puts the expiry
/// tens of thousands of years out, so the token never appears to expire and the sign-in state
/// never arrives.
/// </para>
/// </summary>
public class CredentialExpiryTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    /// <summary>
    /// 2026-08-31T05:54:31Z, expressed in seconds. The same instant appears below in
    /// milliseconds.
    /// </summary>
    private const long ExpirySeconds = 1_788_203_671;

    private static readonly DateTimeOffset Expiry =
        DateTimeOffset.FromUnixTimeSeconds(ExpirySeconds);

    private Task<Credential?> ReadAsync(string content) =>
        new FileCredentialProvider(_directory.WriteFile(".credentials.json", content)).GetAsync();

    private Task<Credential?> ReadWithExpiry(string rawExpiresAt) =>
        ReadAsync(
            $$"""
            {
              "claudeAiOauth": {
                "accessToken": "sk-ant-oat01-example-token",
                "expiresAt": {{rawExpiresAt}}
              }
            }
            """);

    [Fact]
    public async Task MillisecondsAreReadAsMilliseconds()
    {
        // The case the live file actually stores.
        var credential = await ReadWithExpiry((ExpirySeconds * 1000).ToString());

        Assert.Equal(Expiry, credential?.ExpiresAt);
    }

    [Fact]
    public async Task SecondsAreReadAsSeconds()
    {
        var credential = await ReadWithExpiry(ExpirySeconds.ToString());

        Assert.Equal(Expiry, credential?.ExpiresAt);
    }

    [Fact]
    public async Task TheTwoUnitsAgreeOnTheSameInstant()
    {
        // The whole point of the boundary: whichever unit the file happens to use, the app
        // reaches the same moment.
        var fromSeconds = await ReadWithExpiry(ExpirySeconds.ToString());
        var fromMilliseconds = await ReadWithExpiry((ExpirySeconds * 1000).ToString());

        Assert.Equal(fromSeconds?.ExpiresAt, fromMilliseconds?.ExpiresAt);
    }

    [Fact]
    public async Task TheValueJustBelowTheBoundaryIsSeconds()
    {
        var credential = await ReadWithExpiry("9999999999");

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(9_999_999_999), credential?.ExpiresAt);
    }

    [Fact]
    public async Task TheBoundaryValueItselfIsMilliseconds()
    {
        var credential = await ReadWithExpiry("10000000000");

        Assert.Equal(
            DateTimeOffset.FromUnixTimeMilliseconds(10_000_000_000),
            credential?.ExpiresAt);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"1788203671000\"")]
    [InlineData("\"soon\"")]
    [InlineData("true")]
    public async Task AnUnreadableExpiryLeavesTheTokenWithNoneRatherThanAGuess(string raw)
    {
        // No expiry is a state the app already handles. A wrong expiry is not.
        var credential = await ReadWithExpiry(raw);

        Assert.NotNull(credential);
        Assert.Null(credential.ExpiresAt);
    }

    [Fact]
    public async Task ATokenWithNoExpiryFieldCarriesNone()
    {
        var credential = await ReadAsync(
            """{ "claudeAiOauth": { "accessToken": "sk-ant-oat01-example-token" } }""");

        Assert.NotNull(credential);
        Assert.Null(credential.ExpiresAt);
    }

    [Fact]
    public async Task TheExpiryComesFromTheSameObjectAsTheToken()
    {
        // An expiry sitting beside a different token describes that other token. Borrowing it
        // would attach a confident-looking lifetime to a credential it says nothing about.
        var body =
            $$"""
            {
              "expiresAt": {{ExpirySeconds * 1000}},
              "claudeAiOauth": { "accessToken": "sk-ant-oat01-nested-token" }
            }
            """;

        var credential = await ReadAsync(body);

        Assert.Equal("sk-ant-oat01-nested-token", credential?.AccessToken);
        Assert.Null(credential?.ExpiresAt);
    }

    [Fact]
    public async Task ARootTokenTakesTheRootExpiry()
    {
        var body =
            $$"""
            {
              "accessToken": "sk-ant-oat01-root-token",
              "expiresAt": {{ExpirySeconds * 1000}}
            }
            """;

        var credential = await ReadAsync(body);

        Assert.Equal(Expiry, credential?.ExpiresAt);
    }

    [Theory]
    [InlineData("9223372036854775807")] // long.MaxValue
    [InlineData("-9223372036854775808")] // long.MinValue
    [InlineData("253402300800000")] // one millisecond past 9999-12-31T23:59:59.999Z
    [InlineData("-62135596801")] // one second before 0001-01-01T00:00:00Z
    public async Task AnExpiryNoDateCanHoldIsUnknownAndTheTokenIsKept(string rawExpiresAt)
    {
        // These once threw out of the conversion, past the parser and into the poll loop, which
        // ends the app on purpose when it faults. An expiry is metadata; the token beside it is
        // still the one to use.
        var credential = await ReadWithExpiry(rawExpiresAt);

        Assert.Equal("sk-ant-oat01-example-token", credential?.AccessToken);
        Assert.Null(credential?.ExpiresAt);
    }

    [Fact]
    public async Task TheLatestExpiryADateCanHoldIsStillRead()
    {
        var credential = await ReadWithExpiry("253402300799999");

        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(253402300799999), credential?.ExpiresAt);
    }

    [Fact]
    public async Task TheEarliestExpiryADateCanHoldIsStillRead()
    {
        var credential = await ReadWithExpiry("-62135596800");

        Assert.Equal(DateTimeOffset.MinValue, credential?.ExpiresAt);
    }
}
