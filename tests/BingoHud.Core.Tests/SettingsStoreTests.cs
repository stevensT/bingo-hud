using System.Text.Json;
using BingoHud.Core.Alerts;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// The things the user can change that have to survive a restart (AC-22): where the HUD sits,
/// whether it collapses, which direction the percentage reads, where the alert lines are, and
/// the two display options added in 0.2.0, reset format and the bar.
///
/// <para>
/// Same posture as <see cref="AlertStateStore"/>: plain JSON a person can read and hand-edit,
/// enums by name, and a file that cannot be read means defaults rather than a crash. One thing
/// is stricter here. A key missing from the file keeps its default without resetting the rest,
/// because the day a version adds a setting, every existing file is missing that key, and
/// throwing the user's position away over it would be the app forgetting on every upgrade.
/// </para>
/// <para>
/// The thresholds are the one value validated on the way in. A hand-edited file that puts
/// critical above warning, or either outside 0 to 100, would not crash anything — it would
/// silently make alerts fire wrong or never, which is worse.
/// </para>
/// </summary>
public class SettingsStoreTests
{
    private static readonly UserSettings Changed = new(
        Position: new HudPosition(Left: -120.5, Top: 42),
        Collapse: true,
        Direction: DisplayDirection.Remaining,
        Thresholds: new Thresholds(WarningAtRemaining: 40, CriticalAtRemaining: 15),
        ResetFormat: ResetFormat.Countdown,
        ShowBar: true);

    [Fact]
    public void NoFileYetLoadsTheDefaults()
    {
        // Pins the spec's defaults: not yet placed, both windows shown (AC-7), consumed (AC-2a),
        // 25 and 10 remaining.
        using var directory = new TempDirectory();

        var loaded = new SettingsStore(directory.PathTo("settings.json")).Load();

        Assert.Equal(UserSettings.Default, loaded);
        Assert.Null(loaded.Position);
        Assert.False(loaded.Collapse);
        Assert.Equal(DisplayDirection.Consumed, loaded.Direction);
        Assert.Equal(Thresholds.Default, loaded.Thresholds);
        Assert.Equal(ResetFormat.ClockTime, loaded.ResetFormat);
        Assert.False(loaded.ShowBar);
    }

    [Fact]
    public void SavedSettingsComeBackAfterARestart()
    {
        using var directory = new TempDirectory();
        var path = directory.PathTo("settings.json");

        Assert.True(new SettingsStore(path).Save(Changed));

        Assert.Equal(Changed, new SettingsStore(path).Load());
    }

    [Fact]
    public void AKeyMissingFromTheFileKeepsItsDefaultWithoutResettingTheOthers()
    {
        // The upgrade case: a file written before a setting existed.
        using var directory = new TempDirectory();
        var path = directory.WriteFile("settings.json", """{ "collapse": true }""");

        var loaded = new SettingsStore(path).Load();

        Assert.True(loaded.Collapse);
        Assert.Null(loaded.Position);
        Assert.Equal(DisplayDirection.Consumed, loaded.Direction);
        Assert.Equal(Thresholds.Default, loaded.Thresholds);
    }

    [Fact]
    public void AFileWithoutACollapseKeyIsNotCollapsed()
    {
        // The mirror of the test above, so that each default is pinned by a file that lacks it.
        using var directory = new TempDirectory();
        var path = directory.WriteFile("settings.json", """{ "direction": "Remaining" }""");

        var loaded = new SettingsStore(path).Load();

        Assert.False(loaded.Collapse);
        Assert.Equal(DisplayDirection.Remaining, loaded.Direction);
    }

    [Fact]
    public void AFileWithoutTheDisplayOptionKeysDrawsTheOriginalHud()
    {
        // Every file written before 0.2.0 lacks both keys, and must come back as clock time with
        // no bar: an upgrade changes nothing on screen until the user asks (AC-16).
        using var directory = new TempDirectory();
        var path = directory.WriteFile("settings.json", """{ "collapse": true }""");

        var loaded = new SettingsStore(path).Load();

        Assert.Equal(ResetFormat.ClockTime, loaded.ResetFormat);
        Assert.False(loaded.ShowBar);
    }

    [Fact]
    public void AFileWrittenByVersionOhPointOneLoadsWithEverythingItHeld()
    {
        // Byte for byte what 0.1.0's Save wrote, every key it knew present. The upgrade must keep
        // the user's position, collapse, direction and thresholds, and add the new two at their
        // defaults (AC-14, AC-16).
        using var directory = new TempDirectory();
        var path = directory.WriteFile("settings.json", """
            {
              "position": {
                "left": -120.5,
                "top": 42
              },
              "collapse": true,
              "direction": "Remaining",
              "thresholds": {
                "warningAtRemaining": 40,
                "criticalAtRemaining": 15
              }
            }
            """);

        var loaded = new SettingsStore(path).Load();

        Assert.Equal(Changed with { ResetFormat = ResetFormat.ClockTime, ShowBar = false }, loaded);
    }

    [Fact]
    public void TheDirectionIsWrittenByNameSoItCannotBeRenumbered()
    {
        using var directory = new TempDirectory();
        var path = directory.PathTo("settings.json");

        new SettingsStore(path).Save(Changed);

        var written = File.ReadAllText(path);
        Assert.Contains("Remaining", written);
        Assert.Contains("Countdown", written);
        using var document = JsonDocument.Parse(written);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("""{ "direction": "Sideways" }""")]
    [InlineData("""{ "collapse": true, "resetFormat": "Sundial" }""")]
    public void AFileThatCannotBeUnderstoodLoadsTheDefaults(string content)
    {
        using var directory = new TempDirectory();
        var path = directory.WriteFile("settings.json", content);

        Assert.Equal(UserSettings.Default, new SettingsStore(path).Load());
    }

    [Theory]
    [InlineData("""{ "warningAtRemaining": 10, "criticalAtRemaining": 25 }""")]
    [InlineData("""{ "warningAtRemaining": 120, "criticalAtRemaining": 10 }""")]
    [InlineData("""{ "warningAtRemaining": 25, "criticalAtRemaining": -5 }""")]
    [InlineData("""{ "warningAtRemaining": 25 }""")]
    [InlineData("null")]
    public void ThresholdsThatMakeNoSenseFallBackToTheDefaultsAlone(string thresholds)
    {
        // The rest of the file is still honoured; only the bad value is replaced.
        using var directory = new TempDirectory();
        var path = directory.WriteFile(
            "settings.json",
            $$"""{ "collapse": true, "thresholds": {{thresholds}} }""");

        var loaded = new SettingsStore(path).Load();

        Assert.Equal(Thresholds.Default, loaded.Thresholds);
        Assert.True(loaded.Collapse);
    }

    [Fact]
    public void EqualWarningAndCriticalLinesAreAllowed()
    {
        // One line, not two, is a legitimate choice — it is not the same as nonsense.
        using var directory = new TempDirectory();
        var path = directory.PathTo("settings.json");
        var oneLine = Changed with { Thresholds = new Thresholds(20, 20) };

        new SettingsStore(path).Save(oneLine);

        Assert.Equal(oneLine.Thresholds, new SettingsStore(path).Load().Thresholds);
    }

    [Fact]
    public void SavingCreatesTheDirectoryOnFirstRun()
    {
        // The default location is a folder of Bingo's own that does not exist until Bingo
        // makes it.
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.PathTo("Bingo"), "settings.json");

        Assert.True(new SettingsStore(path).Save(Changed));

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void ANonFinitePositionIsSavedAsNotYetPlaced()
    {
        // A WPF window reports NaN for its position until it has been shown and placed. Saving
        // that must not fail, and must not come back as a position either.
        using var directory = new TempDirectory();
        var path = directory.PathTo("settings.json");
        var unplaced = Changed with { Position = new HudPosition(double.NaN, double.NaN) };

        Assert.True(new SettingsStore(path).Save(unplaced));

        Assert.Null(new SettingsStore(path).Load().Position);
    }

    [Fact]
    public void AStoreThatCannotWriteSaysSoInsteadOfThrowing()
    {
        // A file sitting where the directory should be is the cheapest unwritable path there is.
        using var directory = new TempDirectory();
        var blocker = directory.WriteFile("Bingo", "");
        var path = Path.Combine(blocker, "settings.json");

        Assert.False(new SettingsStore(path).Save(Changed));
    }

    [Fact]
    public void AppStateLivesInOneFolderUnderLocalAppData()
    {
        // Local rather than roaming: the HUD position is per-machine, and it is one file.
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var expected = Path.Combine(local, "Bingo");

        Assert.Equal(expected, AppData.Directory);
        Assert.Equal(Path.Combine(expected, "settings.json"), SettingsStore.DefaultPath);
        Assert.Equal(Path.Combine(expected, "alerts.json"), AlertStateStore.DefaultPath);
    }
}
