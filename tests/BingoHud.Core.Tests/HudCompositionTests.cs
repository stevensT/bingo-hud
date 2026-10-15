using System.Globalization;
using BingoHud.Core.Codex;
using BingoHud.Core.Display;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// The HUD as one section per product (codex-quota AC-4, AC-5, AC-7a, AC-8).
///
/// <para>
/// What each section says about its own reading is tested elsewhere: Claude's in
/// <see cref="ClaudeOnlyHudTests"/> and <see cref="ReadoutTests"/>, Codex's marks and phrases
/// in the Codex section's tests. This file is about how the sections are put together: which
/// products get one, in what order, how every line is labelled, how collapse works when there are
/// two products, and what the accent bar shows.
/// </para>
/// </summary>
public class HudCompositionTests
{
    private static readonly CultureInfo TwelveHour = new("en-US");
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 9, 34, 0, TimeSpan.FromHours(-7));

    private static QuotaSnapshot Snapshot(double session, double weekly) => new(
        [
            new QuotaWindow(WindowKind.Session, session, Now.AddMinutes(53), ServerSeverity.Unknown),
            new QuotaWindow(WindowKind.WeeklyAll, weekly, Now.AddDays(2), ServerSeverity.Unknown),
        ],
        Now,
        "{}");

    private static ReadingState Claude(double session = 12, double weekly = 41, Freshness freshness = Freshness.Fresh) =>
        new(Snapshot(session, weekly), freshness,
            freshness == Freshness.Frozen ? new FetchOutcome.AuthFailed(AuthFailureKind.SignedOut) : null,
            TimeSpan.Zero, "test");

    private static CodexState Codex(double session = 30, double weekly = 20) =>
        new(CodexPresence.Found, Snapshot(session, weekly), Problem: null, TimeSpan.FromMinutes(5), ReachedType: null, []);

    private static CodexState CodexNotFound =>
        new(CodexPresence.NotFound, Last: null, Problem: null, TimeSpan.Zero, ReachedType: null, []);

    private static UserSettings Showing(bool claude, bool codex, bool collapse = false) =>
        UserSettings.Default with { Products = new ProductSwitches(claude, codex), Collapse = collapse };

    private static Hud Compose(ReadingState claude, CodexState codex, UserSettings settings) =>
        Readout.Compose(claude, codex, settings, Now, TwelveHour);

    private static IReadOnlyList<ReadoutLine> Lines(HudSection section) =>
        Assert.IsType<HudContent.Reading>(section.Content).Lines;

    // --- Which products get a section

    [Fact]
    public void BeforeTheQuestionIsAnsweredOnlyClaudeIsShown()
    {
        // The first-start question appears before the HUD, so this is only the moment between,
        // or a failed save. Claude alone is what Bingo has always shown.
        var hud = Compose(Claude(), Codex(), UserSettings.Default);

        Assert.Equal(Product.Claude, Assert.Single(hud.Sections).Product);
    }

    [Fact]
    public void BothProductsOnGiveTwoSectionsClaudeFirst()
    {
        var hud = Compose(Claude(), Codex(), Showing(claude: true, codex: true));

        Assert.Equal([Product.Claude, Product.Codex], hud.Sections.Select(s => s.Product));
    }

    [Fact]
    public void AProductSwitchedOffHasNoSection()
    {
        var hud = Compose(Claude(), Codex(), Showing(claude: false, codex: true));

        Assert.Equal(Product.Codex, Assert.Single(hud.Sections).Product);
    }

    [Fact]
    public void AProductShownWithNothingToShowStillHasASection()
    {
        // AC-7: never hidden, because a missing line looks the same as Bingo failing to look.
        var hud = Compose(Claude(), CodexNotFound, Showing(claude: true, codex: true));

        var codex = Assert.Single(hud.Sections, s => s.Product == Product.Codex);
        Assert.IsType<HudContent.Empty>(codex.Content);
    }

    // --- Labels

    [Fact]
    public void EveryLineIsLabelledWithItsProduct()
    {
        // AC-7a: neither product is the unlabelled default.
        var hud = Compose(Claude(), Codex(), Showing(claude: true, codex: true));

        Assert.Equal(["Claude 5h", "Claude Week"], Lines(hud.Sections[0]).Select(l => l.Window));
        Assert.Equal(["Codex 5h", "Codex Week"], Lines(hud.Sections[1]).Select(l => l.Window));
    }

    [Fact]
    public void CodexLinesFollowTheSameDisplaySettings()
    {
        // AC-5: direction, reset format and bar are Bingo's settings, not Claude's.
        var settings = Showing(claude: true, codex: true) with
        {
            Direction = DisplayDirection.Remaining,
            ResetFormat = ResetFormat.Countdown,
            ShowBar = true,
        };

        var codex = Lines(Compose(Claude(), Codex(session: 30), settings).Sections[1])[0];

        Assert.Equal("70% left", codex.Percent);
        Assert.Equal("53m", codex.Reset);
        Assert.Equal(Bar.Segments(30), codex.Bar);
    }

    // --- Collapse

    [Fact]
    public void CollapseWorksWithinEachProduct()
    {
        // Each product shrinks to its own worst window. Collapsing across products would let a
        // Claude warning hide a Codex window running out.
        var hud = Compose(Claude(session: 80, weekly: 41), Codex(session: 10, weekly: 60),
            Showing(claude: true, codex: true, collapse: true));

        Assert.Equal(["Claude 5h"], Lines(hud.Sections[0]).Select(l => l.Window));
        Assert.Equal(["Codex Week"], Lines(hud.Sections[1]).Select(l => l.Window));
    }

    [Fact]
    public void CollapseKeepsBothWindowsOfAProductWhenBothAreInTrouble()
    {
        var hud = Compose(Claude(), Codex(session: 80, weekly: 85),
            Showing(claude: true, codex: true, collapse: true));

        Assert.Equal(["Codex 5h", "Codex Week"], Lines(hud.Sections[1]).Select(l => l.Window));
    }

    // --- The accent bar

    [Fact]
    public void TheAccentIsTheWorstLineAcrossProducts()
    {
        var hud = Compose(Claude(session: 12), Codex(session: 95), Showing(claude: true, codex: true));

        Assert.Equal(Severity.Critical, hud.Overall);
    }

    [Fact]
    public void AFrozenReadingCannotOwnTheAccent()
    {
        // Claude's 95% is frozen, so drawn without colour; Codex's 80% is live.
        var hud = Compose(Claude(session: 95, freshness: Freshness.Frozen), Codex(session: 80),
            Showing(claude: true, codex: true));

        Assert.Equal(Severity.Warning, hud.Overall);
    }

    [Fact]
    public void NothingToShowAnywhereIsNormal()
    {
        var nothing = new ReadingState(null, Freshness.Fresh, null, TimeSpan.Zero, "test");

        Assert.Equal(Severity.Normal, Compose(nothing, CodexNotFound, Showing(claude: true, codex: true)).Overall);
    }

    // --- Repainting

    [Fact]
    public void TheSameStateComposesTheSameHud()
    {
        // Composed twice, the lists are different objects. A repaint check that compared them by
        // reference would rebuild the HUD every second.
        var settings = Showing(claude: true, codex: true) with { ShowBar = true };

        Assert.True(Compose(Claude(), Codex(), settings).SameAs(Compose(Claude(), Codex(), settings)));
    }

    [Fact]
    public void AChangeInEitherSectionIsADifferentHud()
    {
        var settings = Showing(claude: true, codex: true);
        var before = Compose(Claude(), Codex(session: 30), settings);

        Assert.False(before.SameAs(Compose(Claude(), Codex(session: 31), settings)));
        Assert.False(before.SameAs(Compose(Claude(session: 13), Codex(session: 30), settings)));
        Assert.False(before.SameAs(Compose(Claude(), Codex(), Showing(claude: true, codex: false))));
    }
}
