using System.Text.Json;
using BingoHud.Core.Alerts;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// The part of alerting that has to survive the process ending.
///
/// <para>
/// Without this, closing Bingo rearms every alert, and a restart mid-window re-announces
/// something the user was told an hour ago. The file is small and holds nothing secret — a list
/// of which alerts have already fired — so it is written as plain JSON with the window kind
/// spelled out, on purpose: state written as an enum's ordinal would silently change meaning the
/// day a new window kind is added in the middle of the list.
/// </para>
/// <para>
/// Nothing here is important enough to fail the app over. A file that cannot be read or written
/// costs at worst a duplicate notification, so every failure degrades to in-memory behaviour
/// rather than throwing into the poll loop.
/// </para>
/// </summary>
public class AlertStateStoreTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 31, 10, 9, 23, TimeSpan.FromHours(-7));

    private static readonly DateTimeOffset Reset =
        new(2026, 8, 31, 16, 0, 0, TimeSpan.FromHours(-7));

    private static AlertKey Key(
        int thresholdPercent = 10,
        WindowKind kind = WindowKind.Session,
        DateTimeOffset? resetsAt = null) =>
        new(kind, thresholdPercent, resetsAt ?? Reset);

    private static AlertStateStore Store(string path, DateTimeOffset? now = null) =>
        new(path, new TestClock(now ?? Now));

    [Fact]
    public void AStoreWithNoFileYetHasFiredNothing()
    {
        using var directory = new TempDirectory();

        Assert.False(Store(directory.PathTo("alerts.json")).HasFired(Key()));
    }

    [Fact]
    public void AMarkedAlertIsRememberedWithinTheSession()
    {
        using var directory = new TempDirectory();
        var store = Store(directory.PathTo("alerts.json"));

        store.MarkFired(Key());

        Assert.True(store.HasFired(Key()));
        Assert.False(store.HasFired(Key(thresholdPercent: 25)));
    }

    [Fact]
    public void AMarkedAlertSurvivesARestartWithinTheSameWindow()
    {
        using var directory = new TempDirectory();
        var path = directory.PathTo("alerts.json");

        Store(path).MarkFired(Key());

        Assert.True(Store(path).HasFired(Key()));
    }

    [Fact]
    public void TheWindowKindIsWrittenByNameSoItCannotBeRenumbered()
    {
        using var directory = new TempDirectory();
        var path = directory.PathTo("alerts.json");

        Store(path).MarkFired(Key(kind: WindowKind.WeeklyAll));

        var written = File.ReadAllText(path);

        Assert.Contains("WeeklyAll", written);
        using var document = JsonDocument.Parse(written);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
    }

    [Fact]
    public void TheSameInstantInADifferentOffsetIsStillTheSameAlertAfterARestart()
    {
        using var directory = new TempDirectory();
        var path = directory.PathTo("alerts.json");

        Store(path).MarkFired(Key(resetsAt: Reset));

        Assert.True(Store(path).HasFired(Key(resetsAt: Reset.ToUniversalTime())));
    }

    [Fact]
    public void LoadingPrunesWindowsThatResetWhileTheAppWasClosed()
    {
        // This is what keeps the file bounded without anything owning a timer: every start drops
        // what can no longer matter, and a window that has reset can never be alerted about
        // again under the same key.
        using var directory = new TempDirectory();
        var path = directory.PathTo("alerts.json");
        Store(path).MarkFired(Key());

        var afterTheWindowClosed = Store(path, now: Reset.AddHours(1));

        Assert.False(afterTheWindowClosed.HasFired(Key()));
    }

    [Fact]
    public void AnAlertIsStillRememberedRightUpToItsResetInstant()
    {
        using var directory = new TempDirectory();
        var path = directory.PathTo("alerts.json");
        Store(path).MarkFired(Key());

        Assert.True(Store(path, now: Reset).HasFired(Key()));
    }

    [Fact]
    public void AFileThatCannotBeParsedIsTreatedAsNoStateRatherThanThrowing()
    {
        using var directory = new TempDirectory();
        var path = directory.WriteFile("alerts.json", "{ this is not json");

        var store = Store(path);

        Assert.False(store.HasFired(Key()));
        store.MarkFired(Key());
        Assert.True(store.HasFired(Key()));
    }

    [Fact]
    public void AFileInAnUnexpectedShapeIsTreatedAsNoState()
    {
        using var directory = new TempDirectory();
        var path = directory.WriteFile("alerts.json", "[1, 2, 3]");

        Assert.False(Store(path).HasFired(Key()));
    }

    [Fact]
    public void AStoreThatCannotWriteStillDeduplicatesForTheRestOfTheSession()
    {
        // An unwritable path costs restart-survival, not correctness. Losing a duplicate
        // notification is not worth throwing into the polling path over.
        using var directory = new TempDirectory();
        var unwritable = Path.Combine(directory.PathTo("no-such-directory"), "alerts.json");
        var store = Store(unwritable);

        store.MarkFired(Key());

        Assert.True(store.HasFired(Key()));
        Assert.False(File.Exists(unwritable));
    }
}
