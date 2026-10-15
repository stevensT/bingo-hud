using System.Globalization;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Polling;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Display;

/// <summary>
/// Composes the detail panel (AC-23, AC-24). A pure function, for the same reason
/// <see cref="Readout"/> is one: the WPF layer places these strings and decides none of them.
///
/// <para>
/// Where the HUD's job is to say as little as possible, the panel's is to make the HUD
/// accountable. Both surfaces show a stale or frozen reading; the difference is room. The HUD has
/// space for a mark saying the numbers are not current, and the panel is where the same state
/// gets the sentence saying what to do about it, along with the exact times and the per-model
/// caps the HUD never shows at all.
/// </para>
/// </summary>
public static class PanelReadout
{
    /// <summary>
    /// The panel's contents at this instant.
    /// </summary>
    /// <param name="state">The monitor's current state.</param>
    /// <param name="settings">The user's display preferences; the panel reads percentages the same way the HUD does.</param>
    /// <param name="version">The running build, which only the shell can know.</param>
    /// <param name="now">The moment of rendering. Its offset is used only when no zone is given.</param>
    /// <param name="culture">Whose clock conventions the times use.</param>
    /// <param name="lastRefresh">
    /// What came of the refresh the user last asked for, or null if they have not asked (AC-28).
    /// </param>
    /// <param name="zone">The time zone times are shown in, using the offset in force at each instant; null keeps the offset of <paramref name="now"/>.</param>
    public static PanelContent Compose(
        ReadingState state,
        UserSettings settings,
        string version,
        DateTimeOffset now,
        CultureInfo? culture = null,
        RefreshResult? lastRefresh = null,
        TimeZoneInfo? zone = null)
    {
        var snapshot = state.Last;

        var windows = Rows(
            snapshot,
            w => w.Kind is WindowKind.Session or WindowKind.WeeklyAll,
            w => WindowName.Short(w.Kind),
            settings,
            now,
            culture,
            zone);

        var perModel = Rows(
            snapshot,
            w => w.Kind == WindowKind.WeeklyScoped,
            // The server's scope string, unaltered. Mapping it to a friendlier model name would
            // mean inventing a vocabulary for a field no capture has ever carried a value in.
            w => w.Scope ?? UnnamedScope,
            settings,
            now,
            culture,
            zone);

        return new PanelContent(
            Windows: windows,
            WindowsEmptyState: EmptyState(snapshot, windows),
            PerModelCaps: perModel,
            PerModelCapsEmptyState: EmptyState(snapshot, perModel),
            // Always shown, rather than only once a reading goes stale. A number on this
            // screen without its age is what principle 6 forbids, and a user who only ever sees
            // the age appear when something is wrong has no idea what it looks like when things
            // are right.
            Age: snapshot is null ? null : AgeText.Old(state.Age),
            LastPoll: snapshot is null ? "never" : ResetFormatter.Exact(snapshot.ObservedAt, now, culture, zone),
            NextPoll: PollLoop.Stops(state.LastFailure) ? PollingStopped : state.PollReason,
            Version: version,
            Status: StatusMessage.Describe(state),
            RefreshNotice: RefreshNotice.Describe(lastRefresh, now));
    }

    /// <summary>
    /// The whole panel: Claude's part, and a Codex block when Codex is shown.
    /// </summary>
    /// <remarks>
    /// Claude's part is composed exactly as it was before Codex, whether or not it is shown, and
    /// the shell hides it when it is switched off. Before the first-start question is answered
    /// the panel is Claude's alone, matching the HUD.
    /// </remarks>
    public static PanelContent ComposeAll(
        ReadingState claude,
        Codex.CodexState codex,
        UserSettings settings,
        string version,
        DateTimeOffset now,
        CultureInfo? culture = null,
        RefreshResult? lastRefresh = null,
        TimeZoneInfo? zone = null,
        string? settingsNotice = null)
    {
        var shown = settings.Products ?? new ProductSwitches(Claude: true, Codex: false);

        return Compose(claude, settings, version, now, culture, lastRefresh, zone) with
        {
            ShowClaude = shown.Claude,
            SettingsNotice = settingsNotice,
            Codex = shown.Codex ? ComposeCodex(codex, settings, now, culture, zone) : null,
        };
    }

    /// <summary>
    /// The Codex block at this instant (codex-quota AC-1, AC-6).
    ///
    /// <para>
    /// The same rows as Claude's, with one difference the HUD also makes: a window whose reset
    /// has passed shows no figure, because the one Bingo holds belongs to an occurrence that has
    /// ended (AC-3). Its row gives the time it reset instead.
    /// </para>
    /// </summary>
    public static CodexPanel ComposeCodex(
        Codex.CodexState state,
        UserSettings settings,
        DateTimeOffset now,
        CultureInfo? culture = null,
        TimeZoneInfo? zone = null)
    {
        var snapshot = state.Last;
        var windows = new List<PanelRow>();

        if (snapshot is not null)
        {
            foreach (var kind in Readout.HudKinds)
            {
                if (snapshot.Windows.FirstOrDefault(w => w.Kind == kind) is not { } window)
                {
                    continue;
                }

                var ended = window.ResetsAt is { } resetsAt && resetsAt <= now;

                windows.Add(new PanelRow(
                    WindowName.Short(kind),
                    ended ? NoFigureSinceReset : Percentage.Describe(window.UsedPercent, settings.Direction),
                    window.ResetsAt is { } at ? ResetFormatter.Exact(at, now, culture, zone) : NoResetTime));
            }
        }

        return new CodexPanel(
            Windows: windows,
            WindowsEmptyState: EmptyState(snapshot, windows),
            // Always shown with a reading, as Claude's is, and here it matters more: a Codex
            // figure is as old as the last Codex turn.
            Age: snapshot is null ? null : AgeText.Old(state.Age),
            LastTurn: snapshot is null ? "never" : ResetFormatter.Exact(snapshot.ObservedAt, now, culture, zone),
            Source: CodexSource,
            Status: StatusMessage.DescribeCodex(state),
            ReachedType: state.ReachedType,
            IgnoredWindows: state.IgnoredWindows);
    }

    /// <summary>
    /// Where every Codex figure comes from, and the one thing it cannot see (codex-quota AC-1).
    /// </summary>
    private const string CodexSource =
        "Read from Codex's own logs on this machine, so each figure is as of the last Codex turn on "
        + "this machine. Codex used elsewhere, in the browser, on another computer, or by cloud "
        + "tasks, shows here only after your next turn on this one.";

    /// <summary>A Codex window's figure column once its reset has passed.</summary>
    private const string NoFigureSinceReset = "no reading since it reset";

    /// <summary>
    /// Said in place of a section's rows when a reading carried none.
    ///
    /// <para>
    /// For per-model caps this is the common case, not the exceptional one: every capture so far
    /// reports them as null. An empty area under a heading reads as a section that failed to
    /// load, so the panel says outright that the server reported none.
    /// </para>
    /// </summary>
    private const string NoneReported = "None reported for this account.";

    /// <summary>
    /// Said in place of a section's rows when there is no reading at all. Saying the account has
    /// none would be a claim about the account that nothing has been read to support.
    /// </summary>
    private const string NothingRead = "Nothing read yet.";

    /// <summary>
    /// The next-poll row once polling has ended, matching the status advice that says so.
    /// </summary>
    private const string PollingStopped = "none, polling has stopped";

    private static string? EmptyState(QuotaSnapshot? snapshot, IReadOnlyList<PanelRow> rows) =>
        snapshot is null ? NothingRead
        : rows.Count == 0 ? NoneReported
        : null;

    /// <summary>
    /// A per-model cap whose scope came back empty. The normalizer will not produce one — a cap
    /// with no scope is not a per-model cap — so this exists to make a future regression legible
    /// on screen rather than to be seen.
    /// </summary>
    private const string UnnamedScope = "unnamed";

    /// <summary>Said in place of a reset time the server did not report.</summary>
    private const string NoResetTime = "not reported";

    private static IReadOnlyList<PanelRow> Rows(
        QuotaSnapshot? snapshot,
        Func<QuotaWindow, bool> include,
        Func<QuotaWindow, string> label,
        UserSettings settings,
        DateTimeOffset now,
        CultureInfo? culture,
        TimeZoneInfo? zone)
    {
        if (snapshot is null)
        {
            return [];
        }

        return snapshot.Windows
            .Where(include)
            .Select(w => new PanelRow(
                label(w),
                Percentage.Describe(w.UsedPercent, settings.Direction),
                w.ResetsAt is { } resetsAt
                    ? ResetFormatter.Exact(resetsAt, now, culture, zone)
                    : NoResetTime))
            .ToList();
    }
}
