using BingoHud.Core.Display;

namespace BingoHud.Core.Tests;

/// <summary>
/// How full each of the bar's ten segments is (display options AC-8, AC-13).
///
/// <para>
/// The fill is the exact figure, not rounded to a segment: segments are tick marks. So the bar and
/// the percentage beside it can never disagree, and no rounding rule exists to get wrong. The
/// figure itself is never touched here; clamping only limits how much of the bar is drawn.
/// </para>
/// </summary>
public class BarTests
{
    [Fact]
    public void ThereAreAlwaysTenSegments()
    {
        Assert.Equal(10, Bar.Segments(42).Count);
    }

    [Fact]
    public void NothingUsedFillsNothing()
    {
        Assert.All(Bar.Segments(0), s => Assert.Equal(0, s));
    }

    [Fact]
    public void AllUsedFillsEverything()
    {
        Assert.All(Bar.Segments(100), s => Assert.Equal(1, s));
    }

    [Fact]
    public void APartlyUsedSegmentIsPartlyFilled()
    {
        Assert.Equal([1, 1, 1, 1, 1, 1, 1, 0.5, 0, 0], Bar.Segments(75));
    }

    [Fact]
    public void LessThanOneSegmentStillShows()
    {
        // 3% is a sliver of the first segment, not nothing.
        var segments = Bar.Segments(3);

        Assert.Equal(0.3, segments[0], precision: 10);
        Assert.All(segments.Skip(1), s => Assert.Equal(0, s));
    }

    [Theory]
    [InlineData(100.5)]
    [InlineData(250)]
    public void OverAHundredFillsTheBarAndStops(double used)
    {
        Assert.Equal(Bar.Segments(100), Bar.Segments(used));
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    public void NothingSensibleToDrawDrawsNothing(double used)
    {
        // Not expected from the server, but a negative or NaN width would throw in the shell.
        Assert.Equal(Bar.Segments(0), Bar.Segments(used));
    }
}
