using BingoHud.Core.Settings;

namespace BingoHud.Core.Tests;

/// <summary>
/// Which products are shown, and remembering that the user was asked (codex-quota AC-8, AC-8a).
///
/// <para>
/// The setting has three meanings, not two. Null is "never asked", and it is what puts the
/// first-start question on screen; it must survive loading a file from a version that had no such
/// setting, or the upgrade would skip the question. Once answered, at least one product is always
/// on, because a HUD showing nothing at all looks the same as one that has crashed.
/// </para>
/// </summary>
public class ProductSettingsTests
{
    private static SettingsStore Store(TempDirectory directory) => new(directory.PathTo("settings.json"));

    // --- The switches

    [Fact]
    public void EachProductReadsItsOwnSwitch()
    {
        var switches = new ProductSwitches(Claude: true, Codex: false);

        Assert.True(switches.IsOn(Product.Claude));
        Assert.False(switches.IsOn(Product.Codex));
    }

    [Fact]
    public void AProductCanBeTurnedOnAndOff()
    {
        var both = new ProductSwitches(Claude: true, Codex: false).With(Product.Codex, on: true);
        Assert.Equal(new ProductSwitches(true, true), both);

        Assert.Equal(new ProductSwitches(true, false), both.With(Product.Codex, on: false));
    }

    [Fact]
    public void TheLastProductOnCannotBeTurnedOff()
    {
        var claudeOnly = new ProductSwitches(Claude: true, Codex: false);

        Assert.False(claudeOnly.CanTurnOff(Product.Claude));
        Assert.Equal(claudeOnly, claudeOnly.With(Product.Claude, on: false));
    }

    [Fact]
    public void EitherCanBeTurnedOffWhileBothAreOn()
    {
        var both = new ProductSwitches(true, true);

        Assert.True(both.CanTurnOff(Product.Claude));
        Assert.True(both.CanTurnOff(Product.Codex));
    }

    [Fact]
    public void AnyOnIsFalseOnlyWithEverythingOff()
    {
        Assert.True(new ProductSwitches(true, false).AnyOn);
        Assert.True(new ProductSwitches(false, true).AnyOn);
        Assert.False(new ProductSwitches(false, false).AnyOn);
    }

    // --- The settings file

    [Fact]
    public void TheDefaultIsNotYetAsked()
    {
        Assert.Null(UserSettings.Default.Products);
    }

    [Fact]
    public void NoSettingsFileIsNotYetAsked()
    {
        using var directory = new TempDirectory();

        Assert.Null(Store(directory).Load().Products);
    }

    [Fact]
    public void AFileFrom040HasNoAnswerAndKeepsEverythingElse()
    {
        // The first start after an upgrade asks the question, and must not lose the HUD's place
        // or the user's other choices in the process.
        using var directory = new TempDirectory();
        directory.WriteFile("settings.json", """
            {
              "position": { "left": 120, "top": 40 },
              "collapse": true,
              "direction": "Remaining",
              "thresholds": { "warningAtRemaining": 25, "criticalAtRemaining": 10 },
              "resetFormat": "Countdown",
              "showBar": true
            }
            """);

        var loaded = Store(directory).Load();

        Assert.Null(loaded.Products);
        Assert.Equal(new HudPosition(120, 40), loaded.Position);
        Assert.True(loaded.Collapse);
        Assert.Equal(DisplayDirection.Remaining, loaded.Direction);
        Assert.True(loaded.ShowBar);
    }

    [Fact]
    public void AnAnswerIsSavedAndReadBack()
    {
        using var directory = new TempDirectory();
        var store = Store(directory);

        store.Save(UserSettings.Default with { Products = new ProductSwitches(Claude: false, Codex: true) });

        Assert.Equal(new ProductSwitches(false, true), store.Load().Products);
    }

    [Fact]
    public void TheSavedAnswerHoldsTheTwoSwitchesAndNothingWorkedOutFromThem()
    {
        // Found on screen at task 5.1: "anyOn" was written beside the switches. A file holds what
        // the user chose; anything derived from it would be a second copy that could disagree.
        using var directory = new TempDirectory();
        var path = directory.PathTo("settings.json");
        new SettingsStore(path).Save(UserSettings.Default with { Products = new ProductSwitches(true, false) });

        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var products = document.RootElement.GetProperty("products");

        Assert.Equal(["claude", "codex"], products.EnumerateObject().Select(p => p.Name));
        Assert.False(document.RootElement.TryGetProperty("shown", out _));
    }

    [Fact]
    public void AnAnswerWithNothingOnIsReadAsNotYetAsked()
    {
        // Only a hand edit can produce it. Asking again is the one way back to a HUD that shows
        // something.
        using var directory = new TempDirectory();
        directory.WriteFile("settings.json", """{ "products": { "claude": false, "codex": false } }""");

        Assert.Null(Store(directory).Load().Products);
    }

    [Fact]
    public void AnAnswerMissingAProductReadsItAsOff()
    {
        // A hand edit again. The answer was given, so it is honoured for the product it names,
        // as long as that leaves something on.
        using var directory = new TempDirectory();
        directory.WriteFile("settings.json", """{ "products": { "codex": true } }""");

        Assert.Equal(new ProductSwitches(Claude: false, Codex: true), Store(directory).Load().Products);
    }
}
