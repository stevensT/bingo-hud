using BingoHud.Core.Time;

namespace BingoHud.Core.Tests;

/// <summary>
/// A clock that only moves when a test tells it to, and never really waits.
///
/// It lives in the test project rather than in Core deliberately: Core ships to users, and a
/// controllable clock is not something the app should be able to reach for.
/// </summary>
public sealed class TestClock : IClock
{
    private readonly List<TimeSpan> _requestedDelays = [];

    public TestClock(DateTimeOffset now)
    {
        Now = now;
    }

    public DateTimeOffset Now { get; private set; }

    /// <summary>
    /// Every span this clock has been asked to wait for, in order. A loop's cadence is not
    /// otherwise observable — the delays it asks for are the behaviour.
    /// </summary>
    public IReadOnlyList<TimeSpan> RequestedDelays => _requestedDelays;

    /// <summary>
    /// Moves time forward. Negative spans are allowed — a clock that cannot go backwards
    /// cannot exercise the case where a server reset time is already in the past.
    /// </summary>
    public void Advance(TimeSpan by)
    {
        Now = Now.Add(by);
    }

    /// <summary>
    /// Records the request, moves time forward by it, and returns immediately. Collapsing the
    /// wait is what lets a test drive a thirty-minute cadence in microseconds while the code
    /// under test still sees time pass exactly as it asked.
    /// </summary>
    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _requestedDelays.Add(duration);
        Advance(duration);

        return Task.CompletedTask;
    }
}
