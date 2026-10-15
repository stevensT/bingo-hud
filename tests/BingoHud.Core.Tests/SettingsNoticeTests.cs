using BingoHud.Core.Codex;
using BingoHud.Core.Display;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

public class SettingsNoticeTests
{
    private static PanelContent Panel(bool claude, bool codex, string? notice) =>
        PanelReadout.ComposeAll(
            new ReadingState(null, Freshness.Fresh, null, TimeSpan.Zero, "not started"),
            new CodexState(CodexPresence.NotFound, null, null, TimeSpan.Zero, null, []),
            UserSettings.Default with { Products = new ProductSwitches(claude, codex) },
            "0.5.0", DateTimeOffset.UnixEpoch, settingsNotice: notice);

    [Fact]
    public void AFailedSaveNamesTheFileAndSaysHowLongTheChangeHolds()
    {
        var path = Path.Combine("Bingo", "settings.json");

        Assert.Equal(
            $"Settings could not be saved to {path}. Your changes apply until Bingo closes.",
            SettingsNotice.Describe(saved: false, path));
    }

    [Fact]
    public void TheNextSuccessfulSaveClearsTheNotice()
    {
        var path = Path.Combine("Bingo", "settings.json");
        var notice = SettingsNotice.Describe(saved: false, path);
        Assert.NotNull(notice);

        notice = SettingsNotice.Describe(saved: true, path);
        Assert.Null(notice);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void TheNoticeBelongsToTheAppWhicheverProductsAreShown(bool claude, bool codex)
    {
        Assert.Equal("Save failed", Panel(claude, codex, "Save failed").SettingsNotice);
    }

    [Fact]
    public void ANoticeAppearingOrClearingRequiresAPanelRedraw()
    {
        var before = Panel(false, true, null);
        var failed = before with { SettingsNotice = "Save failed" };

        Assert.False(before.SameAs(failed));
        Assert.False(failed.SameAs(before));
        Assert.True(failed.SameAs(failed with { }));
    }
}
