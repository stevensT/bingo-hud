using BingoHud.Core.Codex;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// The Codex reading held between scans, and when the logs are read again
/// (codex-quota AC-1, AC-3, AC-3a, AC-7).
/// </summary>
public class CodexMonitorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 8, 0, 0, TimeSpan.Zero);

    private static readonly string[] Session = Fixtures.CodexLines(Fixtures.CodexSession);

    // The first record of the session fixture, written at 02:29:33.252 UTC: five-hour 44%.
    private static CodexScan.Reading First => Reading(Session[1]);

    // The last record of the session fixture, written at 06:32:06.293 UTC: five-hour 24%.
    private static CodexScan.Reading Last => Reading(Session[8]);

    private static CodexScan.Reading Reading(string line) =>
        new(Assert.IsType<CodexRecordResult.Figures>(CodexRecord.Parse(line)));

    private static readonly CodexScan Locked =
        new CodexScan.Problem(new CodexProblem.LogsUnreadable("A Codex session log could not be opened."));

    /// <summary>
    /// A log source whose next scan and newest write time a test sets, and which counts how often
    /// it was read.
    /// </summary>
    private sealed class FakeLogs
    {
        public DateTime? NewestWrite { get; set; } = new DateTime(2026, 10, 14, 6, 32, 6, DateTimeKind.Utc);

        public CodexScan Next { get; set; } = new CodexScan.NoReading();

        public int Scans { get; private set; }

        public CodexMonitor Monitor(TestClock clock) => new(() => NewestWrite, () =>
        {
            Scans++;
            return Next;
        }, clock);
    }

    private static double SessionUsed(CodexState state) =>
        Assert.Single(state.Last!.Windows, w => w.Kind == WindowKind.Session).UsedPercent;

    [Fact]
    public void BeforeAnyScanNothingIsClaimed()
    {
        var monitor = new FakeLogs().Monitor(new TestClock(Now));

        var state = monitor.Current;

        Assert.Equal(CodexPresence.NotLookedYet, state.Presence);
        Assert.Null(state.Last);
        Assert.Null(state.Problem);
    }

    [Fact]
    public void AScanWithFiguresBecomesTheReading()
    {
        var logs = new FakeLogs { Next = Last };
        var monitor = logs.Monitor(new TestClock(Now));

        monitor.Rescan();
        var state = monitor.Current;

        Assert.Equal(CodexPresence.Found, state.Presence);
        Assert.Equal(24.0, SessionUsed(state));
        Assert.Null(state.Problem);
    }

    [Fact]
    public void TheAgeIsMeasuredFromTheRecordNotFromTheScan()
    {
        // Scanned just now, written at 06:32:06.293: the figure is that old, not a second old
        // (AC-3). Timed by the scan, an idle afternoon would read as a current figure.
        var monitor = new FakeLogs { Next = Last }.Monitor(new TestClock(Now));

        monitor.Rescan();

        Assert.Equal(Now - Last.Figures.Snapshot.ObservedAt, monitor.Current.Age);
    }

    [Fact]
    public void TheAgeKeepsGrowingBetweenScans()
    {
        var clock = new TestClock(Now);
        var monitor = new FakeLogs { Next = Last }.Monitor(clock);
        monitor.Rescan();

        clock.Advance(TimeSpan.FromMinutes(20));

        Assert.Equal(Now.AddMinutes(20) - Last.Figures.Snapshot.ObservedAt, monitor.Current.Age);
    }

    [Fact]
    public void TheLogsAreNotReadAgainWhenNothingHasBeenWritten()
    {
        var logs = new FakeLogs { Next = Last };
        var monitor = logs.Monitor(new TestClock(Now));

        monitor.Rescan();
        monitor.Rescan();
        monitor.Rescan();

        Assert.Equal(1, logs.Scans);
    }

    [Fact]
    public void TheLogsAreReadAgainOnceALogHasBeenWritten()
    {
        var logs = new FakeLogs { Next = First };
        var monitor = logs.Monitor(new TestClock(Now));
        monitor.Rescan();

        logs.NewestWrite = logs.NewestWrite!.Value.AddMinutes(1);
        logs.Next = Last;
        monitor.Rescan();

        Assert.Equal(2, logs.Scans);
        Assert.Equal(24.0, SessionUsed(monitor.Current));
    }

    [Fact]
    public void AfterAFailedScanTheLogsAreReadAgainEvenWithNothingWritten()
    {
        // A log held open by another program is let go of without being written to. Waiting for
        // a write would leave the failure on screen until the next Codex turn.
        var logs = new FakeLogs { Next = Locked };
        var monitor = logs.Monitor(new TestClock(Now));
        monitor.Rescan();

        logs.Next = Last;
        monitor.Rescan();

        Assert.Equal(2, logs.Scans);
        Assert.Null(monitor.Current.Problem);
    }

    [Fact]
    public void AFailedScanKeepsTheLastReadingAndSaysWhy()
    {
        // AC-3a, failed: the figure was true when written, and Bingo can no longer tell whether
        // Codex has been used since. Both facts are on screen at once.
        var logs = new FakeLogs { Next = Last };
        var monitor = logs.Monitor(new TestClock(Now));
        monitor.Rescan();

        logs.NewestWrite = logs.NewestWrite!.Value.AddMinutes(1);
        logs.Next = Locked;
        monitor.Rescan();
        var state = monitor.Current;

        Assert.Equal(24.0, SessionUsed(state));
        Assert.IsType<CodexProblem.LogsUnreadable>(state.Problem);
    }

    [Fact]
    public void AnUnknownShapeAlsoKeepsTheLastReadingAndSaysWhy()
    {
        var logs = new FakeLogs { Next = Last };
        var monitor = logs.Monitor(new TestClock(Now));
        monitor.Rescan();

        logs.NewestWrite = logs.NewestWrite!.Value.AddMinutes(1);
        logs.Next = new CodexScan.Problem(new CodexProblem.UnknownShape("The primary window had no percentage."));
        monitor.Rescan();
        var state = monitor.Current;

        Assert.Equal(24.0, SessionUsed(state));
        Assert.IsType<CodexProblem.UnknownShape>(state.Problem);
    }

    [Fact]
    public void ACleanScanWithNoNewerRecordKeepsTheReadingAndClearsTheProblem()
    {
        // AC-3a, idle: the logs read fine and say nothing newer, so the figure is simply old.
        var logs = new FakeLogs { Next = Last };
        var monitor = logs.Monitor(new TestClock(Now));
        monitor.Rescan();
        logs.Next = Locked;
        logs.NewestWrite = logs.NewestWrite!.Value.AddMinutes(1);
        monitor.Rescan();

        logs.Next = Last;
        monitor.Rescan();
        var state = monitor.Current;

        Assert.Equal(24.0, SessionUsed(state));
        Assert.Null(state.Problem);
    }

    [Fact]
    public void LogsWithNoFiguresAfterAReadingKeepTheReading()
    {
        // Codex was found and read cleanly, and the figure it last wrote has not stopped being
        // the last thing it said.
        var logs = new FakeLogs { Next = Last };
        var monitor = logs.Monitor(new TestClock(Now));
        monitor.Rescan();

        logs.NewestWrite = logs.NewestWrite!.Value.AddMinutes(1);
        logs.Next = new CodexScan.NoReading();
        monitor.Rescan();
        var state = monitor.Current;

        Assert.Equal(CodexPresence.Found, state.Presence);
        Assert.Equal(24.0, SessionUsed(state));
        Assert.Null(state.Problem);
    }

    [Fact]
    public void FoundWithNoReadingIsFoundAndEmpty()
    {
        var monitor = new FakeLogs { Next = new CodexScan.NoReading() }.Monitor(new TestClock(Now));

        monitor.Rescan();
        var state = monitor.Current;

        Assert.Equal(CodexPresence.Found, state.Presence);
        Assert.Null(state.Last);
        Assert.Equal(TimeSpan.Zero, state.Age);
    }

    [Fact]
    public void CodexNotFoundShowsNoFigureEvenAfterAReading()
    {
        // AC-7: a Codex folder that has gone is Codex not installed, and a figure beside "not
        // found" would contradict it.
        var logs = new FakeLogs { Next = Last };
        var monitor = logs.Monitor(new TestClock(Now));
        monitor.Rescan();

        logs.NewestWrite = null;
        logs.Next = new CodexScan.NotFound();
        monitor.Rescan();
        var state = monitor.Current;

        Assert.Equal(CodexPresence.NotFound, state.Presence);
        Assert.Null(state.Last);
    }

    [Fact]
    public void WhileCodexIsNotFoundEveryRescanLooksAgain()
    {
        // Installing Codex creates its folder before it writes any log, so the newest write time
        // stays empty across the change. Waiting for it to move would leave "not found" on screen
        // until the first Codex turn.
        var logs = new FakeLogs { NewestWrite = null, Next = new CodexScan.NotFound() };
        var monitor = logs.Monitor(new TestClock(Now));
        monitor.Rescan();

        logs.Next = new CodexScan.NoReading();
        monitor.Rescan();

        Assert.Equal(2, logs.Scans);
        Assert.Equal(CodexPresence.Found, monitor.Current.Presence);
    }

    [Fact]
    public void TheReachedTypeAndIgnoredWindowsComeWithTheReading()
    {
        var reached = Reading(Fixtures.CodexLines(Fixtures.CodexReachedType)[0]);
        var monitor = new FakeLogs { Next = reached }.Monitor(new TestClock(Now));
        monitor.Rescan();

        Assert.Equal("primary", monitor.Current.ReachedType);

        var unknown = Reading(Fixtures.CodexLines(Fixtures.CodexUnknownWindow)[0]);
        var other = new FakeLogs { Next = unknown }.Monitor(new TestClock(Now));
        other.Rescan();

        Assert.Single(other.Current.IgnoredWindows);
        Assert.Null(other.Current.ReachedType);
    }

    [Fact]
    public void OverRealLogsANewTurnIsPickedUpOnTheNextRescan()
    {
        // The whole path on disk: a log written, read, then a newer record appended, as Codex
        // does at the end of a turn.
        using var codex = new TempDirectory();
        var log = Path.Combine(codex.Path, "sessions", "2026", "10", "14", "live.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        File.WriteAllText(log, Session[1] + "\n");
        File.SetLastWriteTimeUtc(log, new DateTime(2026, 10, 14, 2, 30, 0, DateTimeKind.Utc));

        var monitor = new CodexMonitor(new CodexLogReader(codex.Path), new TestClock(Now));
        monitor.Rescan();
        Assert.Equal(44.0, SessionUsed(monitor.Current));

        File.AppendAllText(log, Session[8] + "\n");
        File.SetLastWriteTimeUtc(log, new DateTime(2026, 10, 14, 6, 33, 0, DateTimeKind.Utc));
        monitor.Rescan();

        Assert.Equal(24.0, SessionUsed(monitor.Current));
    }
}
