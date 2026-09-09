using System.Net.Http;
using System.Reflection;
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
    private DetailPanelWindow? _panel;
    private DateTimeOffset? _panelOpenedAt;
    private TrayIcon? _tray;
    private RefreshResult? _lastRefresh;
    private TranscriptActivity? _transcripts;
    private AlertEngine? _alerts;

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

        _transcripts = new TranscriptActivity(TranscriptActivity.DefaultPath, _clock);
        _alerts = new AlertEngine(new AlertStateStore(AlertStateStore.DefaultPath, _clock));

        var loop = new PollLoop(
            _monitor,
            _clock,
            gatherSignals: GatherSignals,
            _alerts,
            thresholds: () => _settings.Thresholds,
            onAlerts: Announce);

        MainWindow = new HudWindow(
            _settings.Position,
            position => Remember(_settings with { Position = position }),
            _clock,
            HudReadout,
            OpenPanel);
        MainWindow.Show();

        // The tray is the only route to the collapse and direction settings, and the only way to
        // quit: the HUD is frameless and has no close button by design (AC-19).
        _tray = new TrayIcon(
            settings: () => _settings,
            change: Remember,
            openPanel: OpenPanel,
            mute: Mute,
            canMute: () => _monitor?.Current.Last is not null,
            quit: Shutdown);

        // Started last, once there is somewhere for an alert to go. The first poll happens
        // immediately, and an account already past a threshold would raise an alert from it; a
        // notification dropped because the tray did not exist yet would be lost for good, since
        // the engine records each crossing as fired whether or not anyone showed it.
        //
        // Runs until shutdown, or until the endpoint says this account cannot use it; the loop
        // completes normally in both cases, so there is nothing to await. Anything else that
        // ends it is a bug, and a bug that stopped polling silently would leave the last number
        // on screen looking alive. So it is thrown on the UI thread instead, where it ends the
        // process with the cause attached.
        loop.RunAsync(_shutdown.Token).ContinueWith(
            stopped => Dispatcher.InvokeAsync(() => throw new InvalidOperationException(
                "The poll loop stopped unexpectedly.", stopped.Exception)),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // A fetch in flight is abandoned rather than awaited. Nobody will see its reading.
        _shutdown.Cancel();
        _tray?.Dispose();
        _http.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// What the machine and the user are doing, read immediately before each poll.
    /// </summary>
    private PollSignals GatherSignals() => new(
        // deferred: battery state needs a Win32 call, so one row of the cadence table still
        // cannot be reached in the running app. Needs a task.
        PowerConstrained: false,
        SinceUserOpenedPanel: _panelOpenedAt is { } opened ? _clock.Now - opened : null,
        SinceLocalTranscriptActivity: _transcripts?.SinceLastWrite());

    /// <summary>
    /// Opens the detail panel, or brings the open one forward (AC-23).
    ///
    /// <para>
    /// One panel, reused. A second window would be a second copy of the same facts, and closing
    /// one of them would leave the other looking authoritative.
    /// </para>
    /// <para>
    /// Opening it is also a cadence signal: someone looking at the numbers is someone who wants
    /// them current, and the poll loop is told so it can tighten the interval. The signal is the
    /// time since the panel was last opened, so it decays on its own without anything having to
    /// clear it.
    /// </para>
    /// </summary>
    private void OpenPanel()
    {
        _panelOpenedAt = _clock.Now;

        if (_panel is not null)
        {
            _panel.Activate();
            return;
        }

        _panel = new DetailPanelWindow(CurrentPanelContent, RefreshNowAsync) { Owner = MainWindow };
        _panel.Closed += (_, _) => _panel = null;
        _panel.Show();
    }

    /// <summary>
    /// The panel's contents as of this instant. Unlike the HUD, this shows a stale or frozen
    /// reading rather than hiding it, because the panel is where the user asks why.
    /// </summary>
    private PanelContent CurrentPanelContent()
    {
        var state = _monitor?.Current
            ?? new ReadingState(null, Freshness.Fresh, null, TimeSpan.Zero, "not started");

        return PanelReadout.Compose(state, _settings, Version, _clock.Now, culture: null, _lastRefresh);
    }

    /// <summary>
    /// Asks for a reading now, at the user's request (AC-28).
    ///
    /// <para>
    /// Held to the same floor as an automatic poll, and refused when it has not elapsed. The
    /// outcome is kept so the panel can say what happened; a refusal that vanished would leave a
    /// button that appears to do nothing.
    /// </para>
    /// <para>
    /// The signals are gathered fresh, exactly as the loop does before its own poll, so a manual
    /// refresh is judged against the same cadence as everything else rather than a stale one.
    /// </para>
    /// </summary>
    private async Task RefreshNowAsync()
    {
        if (_monitor is null)
        {
            return;
        }

        _lastRefresh = await _monitor.RefreshAsync(GatherSignals(), _shutdown.Token);
    }

    /// <summary>
    /// The running build (AC-24). Read from the assembly rather than written down, so it cannot
    /// disagree with what was actually shipped; Core decides how it reads.
    /// </summary>
    private static string Version => VersionLabel.Describe(
        typeof(App).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>
    /// What the HUD shows as of this instant. Core decides all of it: which windows get a line,
    /// how a reading that is no longer current is marked, and what stands in for the lines when
    /// there are none.
    /// </summary>
    private HudContent HudReadout()
    {
        if (_monitor is null)
        {
            // Only reachable if the HUD renders before startup finishes wiring the monitor. The
            // honest phrase for it is the same one the first second of every run shows.
            return new HudContent([], "No reading yet");
        }

        return Readout.Content(_monitor.Current, _settings, _clock.Now);
    }

    /// <summary>
    /// Raises a desktop notification for each alert the engine says is due (AC-14).
    ///
    /// <para>
    /// The loop runs off the UI thread, and the notification area may only be touched from the
    /// thread that owns it, so this hops back. Core has already decided that these are due and
    /// that each is due only once (AC-15); nothing here re-decides anything.
    /// </para>
    /// </summary>
    private void Announce(IReadOnlyList<Alert> due)
    {
        Dispatcher.InvokeAsync(() =>
        {
            // One notification for the whole batch. Windows shows one at a time and drops any
            // that arrive while it is up, so announcing them one by one loses all but the first
            // — and the engine has already recorded every one as fired, so a lost one never
            // comes back. Core decides the wording for one or many alike.
            if (AlertMessage.Describe(due, _settings.Direction, _clock.Now) is not { } message)
            {
                return;
            }

            _tray?.Notify(
                message.Title,
                message.Body,
                critical: due.Any(a => a.Severity == Severity.Critical));
        });
    }

    /// <summary>
    /// Silences every window's current occurrence (AC-18).
    ///
    /// <para>
    /// "The current window" is read as every window on the reading rather than one of them. The
    /// user reaching for mute is saying "not now" about the interruption, not about one of two
    /// quotas they were not asked to choose between.
    /// </para>
    /// <para>
    /// This cannot silence Bingo indefinitely, and that is the point: muting records the
    /// thresholds as already fired for this occurrence only, so it lifts at the next reset with
    /// no mechanism of its own to get wrong.
    /// </para>
    /// </summary>
    private bool Mute()
    {
        if (_alerts is null || _monitor?.Current.Last is not { } snapshot)
        {
            return false;
        }

        foreach (var window in snapshot.Windows)
        {
            _alerts.Mute(window, _settings.Thresholds);
        }

        return true;
    }

    /// <summary>
    /// Applies a settings change and writes it down (AC-22). The HUD and panel both re-read
    /// once a second, so a change made from the tray shows up without anything being told.
    /// </summary>
    // deferred: a failed save is dropped on the floor here. The settings still apply for this
    // session. The panel now exists to carry the news, but it has no place to put it until
    // 7.1 writes the copy for states like this one.
    private void Remember(UserSettings changed)
    {
        _settings = changed;
        _settingsStore.Save(changed);
    }
}
