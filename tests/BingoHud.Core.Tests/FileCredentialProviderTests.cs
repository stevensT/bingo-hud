using BingoHud.Core.Credentials;

namespace BingoHud.Core.Tests;

/// <summary>
/// Reading the OAuth token out of Claude Code's credential file.
///
/// <para>
/// The file itself is never read by these tests — the real one holds a live token, and the tool
/// classifier blocks reading it for exactly the right reason. Its shape is known from the
/// project's own capture script, and the fixtures here are written to match that shape.
/// </para>
/// <para>
/// Every failure mode returns null rather than throwing. A missing or malformed credential file
/// is an ordinary state — the user has not signed in yet, or an upgrade changed the format — and
/// the app's answer to all of them is the same: show a sign-in state and no numbers.
/// </para>
/// </summary>
public class FileCredentialProviderTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private Task<Credential?> ReadAsync(string content) =>
        new FileCredentialProvider(_directory.WriteFile(".credentials.json", content)).GetAsync();

    private const string NestedShape =
        """
        {
          "claudeAiOauth": {
            "accessToken": "sk-ant-oat01-example-token",
            "refreshToken": "sk-ant-ort01-example-token",
            "expiresAt": 1788203671000,
            "scopes": ["user:inference", "user:profile"],
            "subscriptionType": "max"
          }
        }
        """;

    [Fact]
    public async Task TheNestedOauthTokenIsRead()
    {
        var credential = await ReadAsync(NestedShape);

        Assert.Equal("sk-ant-oat01-example-token", credential?.AccessToken);
    }

    [Fact]
    public async Task ATokenAtTheRootIsRead()
    {
        // The older shape, and the one an upgrade could return to. Reading both costs a single
        // fallback and saves a sign-in prompt that would otherwise be unexplainable.
        var credential = await ReadAsync("""{ "accessToken": "sk-ant-oat01-root-token" }""");

        Assert.Equal("sk-ant-oat01-root-token", credential?.AccessToken);
    }

    [Fact]
    public async Task TheNestedTokenWinsWhenBothArePresent()
    {
        var body =
            """
            {
              "accessToken": "sk-ant-oat01-root-token",
              "claudeAiOauth": { "accessToken": "sk-ant-oat01-nested-token" }
            }
            """;

        var credential = await ReadAsync(body);

        Assert.Equal("sk-ant-oat01-nested-token", credential?.AccessToken);
    }

    [Fact]
    public async Task AMissingFileReadsAsNoCredential()
    {
        var provider = new FileCredentialProvider(_directory.PathTo("nothing-here.json"));

        Assert.Null(await provider.GetAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{ "claudeAiOauth": null }""")]
    [InlineData("""{ "claudeAiOauth": { "refreshToken": "only-a-refresh-token" } }""")]
    [InlineData("""{ "accessToken": null }""")]
    [InlineData("""{ "accessToken": 12345 }""")]
    public async Task AFileWithNoUsableTokenReadsAsNoCredential(string content)
    {
        Assert.Null(await ReadAsync(content));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyTokenIsNoCredential(string token)
    {
        // A blank token would otherwise be sent as a bearer and come back 401, which reports
        // the wrong problem: the user is signed out, not rejected.
        Assert.Null(await ReadAsync($$"""{ "claudeAiOauth": { "accessToken": "{{token}}" } }"""));
    }

    [Fact]
    public async Task TheTokenIsNotInTheCredentialsStringForm()
    {
        // A positional record prints every property by default, so without an override the
        // token would be one interpolated string away from a log file. This is the test that
        // keeps the override in place.
        var credential = await ReadAsync(NestedShape);

        Assert.DoesNotContain("sk-ant-oat01-example-token", credential!.ToString());
    }

    [Fact]
    public void TheDefaultPathPointsAtClaudeCodesCredentialFile()
    {
        Assert.EndsWith(Path.Combine(".claude", ".credentials.json"), FileCredentialProvider.DefaultPath);
    }
}
