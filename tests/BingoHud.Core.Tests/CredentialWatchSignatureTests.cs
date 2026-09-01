using BingoHud.Core.Credentials;

namespace BingoHud.Core.Tests;

/// <summary>
/// The cheap check for "has the credential file changed since we last read it".
///
/// <para>
/// It exists so that Bingo does not re-read and re-parse a credential file on a timer when
/// nothing has happened to it. Claude Code rewrites the file when it refreshes the token, and
/// that write is the only event worth reacting to.
/// </para>
/// <para>
/// The signature is metadata only — path, size, last write time — so it never opens the file and
/// never touches the token. That also fixes its limit: a change that keeps the byte count and
/// the timestamp identical is invisible to it. Nothing writes a credential file that way, and
/// the cost of missing it is one stale read rather than a wrong number.
/// </para>
/// </summary>
public class CredentialWatchSignatureTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void TwoCapturesOfAnUnchangedFileAreEqual()
    {
        var path = _directory.WriteFile(".credentials.json", """{ "accessToken": "one" }""");

        Assert.Equal(
            CredentialWatchSignature.Capture(path),
            CredentialWatchSignature.Capture(path));
    }

    [Fact]
    public void RewritingTheFileWithDifferentContentChangesTheSignature()
    {
        var path = _directory.WriteFile(".credentials.json", """{ "accessToken": "one" }""");
        var before = CredentialWatchSignature.Capture(path);

        File.WriteAllText(path, """{ "accessToken": "a much longer replacement token" }""");

        Assert.NotEqual(before, CredentialWatchSignature.Capture(path));
    }

    [Fact]
    public void RewritingTheFileAtTheSameLengthStillChangesTheSignature()
    {
        // The realistic refresh: a new token of the same shape and therefore the same size.
        // Only the timestamp separates the two, which is why the timestamp is in the signature.
        var path = _directory.WriteFile(".credentials.json", """{ "accessToken": "one" }""");
        var before = CredentialWatchSignature.Capture(path);

        File.WriteAllText(path, """{ "accessToken": "two" }""");
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(1));

        Assert.NotEqual(before, CredentialWatchSignature.Capture(path));
    }

    [Fact]
    public void AMissingFileHasAStableSignature()
    {
        var path = _directory.PathTo("nothing-here.json");

        Assert.Equal(
            CredentialWatchSignature.Capture(path),
            CredentialWatchSignature.Capture(path));
    }

    [Fact]
    public void AFileAppearingChangesTheSignature()
    {
        // Signing in for the first time while Bingo is already running.
        var path = _directory.PathTo(".credentials.json");
        var before = CredentialWatchSignature.Capture(path);

        File.WriteAllText(path, """{ "accessToken": "one" }""");

        Assert.NotEqual(before, CredentialWatchSignature.Capture(path));
    }

    [Fact]
    public void AFileDisappearingChangesTheSignature()
    {
        var path = _directory.WriteFile(".credentials.json", """{ "accessToken": "one" }""");
        var before = CredentialWatchSignature.Capture(path);

        File.Delete(path);

        Assert.NotEqual(before, CredentialWatchSignature.Capture(path));
    }

    [Fact]
    public void TwoDifferentFilesWithIdenticalContentHaveDifferentSignatures()
    {
        // The path is part of the identity, so a signature captured for one file can never be
        // mistaken for another's.
        var first = _directory.WriteFile("first.json", """{ "accessToken": "one" }""");
        var second = _directory.WriteFile("second.json", """{ "accessToken": "one" }""");

        File.SetLastWriteTimeUtc(second, File.GetLastWriteTimeUtc(first));

        Assert.NotEqual(
            CredentialWatchSignature.Capture(first),
            CredentialWatchSignature.Capture(second));
    }

    [Fact]
    public void TheSignatureDoesNotCarryTheFileContents()
    {
        // It is metadata about a file holding a secret. Its string form ends up in diagnostics,
        // so it must stay metadata.
        var path = _directory.WriteFile(".credentials.json", """{ "accessToken": "sk-ant-secret" }""");

        Assert.DoesNotContain("sk-ant-secret", CredentialWatchSignature.Capture(path).ToString());
    }
}
