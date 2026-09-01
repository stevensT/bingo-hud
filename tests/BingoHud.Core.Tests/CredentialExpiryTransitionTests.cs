using BingoHud.Core.Credentials;

namespace BingoHud.Core.Tests;

/// <summary>
/// A token expiring is an ordinary transition, not a fault.
///
/// <para>
/// The observed token life is roughly eight hours, so an always-on app crosses it daily. Since
/// Bingo does not refresh — see the 3.1 decision — the crossing is guaranteed rather than
/// exceptional, and everything about it has to be ordinary: the expired credential is still
/// read and still returned, and the app surfaces a sign-in state while keeping the last reading
/// on screen, frozen and marked with its age.
/// </para>
/// <para>
/// The one thing not permitted is treating expiry as a read failure. Returning null for an
/// expired token would make the file look missing, and the user would be told they are signed
/// out when the token is simply old.
/// </para>
/// </summary>
public class CredentialExpiryTransitionTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private static readonly DateTimeOffset Now =
        new(2026, 8, 31, 9, 34, 31, TimeSpan.FromHours(-7));

    private Task<Credential?> ReadWithExpiry(DateTimeOffset expiry) =>
        new FileCredentialProvider(_directory.WriteFile(
            ".credentials.json",
            $$"""
            {
              "claudeAiOauth": {
                "accessToken": "sk-ant-oat01-example-token",
                "expiresAt": {{expiry.ToUnixTimeMilliseconds()}}
              }
            }
            """)).GetAsync();

    [Fact]
    public async Task AnExpiredTokenIsStillRead()
    {
        // The read succeeded. What the token is worth is a separate question, asked later by
        // whoever is about to use it.
        var credential = await ReadWithExpiry(Now.AddHours(-1));

        Assert.NotNull(credential);
        Assert.Equal("sk-ant-oat01-example-token", credential.AccessToken);
    }

    [Fact]
    public void ATokenPastItsExpiryHasExpired()
    {
        var credential = new Credential("sk-ant-oat01-example-token", Now.AddSeconds(-1));

        Assert.True(credential.HasExpiredAt(Now));
    }

    [Fact]
    public void ATokenBeforeItsExpiryHasNot()
    {
        var credential = new Credential("sk-ant-oat01-example-token", Now.AddHours(8));

        Assert.False(credential.HasExpiredAt(Now));
    }

    [Fact]
    public void TheExpiryInstantItselfCountsAsExpired()
    {
        var credential = new Credential("sk-ant-oat01-example-token", Now);

        Assert.True(credential.HasExpiredAt(Now));
    }

    [Fact]
    public void ATokenWithNoRecordedExpiryIsNotTreatedAsExpired()
    {
        // The file did not say. Declaring it expired would invent a sign-in prompt out of a
        // missing field; letting the request go and reading the server's answer is the only
        // claim that rests on evidence.
        var credential = new Credential("sk-ant-oat01-example-token", ExpiresAt: null);

        Assert.False(credential.HasExpiredAt(Now));
    }

    [Fact]
    public void ExpiryIsJudgedAgainstTheInstantGivenRatherThanTheSystemClock()
    {
        // The crossing happens once a day in real use and never in a test run, unless the
        // instant is injected. Nothing in Core reads the system clock.
        var credential = new Credential("sk-ant-oat01-example-token", Now);

        Assert.False(credential.HasExpiredAt(Now.AddSeconds(-1)));
        Assert.True(credential.HasExpiredAt(Now.AddSeconds(1)));
    }
}
