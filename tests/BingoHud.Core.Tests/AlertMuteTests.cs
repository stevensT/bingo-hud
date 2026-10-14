using BingoHud.Core.Alerts;
using BingoHud.Core.Time;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Tests;

/// <summary>
/// Silencing the window you already know about.
///
/// <para>
/// Muting is not a new kind of state. "Do not tell me about this window again" and "you have
/// already told me about this window" are the same instruction to the same record, so a mute is
/// simply every threshold for that occurrence marked as fired. That is why muting survives a
/// restart and lifts on reset without either behaviour being written twice — both already belong
/// to <see cref="AlertKey"/>.
/// </para>
/// <para>
/// It follows that a mute is per window and lasts until that window resets. There is no
/// indefinite mute, because a quota tool that can be silenced permanently is a quota tool that
/// will be silent on the day it matters.
/// </para>
/// </summary>
public class AlertMuteTests
{
    private static readonly DateTimeOffset ObservedAt =
        new(2026, 8, 31, 10, 9, 23, TimeSpan.FromHours(-7));

    private static readonly DateTimeOffset Reset =
        new(2026, 8, 31, 16, 0, 0, TimeSpan.FromHours(-7));

    private static QuotaWindow Window(
        double usedPercent,
        WindowKind kind = WindowKind.Session,
        DateTimeOffset? resetsAt = null) =>
        new(kind, usedPercent, resetsAt ?? Reset, ServerSeverity.Normal);

    private static IReadOnlyList<Alert> Take(AlertEngine engine, params QuotaWindow[] windows) =>
        engine.TakeNewAlerts(new QuotaSnapshot(windows, ObservedAt, "{}"), Thresholds.Default);

    [Fact]
    public void AMutedWindowRaisesNothingItWouldOtherwiseHaveRaised()
    {
        var engine = new AlertEngine(new InMemoryAlertStateStore());

        engine.Mute(Window(usedPercent: 50), Thresholds.Default);

        Assert.Empty(Take(engine, Window(usedPercent: 95)));
    }

    [Fact]
    public void MutingSilencesTheCriticalLineToNotJustTheOneAlreadyCrossed()
    {
        // Muted at the warning line, the user must not then be shouted at by the critical one.
        var engine = new AlertEngine(new InMemoryAlertStateStore());
        Assert.Single(Take(engine, Window(usedPercent: 80)));

        engine.Mute(Window(usedPercent: 80), Thresholds.Default);

        Assert.Empty(Take(engine, Window(usedPercent: 99)));
    }

    [Fact]
    public void MutingOneWindowLeavesTheOtherAlone()
    {
        var engine = new AlertEngine(new InMemoryAlertStateStore());

        engine.Mute(Window(usedPercent: 95, kind: WindowKind.Session), Thresholds.Default);

        var alert = Assert.Single(Take(
            engine,
            Window(usedPercent: 95, kind: WindowKind.Session),
            Window(usedPercent: 95, kind: WindowKind.WeeklyAll)));

        Assert.Equal(WindowKind.WeeklyAll, alert.Key.Kind);
    }

    [Fact]
    public void TheMuteLiftsWhenTheWindowResets()
    {
        var engine = new AlertEngine(new InMemoryAlertStateStore());
        engine.Mute(Window(usedPercent: 95), Thresholds.Default);

        var nextOccurrence = Window(usedPercent: 95, resetsAt: Reset.AddHours(5));

        Assert.Single(Take(engine, nextOccurrence));
    }

    [Fact]
    public void AMuteSurvivesARestartWithinTheSameWindow()
    {
        using var directory = new TempDirectory();
        var path = directory.PathTo("alerts.json");
        var clock = new TestClock(ObservedAt);

        new AlertEngine(new AlertStateStore(path, clock)).Mute(Window(95), Thresholds.Default);

        var afterRestart = new AlertEngine(new AlertStateStore(path, clock));

        Assert.Empty(Take(afterRestart, Window(usedPercent: 95)));
    }

    [Fact]
    public void MutingAWindowWithNoResetTimeIsHarmless()
    {
        // It has no occurrence to mute, and 5.1 already means it never alerts at all.
        var engine = new AlertEngine(new InMemoryAlertStateStore());
        var window = new QuotaWindow(
            WindowKind.Session, UsedPercent: 99, ResetsAt: null, ServerSeverity.Normal);

        engine.Mute(window, Thresholds.Default);

        Assert.Empty(Take(engine, window));
    }

    [Fact]
    public async Task MutingWhileAReadingIsBeingEvaluatedWaitsForItToFinish()
    {
        // The poll loop evaluates off the UI thread and the tray mutes on it, against one store.
        // Unguarded, a mute could add to the set while a save was serializing it, and the
        // collection-modified exception that follows is not a file failure the store forgives.
        // The store here holds evaluation inside its first write, so overlap shows up as state
        // rather than depending on a lucky interleaving.
        //
        // Not overlapping is not enough. Evaluation is a check, a record, and another check, and
        // a mute landing between them is the race the lock exists for, so the store's call log
        // must show evaluation's calls as one unbroken run. Evaluation works on the session
        // window and the mute on the weekly one, which is how the log tells them apart.
        var store = new HoldingAlertStateStore();
        var engine = new AlertEngine(store);

        var evaluating = Task.Run(() => engine.TakeNewAlerts(
            new QuotaSnapshot([Window(usedPercent: 95)], ObservedAt, "{}"), Thresholds.Default));

        Assert.True(await store.FirstWriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(5)));

        var muteCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var muting = Task.Run(() =>
        {
            muteCalled.SetResult();
            engine.Mute(Window(usedPercent: 95, kind: WindowKind.WeeklyAll), Thresholds.Default);
        });

        // Measured from the moment Mute is called rather than from Task.Run, so a slow thread
        // pool cannot use up the window and let an unguarded mute through unseen. Long enough
        // for an unguarded mute to reach the store while evaluation is held there.
        await muteCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.WhenAny(muting, Task.Delay(TimeSpan.FromMilliseconds(200)));
        store.Release();

        await Task.WhenAll(evaluating, muting).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(store.Overlapped);
        Assert.True(store.HasFired(new AlertKey(WindowKind.WeeklyAll, 10, Reset)));

        var calls = store.Calls;
        var first = calls.IndexOf(WindowKind.Session);
        var last = calls.LastIndexOf(WindowKind.Session);
        Assert.All(calls.Skip(first).Take(last - first + 1), kind => Assert.Equal(WindowKind.Session, kind));
    }

    /// <summary>
    /// A store that notices two callers inside it at once, logs which window each call was
    /// about in order, and holds its first write open until released.
    /// </summary>
    private sealed class HoldingAlertStateStore : IAlertStateStore
    {
        private readonly HashSet<AlertKey> _fired = [];
        private readonly List<WindowKind> _calls = [];
        private readonly ManualResetEventSlim _release = new();
        private int _inside;
        private int _writes;

        public TaskCompletionSource<bool> FirstWriteEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Overlapped { get; private set; }

        public List<WindowKind> Calls
        {
            get
            {
                lock (_calls)
                {
                    return [.. _calls];
                }
            }
        }

        public void Release() => _release.Set();

        public bool HasFired(AlertKey key)
        {
            Enter(key);
            try
            {
                lock (_fired)
                {
                    return _fired.Contains(key);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _inside);
            }
        }

        public void MarkFired(AlertKey key)
        {
            Enter(key);
            try
            {
                if (Interlocked.Increment(ref _writes) == 1)
                {
                    FirstWriteEntered.SetResult(true);
                    _release.Wait(TimeSpan.FromSeconds(5));
                }

                lock (_fired)
                {
                    _fired.Add(key);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _inside);
            }
        }

        private void Enter(AlertKey key)
        {
            lock (_calls)
            {
                _calls.Add(key.Kind);
            }

            if (Interlocked.Increment(ref _inside) > 1)
            {
                Overlapped = true;
            }
        }
    }

    [Fact]
    public void MutingAPerModelCapRecordsNothing()
    {
        // Per-model caps never alert, so there is nothing of theirs to silence. Recording keys
        // for them would only fill the state file with entries nothing ever reads.
        var store = new InMemoryAlertStateStore();
        var scoped = new QuotaWindow(
            WindowKind.WeeklyScoped, UsedPercent: 95, Reset, ServerSeverity.Normal, Scope: "opus");

        new AlertEngine(store).Mute(scoped, Thresholds.Default);

        Assert.Equal(0, store.Count);
    }
}
