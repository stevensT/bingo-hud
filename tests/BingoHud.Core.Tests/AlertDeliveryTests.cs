using BingoHud.Core.Alerts;
using BingoHud.Core.Display;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

public class AlertDeliveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static Alert For(Product product, WindowKind kind = WindowKind.Session) =>
        new(new AlertKey(kind, 25, Now.AddHours(1), product), Severity.Warning, 80);

    [Fact]
    public void TheFirstBatchIsReadyImmediately()
    {
        var alert = For(Product.Claude);

        Assert.Equal([alert], new AlertDelivery(new TestClock(Now)).Add([alert]));
    }

    [Fact]
    public void AnotherProductWaitsUntilTheVisibleNotificationEnds()
    {
        var delivery = new AlertDelivery(new TestClock(Now));
        var claude = For(Product.Claude);
        var codex = For(Product.Codex);

        Assert.Equal([claude], delivery.Add([claude]));
        Assert.Empty(delivery.Add([codex]));
        Assert.Equal([codex], delivery.Closed());
        Assert.Empty(delivery.Closed());
    }

    [Fact]
    public void WaitingCrossingsShareTheNextNotificationAndKeepTheirProductNames()
    {
        var delivery = new AlertDelivery(new TestClock(Now));
        delivery.Add([For(Product.Claude)]);
        var codex = For(Product.Codex);
        var weekly = For(Product.Claude, WindowKind.WeeklyAll);

        Assert.Empty(delivery.Add([codex]));
        Assert.Empty(delivery.Add([weekly]));
        var next = delivery.Closed();

        Assert.Equal([codex, weekly], next);
        var message = AlertMessage.Describe(next, DisplayDirection.Consumed, Now);
        Assert.NotNull(message);
        Assert.Contains("Codex 5h", message.Body);
        Assert.Contains("Claude Week", message.Body);
        Assert.Empty(delivery.Closed());
    }

    [Fact]
    public void AnEmptyBatchDoesNotBlockTheNextNotification()
    {
        var delivery = new AlertDelivery(new TestClock(Now));
        var alert = For(Product.Codex);

        Assert.Empty(delivery.Add([]));
        Assert.Empty(delivery.Closed());
        Assert.Equal([alert], delivery.Add([alert]));
    }

    [Fact]
    public void AfterTheLastNotificationEndsANewBatchIsReadyImmediately()
    {
        var delivery = new AlertDelivery(new TestClock(Now));
        delivery.Add([For(Product.Claude)]);
        Assert.Empty(delivery.Closed());
        var next = For(Product.Codex);

        Assert.Equal([next], delivery.Add([next]));
    }
    [Fact]
    public void MutingDiscardsWaitingCrossingsWithoutBlockingTheNextOccurrence()
    {
        var delivery = new AlertDelivery(new TestClock(Now));
        delivery.Add([For(Product.Claude)]);
        delivery.Add([For(Product.Codex)]);

        delivery.Clear();

        Assert.Empty(delivery.Closed());
        var nextOccurrence = For(Product.Codex) with
        {
            Key = new AlertKey(WindowKind.Session, 25, Now.AddHours(6), Product.Codex),
        };
        Assert.Equal([nextOccurrence], delivery.Add([nextOccurrence]));
    }

    [Fact]
    public void ANotificationThatNeverReportsEndingStopsHoldingTheQueueAfterTheLimit()
    {
        // Windows does not promise to say when a notification ends: one that was suppressed, or
        // went straight to the notification centre, may never report closing. Without a limit,
        // every later alert would wait for the rest of the session.
        var clock = new TestClock(Now);
        var delivery = new AlertDelivery(clock);
        delivery.Add([For(Product.Claude)]);
        var codex = For(Product.Codex);

        clock.Advance(AlertDelivery.ShowingLimit - TimeSpan.FromSeconds(1));
        Assert.Empty(delivery.Add([codex]));

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal([codex], delivery.Add([]));
    }

    [Fact]
    public void TheLimitStartsAgainWithEachNotificationShown()
    {
        var clock = new TestClock(Now);
        var delivery = new AlertDelivery(clock);
        delivery.Add([For(Product.Claude)]);
        clock.Advance(AlertDelivery.ShowingLimit);
        delivery.Add([For(Product.Codex)]);
        var weekly = For(Product.Claude, WindowKind.WeeklyAll);

        clock.Advance(AlertDelivery.ShowingLimit - TimeSpan.FromSeconds(1));

        Assert.Empty(delivery.Add([weekly]));
    }
}
