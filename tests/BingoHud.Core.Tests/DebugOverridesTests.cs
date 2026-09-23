namespace BingoHud.Core.Tests;

/// <summary>
/// What a Debug build makes of its two override variables.
///
/// <para>
/// The endpoint override is the dangerous one: the request it redirects carries the token. Set on
/// its own, it would send the real token to whatever URL it names. So it is honoured only beside a
/// credential override, and only for a server on this machine.
/// </para>
/// </summary>
public class DebugOverridesTests
{
    [Fact]
    public void NeitherVariableSetMeansNoOverride()
    {
        var resolved = DebugOverrides.Resolve(null, null);

        Assert.Null(resolved.CredentialPath);
        Assert.Null(resolved.Endpoint);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankVariableCountsAsUnset(string blank)
    {
        // A blank credential path would otherwise read as a missing file, and the HUD would say
        // "Signed out" when the real cause is an empty variable.
        var resolved = DebugOverrides.Resolve(blank, blank);

        Assert.Null(resolved.CredentialPath);
        Assert.Null(resolved.Endpoint);
    }

    [Fact]
    public void BothSetToALocalServerAreBothHonoured()
    {
        var resolved = DebugOverrides.Resolve(@"C:\fake.json", "http://localhost:8765/usage");

        Assert.Equal(@"C:\fake.json", resolved.CredentialPath);
        Assert.Equal("http://localhost:8765/usage", resolved.Endpoint?.ToString());
    }

    [Fact]
    public void TheEndpointAloneIsRefusedBecauseItWouldCarryTheRealToken()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => DebugOverrides.Resolve(null, "http://localhost:8765/usage"));

        Assert.Contains(DebugOverrides.CredentialsVariable, error.Message);
    }

    [Theory]
    [InlineData("https://example.com/usage")]
    [InlineData("http://192.168.1.20:8765/usage")]
    public void AnEndpointOffThisMachineIsRefused(string url)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => DebugOverrides.Resolve(@"C:\fake.json", url));

        Assert.Contains(DebugOverrides.EndpointVariable, error.Message);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("localhost:8765/usage")]
    [InlineData("ftp://localhost/usage")]
    public void AnEndpointThatIsNotAnHttpUrlIsRefusedByName(string url)
    {
        // "localhost:8765/usage" parses as a URI whose scheme is "localhost", which HttpClient
        // cannot send to — it would throw past the client's catch and end the process.
        var error = Assert.Throws<InvalidOperationException>(
            () => DebugOverrides.Resolve(@"C:\fake.json", url));

        Assert.Contains(DebugOverrides.EndpointVariable, error.Message);
    }

    [Fact]
    public void TheCredentialPathAloneIsFine()
    {
        // Pointing at a fake credential file sends a fake token to the real endpoint, which
        // answers 401. Nothing real leaves the machine.
        var resolved = DebugOverrides.Resolve(@"C:\fake.json", null);

        Assert.Equal(@"C:\fake.json", resolved.CredentialPath);
        Assert.Null(resolved.Endpoint);
    }
}
