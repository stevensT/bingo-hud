using BingoHud.Core.Credentials;
using BingoHud.Core.Polling;
using BingoHud.Core.Time;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Monitoring;

/// <summary>
/// The one stateful orchestrator: holds the current reading, the single-flight guard, and the
/// backoff.
///
/// <para>
/// Everything upstream of this class is a pure function or a thin adapter behind an interface.
/// This is the only place that remembers anything, which makes it the only place a stale number
/// could be presented as a current one. That is why the age is computed on every read rather
/// than stored: there is no moment at which a reading's age can quietly stop being true.
/// </para>
/// <para>
/// A failed attempt never replaces or ages the last reading. It is recorded alongside it, so the
/// display can show both facts at once — here is the last thing we know, and here is what went
/// wrong since.
/// </para>
/// </summary>
public sealed class QuotaMonitor
{
    private readonly ICredentialProvider _credentials;
    private readonly IUsageClient _client;
    private readonly IClock _clock;

    private readonly object _gate = new();
    private Task<RefreshResult>? _inFlight;

    private QuotaSnapshot? _last;
    private FetchOutcome? _lastFailure;
    private string _pollReason = PollPolicy.Reasons.NothingIsHappening;
    private DateTimeOffset? _nextAttemptAt;

    // The earliest a refresh is accepted at all, and why. Distinct from _nextAttemptAt, which
    // is when the cadence would like the next attempt: the floor and a backoff are restraints
    // owed to the service, while the cadence is only a preference, and a user asking for a
    // number now outranks a preference.
    private DateTimeOffset? _acceptedFrom;
    private string _refusalReason = PollPolicy.Reasons.NothingIsHappening;

    public QuotaMonitor(ICredentialProvider credentials, IUsageClient client, IClock clock)
    {
        _credentials = credentials;
        _client = client;
        _clock = clock;
    }

    /// <summary>
    /// How old a reading may be before its age is worth showing.
    ///
    /// <para>
    /// The slowest ordinary cadence is <see cref="PollPolicy.Ceiling"/>, so a perfectly healthy
    /// reading is routinely half an hour old and calling that stale would make the marker
    /// meaningless. This is that ceiling plus half again: past it, a poll has actually been
    /// missed.
    /// </para>
    /// </summary>
    public static TimeSpan StaleAfter { get; } = PollPolicy.Ceiling + TimeSpan.FromMinutes(15);

    /// <summary>
    /// When the cadence wants the next automatic attempt, or null before the first one has been
    /// made. Not when a refresh will be accepted: one is refused only inside the floor or a
    /// backoff, which can end sooner (see <see cref="RefreshAsync"/>).
    ///
    /// <para>
    /// Exposed so the poll loop can wait on it instead of working the cadence out again. The
    /// monitor is already the only place that merges what happened to a request back into the
    /// signals the policy reads, and a second copy of that merge would drift from this one.
    /// </para>
    /// </summary>
    public DateTimeOffset? NextAttemptAt
    {
        get
        {
            lock (_gate)
            {
                return _nextAttemptAt;
            }
        }
    }

    /// <summary>
    /// The current state, with its age measured as of now.
    /// </summary>
    public ReadingState Current
    {
        get
        {
            lock (_gate)
            {
                return Build();
            }
        }
    }

    /// <summary>
    /// Fetches, unless a fetch is already running or the backoff has not elapsed.
    ///
    /// <para>
    /// Concurrent callers join the running fetch rather than starting their own (AC-27), and a
    /// manual refresh is subject to the same backoff as an automatic poll (AC-28). Joining takes
    /// precedence over refusing: a caller who arrives during a fetch wants the number that fetch
    /// is about to produce, and refusing them would be both unhelpful and untrue.
    /// </para>
    /// <para>
    /// What is refused is anything inside the floor, or inside a backoff after a failure or a
    /// server's wait. The cadence is not enforced here: a manual refresh five minutes into an
    /// idle half hour is accepted. The loop keeps to the cadence itself, by waiting on
    /// <see cref="NextAttemptAt"/>.
    /// </para>
    /// </summary>
    public Task<RefreshResult> RefreshAsync(
        PollSignals signals,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            // Only an unfinished fetch is worth joining. Testing completion here rather than
            // clearing the field from a continuation removes a race: a continuation runs at
            // some unspecified later moment, so a caller arriving immediately after the first
            // one finished could otherwise be handed a task that had already completed, and no
            // second fetch would ever happen.
            if (_inFlight is { IsCompleted: false } running)
            {
                return running;
            }

            // Stopped for good once the endpoint says this account cannot use it. Checked before
            // the backoff because no amount of waiting changes the answer, and a success from a
            // later manual refresh would otherwise clear the failure and leave the panel
            // promising polls the stopped loop will never make.
            if (PollLoop.Stops(_lastFailure))
            {
                return Task.FromResult<RefreshResult>(new RefreshResult.Stopped());
            }

            if (_acceptedFrom is { } accepted && _clock.Now < accepted)
            {
                return Task.FromResult<RefreshResult>(
                    new RefreshResult.Refused(_refusalReason, accepted));
            }

            var fetch = FetchAsync(signals, cancellationToken);
            _inFlight = fetch;

            return fetch;
        }
    }

    private async Task<RefreshResult> FetchAsync(
        PollSignals signals,
        CancellationToken cancellationToken)
    {
        var outcome = await AttemptAsync(cancellationToken).ConfigureAwait(false);

        lock (_gate)
        {
            Record(outcome);
            Schedule(signals, outcome);

            return new RefreshResult.Performed(Build());
        }
    }

    /// <summary>
    /// One attempt: get a credential, then use it. A missing credential is answered locally,
    /// without a request — sending a blank bearer would earn a 401 and report the user as
    /// rejected when they are simply not signed in.
    /// </summary>
    private async Task<FetchOutcome> AttemptAsync(CancellationToken cancellationToken)
    {
        var credential = await _credentials.GetAsync(cancellationToken).ConfigureAwait(false);

        if (credential is null)
        {
            return WithoutCredential();
        }

        return await _client.FetchAsync(credential, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Why there is no credential, in the terms the rest of the app speaks. This is the one
    /// place the file-level answer becomes an authentication outcome, and each branch sends the
    /// user somewhere different (AC-11).
    /// </summary>
    private FetchOutcome WithoutCredential() => _credentials.Probe() switch
    {
        CredentialAvailability.Absent => new FetchOutcome.AuthFailed(AuthFailureKind.SignedOut),
        CredentialAvailability.AccessDenied =>
            new FetchOutcome.AuthFailed(AuthFailureKind.PermissionDenied),

        // Claude Code is rewriting the token as we read it. That resolves itself in
        // milliseconds; asking the user to sign in over it would be absurd.
        CredentialAvailability.Busy => new FetchOutcome.Transient(RetryAfter: null),

        // Readable, and still no token in it. "Signed out" is a claim this evidence does not
        // support — something else is wrong with the file.
        _ => new FetchOutcome.AuthFailed(AuthFailureKind.Unspecified),
    };

    private void Record(FetchOutcome outcome)
    {
        if (outcome is FetchOutcome.Success success)
        {
            _last = success.Snapshot;
            _lastFailure = null;

            return;
        }

        // The previous reading survives, and so does its age. A failed attempt is not a reading,
        // so it cannot reset the clock on the last one — otherwise a run of failures would keep
        // an hours-old number looking a minute old.
        _lastFailure = outcome;
    }

    private void Schedule(PollSignals signals, FetchOutcome outcome)
    {
        // The caller knows about the machine and the user; the monitor knows what just happened
        // to the request. Those two halves are combined here rather than either side guessing.
        var informed = signals with
        {
            LastAttemptFailed = outcome is not FetchOutcome.Success,
            ServerRetryAfter = (outcome as FetchOutcome.Transient)?.RetryAfter
                ?? signals.ServerRetryAfter,
        };

        var (delay, reason) = PollPolicy.NextDelay(informed);

        // One reading of the clock for both instants. The loop relies on the floor never landing
        // after the cadence, and two readings could put it a tick past when both are the floor.
        var now = _clock.Now;

        _pollReason = reason;
        _nextAttemptAt = now + delay;

        // A failure or a server's wait is a backoff, and holds every caller to the whole of it.
        // After a success only the floor does.
        if (informed.LastAttemptFailed || informed.ServerRetryAfter is not null)
        {
            _acceptedFrom = _nextAttemptAt;
            _refusalReason = reason;
        }
        else
        {
            _acceptedFrom = now + PollPolicy.Floor;
            _refusalReason = PollPolicy.Reasons.JustChecked;
        }
    }

    private ReadingState Build()
    {
        var age = _last is null ? TimeSpan.Zero : _clock.Now - _last.ObservedAt;

        return new ReadingState(_last, FreshnessOf(age), _lastFailure, age, _pollReason);
    }

    /// <summary>
    /// How much the current reading can still be trusted.
    ///
    /// <para>
    /// Frozen is decided by the failure rather than by the age: an invalidated token or an
    /// endpoint that is gone will not refresh no matter how long is waited, and presenting that
    /// as merely stale would suggest a retry is coming. A transient failure is the opposite case
    /// and must not freeze anything — it is expected to pass.
    /// </para>
    /// </summary>
    private Freshness FreshnessOf(TimeSpan age) => _lastFailure switch
    {
        FetchOutcome.AuthFailed or FetchOutcome.Unsupported => Freshness.Frozen,
        _ => age > StaleAfter ? Freshness.Stale : Freshness.Fresh,
    };
}
