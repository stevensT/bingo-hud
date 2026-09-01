using BingoHud.Core.Polling;

namespace BingoHud.Core.Tests;

/// <summary>
/// The only signal that says whether there is anything to watch.
///
/// <para>
/// Utilization moves when Claude Code is working and at no other time, so "when did Claude Code
/// last write a transcript" is the closest available proxy for "is a number about to change".
/// Without it the <c>Claude Code is working</c> row of the cadence table can never fire, and
/// Bingo falls through to its slowest cadence at exactly the moment quota is moving fastest.
/// </para>
/// <para>
/// It reads modification times and nothing else. Transcripts are the user's conversations, and
/// this feature needs one timestamp from them — opening them would be a serious overreach for a
/// number that <c>stat</c> already answers.
/// </para>
/// </summary>
public class TranscriptActivityTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 1, 12, 0, 0, TimeSpan.FromHours(-7));

    private static TranscriptActivity Activity(string path, DateTimeOffset? now = null) =>
        new(path, new TestClock(now ?? Now));

    /// <summary>Writes a transcript in a project folder and backdates it.</summary>
    private static void WriteTranscript(
        TempDirectory directory,
        string project,
        string name,
        DateTimeOffset writtenAt)
    {
        var folder = Path.Combine(directory.Path, project);
        Directory.CreateDirectory(folder);

        var file = Path.Combine(folder, name);
        File.WriteAllText(file, "{}");
        File.SetLastWriteTimeUtc(file, writtenAt.UtcDateTime);
    }

    [Fact]
    public void ADirectoryThatDoesNotExistIsUnknownRatherThanIdle()
    {
        // Null means "no evidence", and the cadence table already treats absence of evidence as
        // a reason to poll slowly rather than as proof that nothing is happening.
        using var directory = new TempDirectory();

        Assert.Null(Activity(directory.PathTo("no-such-directory")).SinceLastWrite());
    }

    [Fact]
    public void NoTranscriptsAtAllIsAlsoUnknown()
    {
        using var directory = new TempDirectory();

        Assert.Null(Activity(directory.Path).SinceLastWrite());
    }

    [Fact]
    public void TheAgeOfTheNewestTranscriptIsWhatCounts()
    {
        using var directory = new TempDirectory();
        WriteTranscript(directory, "project-a", "old.jsonl", Now.AddHours(-6));
        WriteTranscript(directory, "project-b", "recent.jsonl", Now.AddMinutes(-3));

        Assert.Equal(TimeSpan.FromMinutes(3), Activity(directory.Path).SinceLastWrite());
    }

    [Fact]
    public void TranscriptsAreFoundAcrossEveryProjectFolder()
    {
        // One folder per project, and the user may be working in any of them. Bingo's quota is
        // account-wide, so activity anywhere counts.
        using var directory = new TempDirectory();
        WriteTranscript(directory, "project-a", "a.jsonl", Now.AddHours(-2));
        WriteTranscript(directory, "project-b", "b.jsonl", Now.AddHours(-1));
        WriteTranscript(directory, "project-c", "c.jsonl", Now.AddMinutes(-30));

        Assert.Equal(TimeSpan.FromMinutes(30), Activity(directory.Path).SinceLastWrite());
    }

    [Fact]
    public void OnlyTranscriptsCount()
    {
        // The projects directory also holds a memory folder of markdown files. Those are written
        // by other things entirely, and counting them would report Claude Code as working when
        // it is not — which would poll a rate-limited endpoint hard for no reason.
        using var directory = new TempDirectory();
        WriteTranscript(directory, "project-a", "session.jsonl", Now.AddHours(-4));
        WriteTranscript(directory, "project-a/memory", "note.md", Now.AddSeconds(-5));

        Assert.Equal(TimeSpan.FromHours(4), Activity(directory.Path).SinceLastWrite());
    }

    [Fact]
    public void ATranscriptWrittenInTheFutureReadsAsThisInstantRatherThanAsNegativeTime()
    {
        // Clock skew and a restored backup both produce this. A negative span would satisfy every
        // comparison in the cadence table and pin Bingo to its fastest cadence indefinitely.
        using var directory = new TempDirectory();
        WriteTranscript(directory, "project-a", "ahead.jsonl", Now.AddMinutes(20));

        Assert.Equal(TimeSpan.Zero, Activity(directory.Path).SinceLastWrite());
    }

    [Fact]
    public void TheAnswerMovesWithTheClockRatherThanBeingCachedFromTheFirstCall()
    {
        using var directory = new TempDirectory();
        WriteTranscript(directory, "project-a", "a.jsonl", Now.AddMinutes(-1));

        var clock = new TestClock(Now);
        var activity = new TranscriptActivity(directory.Path, clock);

        Assert.Equal(TimeSpan.FromMinutes(1), activity.SinceLastWrite());

        clock.Advance(TimeSpan.FromMinutes(9));

        Assert.Equal(TimeSpan.FromMinutes(10), activity.SinceLastWrite());
    }

    [Fact]
    public void TheDefaultPathIsWhereClaudeCodeKeepsItsTranscripts()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude",
            "projects");

        Assert.Equal(expected, TranscriptActivity.DefaultPath);
    }
}
