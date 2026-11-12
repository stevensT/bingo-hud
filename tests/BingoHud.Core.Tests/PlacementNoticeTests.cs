using BingoHud.Core.Codex;
using BingoHud.Core.Display;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// The detail panel's line for a HUD that is not where the user left it, because that spot is on
/// no connected monitor (late-monitor-placement AC-6). It shows only while that is true, says
/// that nothing needs doing, and belongs to the app rather than to either product.
/// </summary>
public class PlacementNoticeTests
{
    private static PanelContent Panel(bool claude, bool codex, string? notice) =>
        PanelReadout.ComposeAll(
            new ReadingState(null, Freshness.Fresh, null, TimeSpan.Zero, "not started"),
            new CodexState(CodexPresence.NotFound, null, null, TimeSpan.Zero, null, []),
            UserSettings.Default with { Products = new ProductSwitches(claude, codex) },
            "0.6.1", DateTimeOffset.UnixEpoch, placementNotice: notice);

    [Fact]
    public void AnAwayHudSaysWhyAndThatItWillGoBack()
    {
        Assert.Equal(
            "The HUD is not where you left it: that spot is on a monitor that is not connected. "
            + "It will go back when the monitor connects.",
            PlacementNotice.Describe(away: true));
    }

    [Fact]
    public void AHudAtItsSavedSpotHasNoLine()
    {
        Assert.Null(PlacementNotice.Describe(away: false));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void TheLineBelongsToTheAppWhicheverProductsAreShown(bool claude, bool codex)
    {
        Assert.Equal("Away", Panel(claude, codex, "Away").PlacementNotice);
    }

    [Fact]
    public void TheLineIsIndependentOfTheSettingsNotice()
    {
        var both = PanelReadout.ComposeAll(
            new ReadingState(null, Freshness.Fresh, null, TimeSpan.Zero, "not started"),
            new CodexState(CodexPresence.NotFound, null, null, TimeSpan.Zero, null, []),
            UserSettings.Default,
            "0.6.1", DateTimeOffset.UnixEpoch, settingsNotice: "Save failed", placementNotice: "Away");

        Assert.Equal("Save failed", both.SettingsNotice);
        Assert.Equal("Away", both.PlacementNotice);
    }

    [Fact]
    public void ALineAppearingOrClearingRequiresAPanelRedraw()
    {
        var before = Panel(true, true, null);
        var away = before with { PlacementNotice = "Away" };

        Assert.False(before.SameAs(away));
        Assert.False(away.SameAs(before));
        Assert.True(away.SameAs(away with { }));
    }
}
