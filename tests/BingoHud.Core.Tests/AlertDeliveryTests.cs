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

        Assert.Equal([alert], new AlertDelivery().Add([alert]));
    }

    [Fact]
    public void AnotherProductWaitsUntilTheVisibleNotificationEnds()
    {
        var delivery = new AlertDelivery();
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
        var delivery = new AlertDelivery();
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
        var delivery = new AlertDelivery();
        var alert = For(Product.Codex);

        Assert.Empty(delivery.Add([]));
        Assert.Empty(delivery.Closed());
        Assert.Equal([alert], delivery.Add([alert]));
    }

    [Fact]
    public void AfterTheLastNotificationEndsANewBatchIsReadyImmediately()
    {
        var delivery = new AlertDelivery();
        delivery.Add([For(Product.Claude)]);
        Assert.Empty(delivery.Closed());
        var next = For(Product.Codex);

        Assert.Equal([next], delivery.Add([next]));
    }
    [Fact]
    public void MutingDiscardsWaitingCrossingsWithoutBlockingTheNextOccurrence()
    {
        var delivery = new AlertDelivery();
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
}
