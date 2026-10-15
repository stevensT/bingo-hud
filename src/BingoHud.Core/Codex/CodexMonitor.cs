using BingoHud.Core.Time;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Codex;

/// <summary>
/// Holds the current Codex reading, and looks at the logs again when asked
/// (codex-quota AC-1, AC-3, AC-3a, AC-7).
///
/// <para>
/// The Codex counterpart of <see cref="Monitoring.QuotaMonitor"/>, and much smaller: reading a
/// local file needs no credential, no backoff and no single-flight guard. What it shares with the
/// Claude monitor is the rule that matters most. A failed look never replaces or ages the last
/// reading; it is recorded beside it, so the display can say both what was last known and why it
/// cannot be confirmed. And the age is measured on every read, from the record's own time, so
/// there is no moment at which it can quietly stop being true.
/// </para>
/// <para>
/// Every requested rescan reads the logs. Windows can leave a live writer's file timestamp
/// unchanged while it appends records, so file times cannot prove that nothing has changed.
/// </para>
/// </summary>
public sealed class CodexMonitor
{
    private readonly Func<CodexScan> _scan;
    private readonly IClock _clock;

    // Rescan runs on the shell's timer and on "Refresh now"; Current is read once a second by the
    // display, on the UI thread. A scan reads every log and grows with them, so it holds only its
    // own lock, which keeps two scans from finishing out of order. The state lock is held just
    // long enough to record a result or read one, so the display never waits for a scan.
    private readonly object _scanGate = new();
    private readonly object _gate = new();

    private CodexPresence _presence = CodexPresence.NotLookedYet;
    private CodexRecordResult.Figures? _last;
    private CodexProblem? _problem;

    /// <summary>The monitor over the real logs.</summary>
    public CodexMonitor(CodexLogReader reader, IClock clock)
        : this(reader.Scan, clock)
    {
    }

    /// <summary>
    /// The monitor over any source of scans, so a test can count them.
    /// </summary>
    /// <param name="scan">One look through the logs.</param>
    /// <param name="clock">The time the age is measured against.</param>
    public CodexMonitor(Func<CodexScan> scan, IClock clock)
    {
        _scan = scan;
        _clock = clock;
    }

    /// <summary>
    /// The current state, with its age measured as of now.
    /// </summary>
    public CodexState Current
    {
        get
        {
            lock (_gate)
            {
                var snapshot = _last?.Snapshot;
                var age = snapshot is null ? TimeSpan.Zero : _clock.Now - snapshot.ObservedAt;

                return new CodexState(
                    _presence,
                    snapshot,
                    _problem,
                    age,
                    _last?.ReachedType,
                    _last?.IgnoredWindows ?? []);
            }
        }
    }

    /// <summary>
    /// Looks at the logs again, even when their file timestamps have not changed.
    /// </summary>
    public void Rescan()
    {
        lock (_scanGate)
        {
            var scan = ScanOrProblem();

            lock (_gate)
            {
                Record(scan);
            }
        }
    }

    /// <summary>
    /// One look, with anything the reader did not expect recorded as a failed look. The rescan
    /// runs on a timer thread, where an escaped exception would end the whole app, Claude's
    /// readout with it; as a problem, it is shown beside the last reading and retried next time.
    /// </summary>
    private CodexScan ScanOrProblem()
    {
        try
        {
            return _scan();
        }
        catch (Exception e)
        {
            return new CodexScan.Problem(new CodexProblem.LogsUnreadable(
                $"Bingo hit an unexpected error reading the Codex logs: {e.Message}"));
        }
    }

    private void Record(CodexScan scan)
    {
        switch (scan)
        {
            case CodexScan.Reading reading:
                _presence = CodexPresence.Found;
                _last = reading.Figures;
                _problem = null;
                break;

            // Read cleanly and nothing with figures in it. A reading found earlier is still the
            // last thing Codex said, so it stays, idle.
            case CodexScan.NoReading:
                _presence = CodexPresence.Found;
                _problem = null;
                break;

            // The last reading stays, marked by the problem beside it.
            case CodexScan.Problem problem:
                _presence = CodexPresence.Found;
                _problem = problem.Cause;
                break;

            // Not installed, so nothing it said earlier is shown beside "not found" (AC-7).
            case CodexScan.NotFound:
                _presence = CodexPresence.NotFound;
                _last = null;
                _problem = null;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(scan), scan, null);
        }
    }
}
