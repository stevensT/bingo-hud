using BingoHud.Core.Time;

namespace BingoHud.Core.Tests;

/// <summary>
/// Time is injected throughout Core so that staleness, reset countdowns, window-occurrence
/// identity, and backoff are all deterministic under test. These tests pin the two
/// implementations that make that possible.
/// </summary>
public class ClockTests
{
    private static readonly DateTimeOffset Instant =
        new(2026, 8, 30, 9, 34, 31, TimeSpan.FromHours(-7));

    [Fact]
    public void TestClockReturnsExactlyTheInstantItWasGiven()
    {
        var clock = new TestClock(Instant);

        Assert.Equal(Instant, clock.Now);
    }

    [Fact]
    public void TestClockDoesNotAdvanceOnItsOwn()
    {
        var clock = new TestClock(Instant);

        var first = clock.Now;
        var second = clock.Now;

        Assert.Equal(first, second);
    }

    [Fact]
    public void TestClockAdvancesOnlyWhenTold()
    {
        var clock = new TestClock(Instant);

        clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(Instant.AddMinutes(5), clock.Now);
    }

    [Fact]
    public void TestClockPreservesTheOffsetItWasGiven()
    {
        // A clock that silently normalised to UTC would make every local-time reset
        // assertion in later phases lie.
        var clock = new TestClock(Instant);

        Assert.Equal(TimeSpan.FromHours(-7), clock.Now.Offset);
    }

    [Fact]
    public void SystemClockReturnsAnInstantBetweenTwoReadingsOfNow()
    {
        var before = DateTimeOffset.Now;
        var actual = new SystemClock().Now;
        var after = DateTimeOffset.Now;

        Assert.InRange(actual, before, after);
    }
}
