using BingoHud.Core.Time;

namespace BingoHud.Core.Tests;

/// <summary>
/// A clock that only moves when a test tells it to.
///
/// It lives in the test project rather than in Core deliberately: Core ships to users, and a
/// controllable clock is not something the app should be able to reach for.
/// </summary>
public sealed class TestClock : IClock
{
    public TestClock(DateTimeOffset now)
    {
        Now = now;
    }

    public DateTimeOffset Now { get; private set; }

    /// <summary>
    /// Moves time forward. Negative spans are allowed — a clock that cannot go backwards
    /// cannot exercise the case where a server reset time is already in the past.
    /// </summary>
    public void Advance(TimeSpan by)
    {
        Now = Now.Add(by);
    }
}
