using BingoHud.Core.Display;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// Equality of a HUD line once it carries a bar (display options AC-8).
///
/// <para>
/// The shell repaints only when <see cref="HudContent.SameAs"/> says the content changed, and that
/// compares lines with the record's own equality. A record compares a list member by reference, so
/// without equality written out, two lines holding the same bar would differ, every reading would
/// look new, and the HUD would rebuild itself every second.
/// </para>
/// </summary>
public class ReadoutLineTests
{
    private static ReadoutLine Line(IReadOnlyList<double>? bar) =>
        new("5h", "75% used", "2.3h", Severity.Normal, bar);

    [Fact]
    public void LinesWithEqualBarsBuiltSeparatelyAreEqual()
    {
        var first = Line(Bar.Segments(75));
        var second = Line(Bar.Segments(75));

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void LinesWhoseBarsDifferInOneSegmentAreNotEqual()
    {
        Assert.NotEqual(Line(Bar.Segments(75)), Line(Bar.Segments(76)));
    }

    [Fact]
    public void ALineWithABarIsNotALineWithout()
    {
        Assert.NotEqual(Line(Bar.Segments(0)), Line(null));
    }

    [Theory]
    [InlineData("Week", "75% used", "2.3h")]
    [InlineData("5h", "74% used", "2.3h")]
    [InlineData("5h", "75% used", "2.2h")]
    [InlineData("5h", "75% used", null)]
    public void LinesThatDifferInAnyOneFieldAreNotEqual(string window, string percent, string? reset)
    {
        // The equality is written by hand, so a field it forgets is a field whose change never
        // repaints. A countdown ticking from 2.3h to 2.2h under an unchanged figure is exactly the
        // change that would freeze on screen.
        var line = Line(Bar.Segments(75));
        var other = line with { Window = window, Percent = percent, Reset = reset };

        Assert.NotEqual(line, other);
        Assert.False(new HudContent.Reading([line], Mark: null)
            .SameAs(new HudContent.Reading([other], Mark: null)));
    }

    [Fact]
    public void TheShellSeesTheSameHudWhenOnlyTheListObjectsAreNew()
    {
        var before = new HudContent.Reading([Line(Bar.Segments(75))], Mark: null);
        var after = new HudContent.Reading([Line(Bar.Segments(75))], Mark: null);

        Assert.True(before.SameAs(after));
    }

    [Fact]
    public void TheShellRepaintsWhenTheBarMoves()
    {
        var before = new HudContent.Reading([Line(Bar.Segments(75))], Mark: null);
        var after = new HudContent.Reading([Line(Bar.Segments(76))], Mark: null);

        Assert.False(before.SameAs(after));
    }
}
