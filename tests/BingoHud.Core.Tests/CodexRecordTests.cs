using BingoHud.Core.Codex;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// One line of a Codex session log, read as a rate-limit record (codex-quota AC-1, AC-2).
///
/// <para>
/// Run against lines cut from real logs wherever one exists; the hand-made fixtures each change a
/// captured record in one named way. The session fixture interleaves invented conversation lines
/// with its records, so the same file tests that a line which merely mentions rate limits is not
/// taken for one.
/// </para>
/// </summary>
public class CodexRecordTests
{
    // The session fixture in file order: decoys at 0, 2, 4 and 7, records at the rest.
    private static readonly string[] Session = Fixtures.CodexLines(Fixtures.CodexSession);
    private static string FirstRecord => Session[1];

    private static CodexRecordResult.Figures ParseExpectingFigures(string line) =>
        Assert.IsType<CodexRecordResult.Figures>(CodexRecord.Parse(line));

    private static CodexRecordResult.Unreadable ParseExpectingUnreadable(string line) =>
        Assert.IsType<CodexRecordResult.Unreadable>(CodexRecord.Parse(line));

    private static QuotaWindow Window(QuotaSnapshot snapshot, WindowKind kind) =>
        Assert.Single(snapshot.Windows, w => w.Kind == kind);

    [Fact]
    public void ARecordGivesTheFiveHourAndWeeklyWindowsAsSent()
    {
        var snapshot = ParseExpectingFigures(FirstRecord).Snapshot;

        var session = Window(snapshot, WindowKind.Session);
        Assert.Equal(44.0, session.UsedPercent);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1791949662), session.ResetsAt);

        var weekly = Window(snapshot, WindowKind.WeeklyAll);
        Assert.Equal(7.0, weekly.UsedPercent);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1792536462), weekly.ResetsAt);

        Assert.Equal(2, snapshot.Windows.Count);
    }

    [Fact]
    public void TheReadingIsTimedByTheRecordNotByWhenItWasRead()
    {
        // The figure is true as of the Codex turn that wrote it. Timing it by the read would make
        // an hours-old figure look current, which is the lie principle 6 exists to prevent.
        var snapshot = ParseExpectingFigures(FirstRecord).Snapshot;

        Assert.Equal(new DateTimeOffset(2026, 10, 14, 2, 29, 33, 252, TimeSpan.Zero), snapshot.ObservedAt);
    }

    [Fact]
    public void WindowsCarryNoServerSeverityAndNoScope()
    {
        // Codex sends no severity per window, so only local thresholds apply, exactly as for a
        // Claude window that reported none. Neither window is a per-model cap.
        foreach (var window in ParseExpectingFigures(FirstRecord).Snapshot.Windows)
        {
            Assert.Equal(ServerSeverity.Unknown, window.Severity);
            Assert.Null(window.Scope);
        }
    }

    [Fact]
    public void WindowsAreMappedByTheirLengthNotByTheirName()
    {
        // "primary" and "secondary" say nothing about which window is which; the minutes do. A
        // record listing them the other way round still lands each in its own slot.
        var swapped = """
            {"timestamp":"2026-10-14T02:29:33.252Z","type":"event_msg","payload":{"type":"token_count","rate_limits":{"primary":{"used_percent":7.0,"window_minutes":10080,"resets_at":1792536462},"secondary":{"used_percent":44.0,"window_minutes":300,"resets_at":1791949662}}}}
            """;

        var snapshot = ParseExpectingFigures(swapped).Snapshot;

        Assert.Equal(44.0, Window(snapshot, WindowKind.Session).UsedPercent);
        Assert.Equal(7.0, Window(snapshot, WindowKind.WeeklyAll).UsedPercent);
    }

    [Fact]
    public void TheRawBodyIsTheRateLimitsObjectAndNothingElseFromTheLine()
    {
        // In the fixtures rate_limits is the last field of the payload, so its text runs from
        // after its key to just before the two closing braces of payload and line.
        const string key = "\"rate_limits\":";
        var expected = FirstRecord[(FirstRecord.IndexOf(key, StringComparison.Ordinal) + key.Length)..^2];

        var raw = ParseExpectingFigures(FirstRecord).Snapshot.RawBody;

        Assert.Equal(expected, raw);
        Assert.DoesNotContain("timestamp", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("token_count", raw, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryRecordInTheSessionReads()
    {
        // The records either side of a five-hour reset included: 50% before, 0% after.
        int[] records = [1, 3, 5, 6, 8];
        double[] sessionUsed = [44.0, 50.0, 0.0, 0.0, 24.0];

        for (var i = 0; i < records.Length; i++)
        {
            var snapshot = ParseExpectingFigures(Session[records[i]]).Snapshot;
            Assert.Equal(sessionUsed[i], Window(snapshot, WindowKind.Session).UsedPercent);
        }
    }

    [Fact]
    public void TheResetTimeIsTakenAsWrittenEvenWhenItMovesBetweenRecords()
    {
        // Two records for one window, eight seconds apart in their reset. The parser reports
        // each as sent; matching them as one occurrence is the alert store's job, not this one's.
        var before = Window(ParseExpectingFigures(Session[5]).Snapshot, WindowKind.Session);
        var after = Window(ParseExpectingFigures(Session[6]).Snapshot, WindowKind.Session);

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1791969796), before.ResetsAt);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1791969804), after.ResetsAt);
    }

    [Fact]
    public void ARecordFromTheTerminalClientReadsTheSame()
    {
        var snapshot = ParseExpectingFigures(Fixtures.CodexLines(Fixtures.CodexTuiRecord)[0]).Snapshot;

        Assert.Equal(24.0, Window(snapshot, WindowKind.Session).UsedPercent);
        Assert.Equal(30.0, Window(snapshot, WindowKind.WeeklyAll).UsedPercent);
    }

    [Fact]
    public void ANullRecordIsNoReadingNeitherZeroNorUnreadable()
    {
        Assert.IsType<CodexRecordResult.NoReading>(
            CodexRecord.Parse(Fixtures.CodexLines(Fixtures.CodexNullRecord)[0]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(7)]
    public void AConversationLineIsNotARecordEvenWhenItMentionsRateLimits(int index)
    {
        // Three of the four decoys contain the text "rate_limits", one of them as a JSON object
        // with a 99% figure inside a tool's output. Text is only a prefilter; structure decides.
        var line = Session[index];
        Assert.Contains(Fixtures.DecoyMarker, line, StringComparison.Ordinal);

        Assert.IsType<CodexRecordResult.NotARecord>(CodexRecord.Parse(line));
    }

    [Fact]
    public void AWindowOfUnknownLengthIsLeftOutAndNamed()
    {
        var figures = ParseExpectingFigures(Fixtures.CodexLines(Fixtures.CodexUnknownWindow)[0]);

        Assert.Equal(WindowKind.Session, Assert.Single(figures.Snapshot.Windows).Kind);
        Assert.Contains("1440", Assert.Single(figures.IgnoredWindows), StringComparison.Ordinal);
    }

    [Fact]
    public void ARecordWithNoWindowOfAKnownLengthIsUnreadable()
    {
        // Leaving out every window would be a reading of nothing, which the display could only
        // show as a blank. A record that names no window Bingo knows has changed shape.
        var line = """
            {"timestamp":"2026-10-14T02:29:33.252Z","type":"event_msg","payload":{"type":"token_count","rate_limits":{"primary":{"used_percent":7.0,"window_minutes":60,"resets_at":1792536462}}}}
            """;

        ParseExpectingUnreadable(line);
    }

    [Fact]
    public void AMissingPercentageIsUnreadableRatherThanZero()
    {
        var unreadable = ParseExpectingUnreadable(Fixtures.CodexLines(Fixtures.CodexMissingField)[0]);

        Assert.False(string.IsNullOrWhiteSpace(unreadable.Reason));
    }

    [Fact]
    public void APercentageOfTheWrongTypeIsUnreadable()
    {
        var unreadable = ParseExpectingUnreadable(Fixtures.CodexLines(Fixtures.CodexWrongType)[0]);

        Assert.False(string.IsNullOrWhiteSpace(unreadable.Reason));
    }

    [Theory]
    [InlineData("""{"type":"event_msg","payload":{"type":"token_count","rate_limits":{"primary":{"used_percent":7.0,"window_minutes":300,"resets_at":1792536462}}}}""")]
    [InlineData("""{"timestamp":"last tuesday","type":"event_msg","payload":{"type":"token_count","rate_limits":{"primary":{"used_percent":7.0,"window_minutes":300,"resets_at":1792536462}}}}""")]
    public void ARecordWithNoUsableTimestampIsUnreadable(string line)
    {
        // Without its own time a reading has no age, and a figure without an age cannot go on
        // screen at all.
        ParseExpectingUnreadable(line);
    }

    [Theory]
    [InlineData("""{"timestamp":"2026-10-14T02:29:33.252Z","type":"event_msg","payload":{"type":"token_count","rate_limits":42}}""")]
    [InlineData("""{"timestamp":"2026-10-14T02:29:33.252Z","type":"event_msg","payload":{"type":"token_count","rate_limits":{"primary":"full"}}}""")]
    [InlineData("""{"timestamp":"2026-10-14T02:29:33.252Z","type":"event_msg","payload":{"type":"token_count","rate_limits":{"primary":{"used_percent":7.0,"window_minutes":300,"resets_at":"soon"}}}}""")]
    public void ARateLimitsValueOfTheWrongShapeIsUnreadable(string line)
    {
        ParseExpectingUnreadable(line);
    }

    [Fact]
    public void ALineCutOffMidWriteIsUnreadableNotAnException()
    {
        // Whether a cut-off last line is worth reporting is the reader's decision, because only
        // the reader knows it was the last line. The parser says only that it could not be read.
        ParseExpectingUnreadable(Fixtures.CodexLines(Fixtures.CodexTruncatedTail)[1]);
    }

    [Fact]
    public void TheReachedTypeIsCarriedAsWrittenAndChangesNoSeverity()
    {
        var figures = ParseExpectingFigures(Fixtures.CodexLines(Fixtures.CodexReachedType)[0]);

        Assert.Equal("primary", figures.ReachedType);
        Assert.All(figures.Snapshot.Windows, w => Assert.Equal(ServerSeverity.Unknown, w.Severity));
    }

    [Fact]
    public void ANullReachedTypeIsNull()
    {
        Assert.Null(ParseExpectingFigures(FirstRecord).ReachedType);
    }

    [Fact]
    public void ALineEndingInACarriageReturnReadsTheSame()
    {
        // Git may check the logs' fixtures out with CRLF, and a reader splitting on LF alone
        // would hand the parser a trailing carriage return.
        var plain = ParseExpectingFigures(FirstRecord).Snapshot;
        var crlf = ParseExpectingFigures(FirstRecord + "\r").Snapshot;

        Assert.Equal(plain.ObservedAt, crlf.ObservedAt);
        Assert.Equal(plain.Windows, crlf.Windows);
    }
}
