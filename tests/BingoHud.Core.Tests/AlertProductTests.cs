using BingoHud.Core.Alerts;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// Alerts once there are two products (codex-quota AC-10).
///
/// <para>
/// Two changes to what makes two alerts the same alert. Each now belongs to a product, so a Claude
/// window and a Codex window of the same kind, resetting at the same moment, are still two alerts.
/// And the occurrence is matched within ten minutes rather than to the instant, because Codex
/// reports one window's reset a few seconds differently from one record to the next; matched
/// exactly, a single crossing would be announced again on every record. Two occurrences of one
/// window are at least five hours apart, so ten minutes cannot join them.
/// </para>
/// </summary>
public class AlertProductTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 4, 40, 0, TimeSpan.Zero);

    // The session fixture's two readings of one five-hour window, eight seconds apart.
    private static readonly DateTimeOffset Reset = DateTimeOffset.FromUnixTimeSeconds(1791969796);
    private static readonly DateTimeOffset ResetEightSecondsLater = DateTimeOffset.FromUnixTimeSeconds(1791969804);

    private static AlertKey Key(
        Product product = Product.Claude,
        DateTimeOffset? resetsAt = null,
        double threshold = 10,
        WindowKind kind = WindowKind.Session) =>
        new(kind, threshold, resetsAt ?? Reset, product);

    private static QuotaSnapshot Snapshot(double usedPercent, DateTimeOffset resetsAt) =>
        new([new QuotaWindow(WindowKind.Session, usedPercent, resetsAt, ServerSeverity.Unknown)], Now, "{}");

    // --- What counts as the same alert

    [Fact]
    public void AResetThatMovedByEightSecondsIsTheSameOccurrence()
    {
        Assert.True(Key(Product.Codex).SameOccurrenceAs(Key(Product.Codex, ResetEightSecondsLater)));
    }

    [Fact]
    public void ResetsTenMinutesApartAreStillTheSameOccurrence()
    {
        Assert.True(Key().SameOccurrenceAs(Key(resetsAt: Reset.AddMinutes(10))));
        Assert.True(Key().SameOccurrenceAs(Key(resetsAt: Reset.AddMinutes(-10))));
    }

    [Fact]
    public void ResetsMoreThanTenMinutesApartAreDifferentOccurrences()
    {
        Assert.False(Key().SameOccurrenceAs(Key(resetsAt: Reset.AddMinutes(10).AddSeconds(1))));
    }

    [Fact]
    public void TheSameWindowInTheOtherProductIsADifferentAlert()
    {
        Assert.False(Key(Product.Claude).SameOccurrenceAs(Key(Product.Codex)));
    }

    [Fact]
    public void ADifferentThresholdOrWindowIsADifferentAlert()
    {
        Assert.False(Key().SameOccurrenceAs(Key(threshold: 25)));
        Assert.False(Key().SameOccurrenceAs(Key(kind: WindowKind.WeeklyAll)));
    }

    [Fact]
    public void AKeyForACodexWindowSaysSo()
    {
        var window = new QuotaWindow(WindowKind.Session, 95, Reset, ServerSeverity.Unknown);

        Assert.Equal(Product.Codex, AlertKey.For(window, 10, Product.Codex)!.Product);
        Assert.Equal(Product.Claude, AlertKey.For(window, 10)!.Product);
    }

    // --- The store, across a restart

    [Fact]
    public void AFileWrittenBy040WithNoProductIsClaudes()
    {
        // An upgrade must not re-announce anything 0.4.0 already said, and every alert it recorded
        // was Claude's.
        using var directory = new TempDirectory();
        var path = directory.WriteFile("alerts.json", """
            [
              {
                "kind": "Session",
                "thresholdPercent": 10,
                "resetsAt": "2026-10-14T09:23:16+00:00"
              }
            ]
            """);

        var store = new AlertStateStore(path, new TestClock(Now));

        Assert.True(store.HasFired(Key(Product.Claude)));
        Assert.False(store.HasFired(Key(Product.Codex)));
    }

    [Fact]
    public void TheProductIsWrittenByName()
    {
        using var directory = new TempDirectory();
        var path = directory.PathTo("alerts.json");

        new AlertStateStore(path, new TestClock(Now)).MarkFired(Key(Product.Codex));

        Assert.Contains("\"Codex\"", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void ACodexAlertStillMatchesAfterARestartWhenItsResetHasMoved()
    {
        using var directory = new TempDirectory();
        var path = directory.PathTo("alerts.json");
        new AlertStateStore(path, new TestClock(Now)).MarkFired(Key(Product.Codex));

        var restarted = new AlertStateStore(path, new TestClock(Now));

        Assert.True(restarted.HasFired(Key(Product.Codex, ResetEightSecondsLater)));
    }

    // --- The engine

    [Fact]
    public void TwoCodexReadingsOfOneWindowRaiseOneAlert()
    {
        // The session fixture's case: the same window, read twice, its reset eight seconds apart.
        var engine = new AlertEngine(new AlertStateStore(new TempDirectory().PathTo("alerts.json"), new TestClock(Now)));

        var first = engine.TakeNewAlerts(Snapshot(95, Reset), Thresholds.Default, Product.Codex);
        var second = engine.TakeNewAlerts(Snapshot(96, ResetEightSecondsLater), Thresholds.Default, Product.Codex);

        Assert.Single(first);
        Assert.Empty(second);
    }

    [Fact]
    public void ACodexAlertSaysItIsCodexs()
    {
        var engine = new AlertEngine(new InMemoryAlertStateStore());

        var alert = Assert.Single(engine.TakeNewAlerts(Snapshot(95, Reset), Thresholds.Default, Product.Codex));

        Assert.Equal(Product.Codex, alert.Key.Product);
    }

    [Fact]
    public void ClaudeAndCodexAlertsForTheSameWindowAreIndependent()
    {
        // Both five-hour windows past the critical line, resetting at the same moment: two
        // products, two alerts, and neither one silences the other.
        var engine = new AlertEngine(new InMemoryAlertStateStore());

        Assert.Single(engine.TakeNewAlerts(Snapshot(95, Reset), Thresholds.Default, Product.Claude));
        Assert.Single(engine.TakeNewAlerts(Snapshot(95, Reset), Thresholds.Default, Product.Codex));
    }

    [Fact]
    public void MutingCodexLeavesClaudeArmed()
    {
        var engine = new AlertEngine(new InMemoryAlertStateStore());
        var window = new QuotaWindow(WindowKind.Session, 95, Reset, ServerSeverity.Unknown);

        engine.Mute(window, Thresholds.Default, Product.Codex);

        Assert.Empty(engine.TakeNewAlerts(Snapshot(95, Reset), Thresholds.Default, Product.Codex));
        Assert.Single(engine.TakeNewAlerts(Snapshot(95, Reset), Thresholds.Default, Product.Claude));
    }
}
