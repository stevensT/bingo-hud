using System.Net.Http;
using System.Windows;
using BingoHud.Core.Alerts;
using BingoHud.Core.Credentials;
using BingoHud.Core.Display;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Polling;
using BingoHud.Core.Settings;
using BingoHud.Core.Time;
using BingoHud.Core.Usage;

namespace BingoHud.App;

/// <summary>
/// The composition root. Everything in Core that touches I/O or holds a reading is constructed
/// here and handed to the shell; a pure policy like <see cref="DwellPolicy"/> may be newed
/// where it is used.
/// </summary>
public partial class App : Application
{
    private readonly SystemClock _clock = new();
    private readonly HttpClient _http = new();
    private readonly SettingsStore _settingsStore = new(SettingsStore.DefaultPath);
    private readonly CancellationTokenSource _shutdown = new();

    private UserSettings _settings = UserSettings.Default;
    private QuotaMonitor? _monitor;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settings = _settingsStore.Load();

        // The data path from the plan: credential file, usage endpoint, monitor, and the loop
        // that drives it. The monitor is the only stateful orchestrator; the alert store
        // remembers what has fired but decides nothing. The shell is not told when a reading
        // changes; it re-reads the monitor once a second, which also keeps the reset countdown
        // moving without a second mechanism.
        _monitor = new QuotaMonitor(
            new FileCredentialProvider(FileCredentialProvider.DefaultPath),
            new UsageClient(_http, _clock),
            _clock);

        var transcripts = new TranscriptActivity(TranscriptActivity.DefaultPath, _clock);
        var alerts = new AlertEngine(new AlertStateStore(AlertStateStore.DefaultPath, _clock));

        // deferred: no alert sink is supplied, so the loop evaluates nothing and the engine is
        // never asked. That is deliberate: evaluating now would record each crossing as fired
        // and the toasts 6.10 adds would never show for it.
        var loop = new PollLoop(
            _monitor,
            _clock,
            gatherSignals: () => GatherSignals(transcripts),
            alerts,
            thresholds: () => _settings.Thresholds);

        // Runs until shutdown, or until the endpoint says this account cannot use it; the loop
        // completes normally in both cases, so there is nothing to await. Anything else that
        // ends it is a bug, and a bug that stopped polling silently would leave the last number
        // on screen looking alive. So it is thrown on the UI thread instead, where it ends the
        // process with the cause attached.
        loop.RunAsync(_shutdown.Token).ContinueWith(
            stopped => Dispatcher.InvokeAsync(() => throw new InvalidOperationException(
                "The poll loop stopped unexpectedly.", stopped.Exception)),
            TaskContinuationOptions.OnlyOnFaulted);

        MainWindow = new HudWindow(
            _settings.Position,
            position => Remember(_settings with { Position = position }),
            _clock,
            ReadoutLines);
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // A fetch in flight is abandoned rather than awaited. Nobody will see its reading.
        _shutdown.Cancel();
        _http.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// What the machine and the user are doing, read immediately before each poll.
    /// </summary>
    private static PollSignals GatherSignals(TranscriptActivity transcripts) => new(
        // deferred: battery state needs a Win32 call and the panel does not exist yet. Both sit
        // at their "nothing to react to" values, so two rows of the cadence table cannot be
        // reached in the running app. The panel signal arrives with 6.8; battery needs a task.
        PowerConstrained: false,
        SinceUserOpenedPanel: null,
        SinceLocalTranscriptActivity: transcripts.SinceLastWrite());

    /// <summary>
    /// The HUD's lines as of this instant. Core decides whether the reading may be shown at
    /// all, so a stale or frozen one comes back as no lines.
    /// </summary>
    private IReadOnlyList<ReadoutLine> ReadoutLines()
    {
        if (_monitor is null)
        {
            return [];
        }

        return Readout.Lines(_monitor.Current, _settings, _clock.Now);
    }

    // deferred: a failed save is dropped on the floor here. The settings still apply for this
    // session; surface it in the detail panel once 6.8 gives it somewhere to go.
    private void Remember(UserSettings changed)
    {
        _settings = changed;
        _settingsStore.Save(changed);
    }
}
