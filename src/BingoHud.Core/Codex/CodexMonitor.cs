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
/// A rescan reads the logs only when one has been written since the last read. Codex writes a
/// record at the end of each turn, so between turns the answer cannot have changed, and the shell
/// can ask every minute for the cost of listing two folders. Three cases read regardless: the
/// first look, a look after a failure (a lock is released without anything being written), and a
/// look while Codex is not found (installing it creates a folder before it writes any log).
/// </para>
/// </summary>
public sealed class CodexMonitor
{
    private readonly Func<DateTime?> _newestWrite;
    private readonly Func<CodexScan> _scan;
    private readonly IClock _clock;

    // Rescan runs on the shell's timer and on "Refresh now"; Current is read once a second by the
    // display. One lock covers all of it, and a scan is fast enough to hold it.
    private readonly object _gate = new();

    private CodexPresence _presence = CodexPresence.NotLookedYet;
    private CodexRecordResult.Figures? _last;
    private CodexProblem? _problem;
    private DateTime? _readAt;

    /// <summary>The monitor over the real logs.</summary>
    public CodexMonitor(CodexLogReader reader, IClock clock)
        : this(reader.NewestWrite, reader.Scan, clock)
    {
    }

    /// <summary>
    /// The monitor over any source of scans, so a test can count them.
    /// </summary>
    /// <param name="newestWrite">When any log was last written, or null when there are none.</param>
    /// <param name="scan">One look through the logs.</param>
    /// <param name="clock">The time the age is measured against.</param>
    public CodexMonitor(Func<DateTime?> newestWrite, Func<CodexScan> scan, IClock clock)
    {
        _newestWrite = newestWrite;
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
    /// Looks at the logs again, if anything could have changed since the last look.
    /// </summary>
    public void Rescan()
    {
        lock (_gate)
        {
            var newestWrite = _newestWrite();

            var mustRead = _presence != CodexPresence.Found
                || _problem is not null
                || newestWrite != _readAt;

            if (!mustRead)
            {
                return;
            }

            Record(_scan());
            _readAt = newestWrite;
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
