using BingoHud.Core.Display;

namespace BingoHud.Core.Tests;

/// <summary>
/// How old a reading is, said once for the whole app.
///
/// <para>
/// The panel has phrased this since 6.8 and the HUD needs the same words at 7.1, which is the
/// reason it moved here. Two vocabularies for one fact is how a panel saying "21 min old" ends
/// up beside a HUD saying "21m" — both true, and the pair reads as two different readings.
/// </para>
/// <para>
/// Two phrasings, because a sentence and a label want different shapes. "21 min" drops into
/// "no poll has succeeded in 21 min"; "21 min old" stands on its own beside a number. Both are
/// built from the same measurement so they can never disagree about the figure.
/// </para>
/// </summary>
public class AgeTextTests
{
    [Fact]
    public void AnAgeUnderAMinuteIsNotShownAsZeroMinutes()
    {
        // "0 min old" reads as a rounding artefact rather than as a fresh reading.
        Assert.Equal("just now", AgeText.Old(TimeSpan.FromSeconds(59)));
    }

    [Fact]
    public void MinutesAreShownAsWholeMinutes()
    {
        Assert.Equal("21 min old", AgeText.Old(TimeSpan.FromMinutes(21.6)));
    }

    [Fact]
    public void OneHourIsSingular()
    {
        Assert.Equal("1 hour old", AgeText.Old(TimeSpan.FromMinutes(75)));
    }

    [Fact]
    public void MoreThanOneHourIsPlural()
    {
        Assert.Equal("3 hours old", AgeText.Old(TimeSpan.FromMinutes(200)));
    }

    [Fact]
    public void ASpanDropsTheWordOldSoItCanSitInASentence()
    {
        Assert.Equal("21 min", AgeText.Span(TimeSpan.FromMinutes(21)));
    }

    [Fact]
    public void ASpanUnderAMinuteIsStillAnAmountOfTime()
    {
        // "just now" is a moment, not a duration. A sentence needs the duration.
        Assert.Equal("under a minute", AgeText.Span(TimeSpan.FromSeconds(30)));
    }
}
