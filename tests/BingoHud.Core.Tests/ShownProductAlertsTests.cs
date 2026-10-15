using BingoHud.Core.Alerts;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// A product switched off raises no alerts and gives Mute nothing to act on (codex-quota spec,
/// Decided, 2026-10-15). The user who unticks a product has said they do not want to see it, and a
/// notification about it is seeing it.
/// </summary>
public class ShownProductAlertsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private static Alert For(Product product) =>
        new(new AlertKey(WindowKind.Session, 25, Now.AddHours(1), product), Severity.Warning, 80);

    private static QuotaSnapshot Snapshot() => new(
        [new QuotaWindow(WindowKind.Session, 80, Now.AddHours(1), ServerSeverity.Unknown)],
        Now,
        "{}");

    private static UserSettings With(ProductSwitches? products) => UserSettings.Default with { Products = products };

    // --- Which products are shown

    [Fact]
    public void TheAnsweredSwitchesAreWhatIsShown()
    {
        var both = new ProductSwitches(Claude: true, Codex: true);

        Assert.Equal(both, With(both).Shown);
    }

    [Fact]
    public void BeforeTheQuestionIsAnsweredClaudeAloneIsShown()
    {
        // As 0.4.0 showed it; the question comes before the HUD in any case.
        Assert.Equal(new ProductSwitches(Claude: true, Codex: false), With(null).Shown);
    }

    [Fact]
    public void SwitchesWithNothingOnShowClaudeRatherThanNothing()
    {
        // The store refuses such a file, but a value built in code is not read from a file. A HUD
        // with nothing on looks the same as one that has crashed.
        Assert.Equal(new ProductSwitches(Claude: true, Codex: false), With(new ProductSwitches(false, false)).Shown);
    }

    // --- Alerts

    [Fact]
    public void AlertsForAProductSwitchedOffAreDropped()
    {
        var claude = For(Product.Claude);

        var shown = ProductAlerts.Shown([claude, For(Product.Codex)], new ProductSwitches(Claude: true, Codex: false));

        Assert.Equal([claude], shown);
    }

    [Fact]
    public void AlertsForEveryProductShownAreKeptInOrder()
    {
        var codex = For(Product.Codex);
        var claude = For(Product.Claude);

        Assert.Equal([codex, claude], ProductAlerts.Shown([codex, claude], new ProductSwitches(true, true)));
    }

    // --- Mute

    [Fact]
    public void MuteActsOnlyOnTheReadingsOfProductsShown()
    {
        var claude = Snapshot();
        var codex = Snapshot();

        var targets = ProductAlerts.Mutable(new ProductSwitches(Claude: false, Codex: true), claude, codex);

        var only = Assert.Single(targets);
        Assert.Equal(Product.Codex, only.Product);
        Assert.Same(codex, only.Snapshot);
    }

    [Fact]
    public void AHiddenReadingAloneLeavesNothingToMute()
    {
        // So "Mute alerts until reset" is greyed out rather than offering to silence alerts that
        // would never be shown.
        Assert.Empty(ProductAlerts.Mutable(new ProductSwitches(Claude: true, Codex: false), claude: null, codex: Snapshot()));
    }
}
