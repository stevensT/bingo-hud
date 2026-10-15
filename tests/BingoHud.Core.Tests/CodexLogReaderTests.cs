using System.Text.Json;
using System.Text.Json.Nodes;
using BingoHud.Core.Codex;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// Finding the newest Codex rate-limit record with figures across the session logs
/// (codex-quota AC-1, AC-7).
///
/// <para>
/// Every test builds a Codex folder from fixtures in a throwaway directory. Record timestamps
/// decide which reading is newest; file times deliberately disagree to prove they are ignored.
/// </para>
/// </summary>
public class CodexLogReaderTests
{
    private static readonly DateTimeOffset Earlier = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

    private static string Fixture(string name) => File.ReadAllText(Fixtures.CodexPath(name));

    /// <summary>
    /// Writes a log under the Codex folder, at a path such as <c>sessions/2026/10/14/a.jsonl</c>,
    /// and sets when it was last written.
    /// </summary>
    private static string WriteLog(TempDirectory codex, string relativePath, string content, DateTimeOffset writtenAt)
    {
        var file = Path.Combine(codex.Path, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
        File.SetLastWriteTimeUtc(file, writtenAt.UtcDateTime);
        return file;
    }

    private static string AtTime(string record, DateTimeOffset observedAt)
    {
        var node = JsonNode.Parse(record)!;
        node["timestamp"] = observedAt.ToString("O");
        return node.ToJsonString() + "\n";
    }

    private static CodexScan Scan(TempDirectory codex) => new CodexLogReader(codex.Path).Scan();

    private static QuotaSnapshot ScanExpectingReading(TempDirectory codex) =>
        Assert.IsType<CodexScan.Reading>(Scan(codex)).Figures.Snapshot;

    private static double Used(QuotaSnapshot snapshot, WindowKind kind) =>
        Assert.Single(snapshot.Windows, w => w.Kind == kind).UsedPercent;

    [Fact]
    public void TheLastRecordWithFiguresInALogIsTheReading()
    {
        using var codex = new TempDirectory();
        WriteLog(codex, "sessions/2026/10/13/session.jsonl", Fixture(Fixtures.CodexSession), Later);

        var snapshot = ScanExpectingReading(codex);

        // The session fixture's last record: five-hour 24%, weekly 12%.
        Assert.Equal(24.0, Used(snapshot, WindowKind.Session));
        Assert.Equal(12.0, Used(snapshot, WindowKind.WeeklyAll));
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 6, 32, 6, 293, TimeSpan.Zero), snapshot.ObservedAt);
    }

    [Fact]
    public void TheNewestRecordWinsEvenWhenItsFileTimeIsOlder()
    {
        // Weekly 30% in the tui record, 12% at the end of the session: the weekly figure says which
        // file the reading came from.
        using var codex = new TempDirectory();
        WriteLog(codex, "sessions/2026/10/14/tui.jsonl", Fixture(Fixtures.CodexTuiRecord), Earlier);
        WriteLog(codex, "sessions/2026/10/13/session.jsonl", Fixture(Fixtures.CodexSession), Later);

        Assert.Equal(30.0, Used(ScanExpectingReading(codex), WindowKind.WeeklyAll));
    }

    [Fact]
    public void ArchivedSessionsAreReadToo()
    {
        using var codex = new TempDirectory();
        WriteLog(codex, "sessions/2026/10/13/session.jsonl", Fixture(Fixtures.CodexSession), Earlier);
        WriteLog(codex, "archived_sessions/tui.jsonl", Fixture(Fixtures.CodexTuiRecord), Later);

        Assert.Equal(30.0, Used(ScanExpectingReading(codex), WindowKind.WeeklyAll));
    }

    [Fact]
    public void ALogHoldingOnlyNullRecordsIsPassedOverForAnOlderOne()
    {
        // What the Codex app writes as it starts: a burst of logs with one null record each.
        using var codex = new TempDirectory();
        WriteLog(codex, "sessions/2026/10/14/tui.jsonl", Fixture(Fixtures.CodexTuiRecord), Earlier);
        WriteLog(codex, "sessions/2026/10/14/startup.jsonl", Fixture(Fixtures.CodexNullRecord), Later);

        Assert.Equal(30.0, Used(ScanExpectingReading(codex), WindowKind.WeeklyAll));
    }

    [Fact]
    public void ANullRecordAfterFiguresDoesNotHideThem()
    {
        // The reading is the latest record with figures. A null record says Codex has nothing to
        // report at that moment; it does not mean the figures before it stopped being true.
        using var codex = new TempDirectory();
        WriteLog(codex, "sessions/2026/10/14/mixed.jsonl",
            Fixture(Fixtures.CodexTuiRecord) + Fixture(Fixtures.CodexNullRecord), Later);

        Assert.Equal(30.0, Used(ScanExpectingReading(codex), WindowKind.WeeklyAll));
    }

    [Fact]
    public void ALastLineStillBeingWrittenIsPassedOver()
    {
        // Codex appends while Bingo reads. Every finished line ends in a newline, so a final line
        // without one is half-written: the record before it is the reading, and nothing is wrong.
        using var codex = new TempDirectory();
        WriteLog(codex, "sessions/2026/10/14/live.jsonl", Fixture(Fixtures.CodexTruncatedTail), Later);

        Assert.Equal(24.0, Used(ScanExpectingReading(codex), WindowKind.Session));
    }

    [Fact]
    public void ADamagedLineThatWasFinishedIsPassedOverForTheRecordBeforeIt()
    {
        // A crash mid-write, then a resumed session appending its next line, leaves the cut-off
        // record finished by a newline. It is damage, not a format change, so the record before
        // it is the reading.
        using var codex = new TempDirectory();
        WriteLog(codex, "sessions/2026/10/14/resumed.jsonl", Fixture(Fixtures.CodexTruncatedTail) + "\n", Later);

        Assert.Equal(24.0, Used(ScanExpectingReading(codex), WindowKind.Session));
    }

    [Fact]
    public void ADamagedLineInAnOldLogDoesNotHideANewerReading()
    {
        // Found at the feature review: one such line in an archived log stopped every scan with
        // "format not recognised" for good.
        using var codex = new TempDirectory();
        WriteLog(codex, "archived_sessions/old.jsonl", Fixture(Fixtures.CodexTruncatedTail) + "\n", Earlier);
        WriteLog(codex, "sessions/tui.jsonl", AtTime(Fixture(Fixtures.CodexTuiRecord), Later.AddDays(1)), Later);

        Assert.Equal(30.0, Used(ScanExpectingReading(codex), WindowKind.WeeklyAll));
    }

    [Fact]
    public void ALogMovedAwayBetweenListingAndOpeningIsPassedOver()
    {
        // Codex moves a finished session from sessions to archived_sessions. If that happens mid
        // scan, the file listed is gone when it is opened, which is not the logs being unreadable.
        using var codex = new TempDirectory();
        WriteLog(codex, "sessions/tui.jsonl", Fixture(Fixtures.CodexTuiRecord), Later);
        var moving = WriteLog(codex, "sessions/moving.jsonl", Fixture(Fixtures.CodexSession), Earlier);

        var reader = new CodexLogReader(codex.Path, readLog: path =>
        {
            if (path == moving)
            {
                File.Delete(moving);
            }

            return CodexLogReader.ReadShared(path);
        });

        var reading = Assert.IsType<CodexScan.Reading>(reader.Scan());
        Assert.Equal(30.0, Used(reading.Figures.Snapshot, WindowKind.WeeklyAll));
    }

    [Fact]
    public void CarriageReturnLineEndingsReadTheSame()
    {
        // Git may check the fixtures out this way, and a log copied between machines could be too.
        using var codex = new TempDirectory();
        WriteLog(codex, "sessions/2026/10/13/session.jsonl",
            Fixture(Fixtures.CodexSession).ReplaceLineEndings("\r\n"), Later);

        Assert.Equal(24.0, Used(ScanExpectingReading(codex), WindowKind.Session));
    }

    [Fact]
    public void ANewestRecordInAnUnknownShapeIsAProblemNotAnOlderReading()
    {
        // Falling back to an older record would show a figure as current when the newest record
        // says Codex has changed what it writes. That change is the news.
        using var codex = new TempDirectory();
        WriteLog(codex, "sessions/2026/10/14/tui.jsonl", Fixture(Fixtures.CodexTuiRecord), Earlier);
        WriteLog(codex, "sessions/2026/10/14/changed.jsonl",
            AtTime(Fixture(Fixtures.CodexWrongType), new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero)), Later);

        var problem = Assert.IsType<CodexScan.Problem>(Scan(codex));
        var cause = Assert.IsType<CodexProblem.UnknownShape>(problem.Cause);
        Assert.False(string.IsNullOrWhiteSpace(cause.Reason));
    }

    [Fact]
    public void ALogThatCannotBeOpenedIsAProblemNotAnException()
    {
        using var codex = new TempDirectory();
        var file = WriteLog(codex, "sessions/2026/10/14/locked.jsonl", Fixture(Fixtures.CodexTuiRecord), Later);

        // Held open with no sharing, as another program refusing readers would.
        using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var problem = Assert.IsType<CodexScan.Problem>(Scan(codex));
            var cause = Assert.IsType<CodexProblem.LogsUnreadable>(problem.Cause);
            Assert.False(string.IsNullOrWhiteSpace(cause.Reason));
        }
    }

    [Fact]
    public void NoCodexFolderIsNotFound()
    {
        using var directory = new TempDirectory();

        Assert.IsType<CodexScan.NotFound>(new CodexLogReader(directory.PathTo(".codex")).Scan());
    }

    [Fact]
    public void ACodexFolderWithNoLogsIsNoReading()
    {
        // Installed and never used: a different thing to say from "not installed" (AC-7).
        using var codex = new TempDirectory();

        Assert.IsType<CodexScan.NoReading>(Scan(codex));
    }

    [Fact]
    public void ACodexFolderWithOnlyNullRecordsIsNoReading()
    {
        using var codex = new TempDirectory();
        WriteLog(codex, "sessions/2026/10/14/startup.jsonl", Fixture(Fixtures.CodexNullRecord), Later);

        Assert.IsType<CodexScan.NoReading>(Scan(codex));
    }

    [Fact]
    public void OtherFilesInTheCodexFolderAreNotRead()
    {
        // The folder also holds Codex's own settings and sign-in. Only the two log folders are
        // looked at, so a record-shaped line anywhere else is not a reading.
        using var codex = new TempDirectory();
        WriteLog(codex, "auth-lookalike.jsonl", Fixture(Fixtures.CodexTuiRecord), Later);

        Assert.IsType<CodexScan.NoReading>(Scan(codex));
    }

    [Fact]
    public void NothingFromAConversationLineSurvivesTheScan()
    {
        // The session fixture mixes invented conversation lines in with its records, three of
        // them mentioning rate limits. Whatever the scan returns, none of their text is in it.
        using var codex = new TempDirectory();
        WriteLog(codex, "sessions/2026/10/13/session.jsonl", Fixture(Fixtures.CodexSession), Later);

        var scan = Scan(codex);
        var everything = JsonSerializer.Serialize<object>(scan);

        Assert.IsType<CodexScan.Reading>(scan);
        // Proof the serialized text reaches the kept record, so the check below is not vacuous.
        Assert.Contains("window_minutes", everything, StringComparison.Ordinal);
        Assert.DoesNotContain(Fixtures.DecoyMarker, everything, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TheRootIsDotCodexInTheProfileWhenCodexHomeIsUnset(string? codexHome)
    {
        Assert.Equal(
            Path.Combine(@"C:\Users\someone", ".codex"),
            CodexLogReader.RootFor(@"C:\Users\someone", codexHome));
    }

    [Fact]
    public void TheRootIsCodexHomeWhenSet()
    {
        // Codex reads CODEX_HOME the same way, so Bingo looks where Codex writes.
        Assert.Equal(@"D:\elsewhere\codex", CodexLogReader.RootFor(@"C:\Users\someone", @"D:\elsewhere\codex"));
    }

    [Fact]
    public void AnOlderUnknownRecordDoesNotHideANewerReading()
    {
        using var codex = new TempDirectory();
        WriteLog(codex, "sessions/tui.jsonl", Fixture(Fixtures.CodexTuiRecord), Earlier);
        WriteLog(codex, "sessions/older-broken.jsonl",
            AtTime(Fixture(Fixtures.CodexWrongType), Earlier), Later);

        Assert.Equal(30.0, Used(ScanExpectingReading(codex), WindowKind.WeeklyAll));
    }

    [Fact]
    public void AnUnreadableRecordWithoutATimestampCannotBeAssumedOlderThanAReading()
    {
        using var codex = new TempDirectory();
        WriteLog(codex, "sessions/tui.jsonl", Fixture(Fixtures.CodexTuiRecord), Later);
        var broken = JsonNode.Parse(Fixture(Fixtures.CodexWrongType))!;
        broken["timestamp"] = "not a timestamp";
        WriteLog(codex, "sessions/untimed.jsonl", broken.ToJsonString() + "\n", Earlier);

        var problem = Assert.IsType<CodexScan.Problem>(Scan(codex));
        Assert.IsType<CodexProblem.UnknownShape>(problem.Cause);
    }
}
