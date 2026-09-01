using BingoHud.Core.Credentials;

namespace BingoHud.Core.Tests;

/// <summary>
/// Telling "not signed in" apart from "not allowed to read the file" (AC-11).
///
/// <para>
/// The provider itself returns null for every failure, because the app's behaviour is the same
/// in all of them: no token, no numbers. This probe runs afterwards, only to explain what
/// happened, and its whole value is in the distinction. Someone told to sign in when the real
/// problem is a file permission will sign in, watch nothing change, and be left with no idea
/// what to try next.
/// </para>
/// <para>
/// The refused case is produced here by putting a directory where the file should be, which
/// raises the same <c>UnauthorizedAccessException</c> the operating system raises for a denying
/// ACL, and is a realistic corruption in its own right. A genuine ACL denial cannot be set up
/// portably inside a test, and the exception path is identical.
/// </para>
/// </summary>
public class CredentialAvailabilityProbeTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void AReadableFileIsReadable()
    {
        var path = _directory.WriteFile(".credentials.json", """{ "accessToken": "one" }""");

        Assert.Equal(CredentialAvailability.Readable, CredentialProbe.Probe(path));
    }

    [Fact]
    public void AMissingFileIsAbsent()
    {
        var path = _directory.PathTo("nothing-here.json");

        Assert.Equal(CredentialAvailability.Absent, CredentialProbe.Probe(path));
    }

    [Fact]
    public void AMissingDirectoryIsAbsentRatherThanRefused()
    {
        // A fresh machine where Claude Code has never run. There is no ~/.claude at all, and
        // the advice is to sign in, not to go looking at permissions.
        var path = Path.Combine(_directory.PathTo("no-such-folder"), ".credentials.json");

        Assert.Equal(CredentialAvailability.Absent, CredentialProbe.Probe(path));
    }

    [Fact]
    public void SomethingThatExistsButCannotBeOpenedIsRefused()
    {
        var path = _directory.PathTo(".credentials.json");
        Directory.CreateDirectory(path);

        Assert.Equal(CredentialAvailability.AccessDenied, CredentialProbe.Probe(path));
    }

    [Fact]
    public void AFileHeldExclusivelyByAnotherProcessIsBusy()
    {
        // Claude Code rewriting the file at the moment Bingo looks at it. Transient, and the
        // advice is to wait — which is neither of the other two answers.
        var path = _directory.WriteFile(".credentials.json", """{ "accessToken": "one" }""");

        using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.Equal(CredentialAvailability.Busy, CredentialProbe.Probe(path));
    }

    [Fact]
    public void AFileOpenedForWritingByAnotherProcessIsStillReadable()
    {
        // A writer that shares read access is the ordinary case, and it is not a problem.
        var path = _directory.WriteFile(".credentials.json", """{ "accessToken": "one" }""");

        using var shared = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.Read);

        Assert.Equal(CredentialAvailability.Readable, CredentialProbe.Probe(path));
    }

    [Fact]
    public void ProbingDoesNotChangeTheFile()
    {
        // It is a read-only diagnostic on a file holding a secret. It must not create it,
        // truncate it, or touch its timestamps.
        var path = _directory.WriteFile(".credentials.json", """{ "accessToken": "one" }""");
        var before = CredentialWatchSignature.Capture(path);

        CredentialProbe.Probe(path);

        Assert.Equal(before, CredentialWatchSignature.Capture(path));
    }

    [Fact]
    public void ProbingAMissingFileDoesNotCreateIt()
    {
        var path = _directory.PathTo("nothing-here.json");

        CredentialProbe.Probe(path);

        Assert.False(File.Exists(path));
    }
}
