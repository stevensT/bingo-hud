using System.Globalization;
using BingoHud.Core.Codex;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Display;

/// <summary>
/// Decides every word on the HUD (AC-1 through AC-3), which windows get a line (AC-7), each
/// line's severity (AC-4 through AC-6), and how a reading that is no longer current is marked
/// (AC-8, AC-13), plus the reset format and each line's bar fills from the display options. The
/// WPF layer places the strings and colours by severity; it does not compose them.
///
/// <para>
/// A pure function over the current reading state, the user's display settings, the moment of
/// rendering, a culture, and a time zone. The shell passes no culture and gets the user's Windows
/// setting, which is the intent, and passes the machine's zone; tests pass both so the phrase is
/// fixed. Being pure is what makes the one
/// error nobody could spot by looking at the screen — a percentage reading the wrong way round
/// — a thing a test pins instead.
/// </para>
/// </summary>
public static class Readout
{
    /// <summary>
    /// The HUD's lines, session first, then the all-models weekly window; or none.
    ///
    /// <para>
    /// None until the first reading arrives, and none when the reading named no window the HUD
    /// draws; <see cref="Content"/> supplies words in their place. Otherwise a reading is always
    /// shown, including a stale or frozen one, because the last thing the server actually said is
    /// the best answer available and hiding it answers nothing. What principle 6 forbids is not
    /// the old number but the old number read as current, which is the mark's job rather than
    /// this one's.
    /// </para>
    /// <para>
    /// A window the server did not report has no line. It is not shown as zero, because a zero
    /// would be a number the server never sent. Model-scoped weekly caps belong to the detail
    /// panel (AC-23) and are left out here, so the HUD never grows a third line.
    /// </para>
    /// </summary>
    /// <param name="state">The monitor's current state.</param>
    /// <param name="settings">The user's display preferences: direction, collapse, thresholds, reset format, bar.</param>
    /// <param name="now">The moment of rendering. Its offset is used only when no zone is given.</param>
    /// <param name="culture">Whose clock conventions the reset phrase uses.</param>
    /// <param name="zone">The time zone times are shown in, using the offset in force at each instant; null keeps the offset of <paramref name="now"/>.</param>
    public static IReadOnlyList<ReadoutLine> Lines(
        ReadingState state,
        UserSettings settings,
        DateTimeOffset now,
        CultureInfo? culture = null,
        TimeZoneInfo? zone = null)
    {
        if (state.Last is not { } snapshot)
        {
            return [];
        }

        return LinesFor(Product.Claude, snapshot, frozen: state.Freshness == Freshness.Frozen, settings, now, culture, zone);
    }

    /// <summary>
    /// One product's lines from one reading. Shared by both products, so a Codex line and a Claude
    /// line can never be drawn by different rules (codex-quota AC-4, AC-5).
    /// </summary>
    /// <param name="ended">
    /// Which windows have reset since the reading was taken, or null when none is treated so.
    /// Only Codex passes one: its reading is as old as the last turn, while Claude's is at most a
    /// poll old and AC-9 keeps its HUD as 0.4.0 drew it.
    /// </param>
    private static List<ReadoutLine> LinesFor(
        Product product,
        QuotaSnapshot snapshot,
        bool frozen,
        UserSettings settings,
        DateTimeOffset now,
        CultureInfo? culture,
        TimeZoneInfo? zone,
        Func<QuotaWindow, bool>? ended = null)
    {
        var isEnded = ended ?? (_ => false);
        var shown = Shown(snapshot, settings, isEnded);
        var lines = new List<ReadoutLine>(shown.Count);

        foreach (var window in shown)
        {
            if (isEnded(window))
            {
                // The figure belongs to an occurrence that has ended, and nothing has been said
                // about the new one. No number, no bar, no colour (codex-quota AC-3, AC-2).
                lines.Add(new ReadoutLine(
                    WindowName.Label(product, window.Kind), Percent: null, EndedPhrase, Severity.Normal, Bar: null));

                continue;
            }

            // A frozen reading takes no colour (AC-13). The overall severity is the worst line,
            // so excluding it here excludes it from the accent bar too.
            var severity = frozen
                ? Severity.Normal
                : SeverityPolicy.Evaluate(window, settings.Thresholds);

            lines.Add(new ReadoutLine(
                WindowName.Label(product, window.Kind),
                Percentage.Describe(window.UsedPercent, settings.Direction),
                Reset(window, severity, settings.ResetFormat, now, culture, zone),
                severity,
                // Always the used figure, whatever the direction setting: the bar fills as usage
                // rises, and the words beside it are what follow the direction (display options
                // AC-9).
                settings.ShowBar ? Bar.Segments(window.UsedPercent) : null));
        }

        return lines;
    }

    /// <summary>
    /// Everything the HUD puts on screen: its lines, and the phrase that stands in for them when
    /// there are none.
    ///
    /// <para>
    /// One value rather than two calls, because the shell only repaints when what it holds
    /// differs from what is on screen. Asked separately, a change from "No reading yet" to
    /// "Signed out" would be a change in neither the line count nor the lines, and the HUD would
    /// go on showing the older phrase until something else happened to move it.
    /// </para>
    /// </summary>
    /// <param name="state">The monitor's current state.</param>
    /// <param name="settings">The user's display preferences: direction, collapse, thresholds, reset format, bar.</param>
    /// <param name="now">The moment of rendering. Its offset is used only when no zone is given.</param>
    /// <param name="culture">Whose clock conventions the reset phrase uses.</param>
    /// <param name="zone">The time zone times are shown in, using the offset in force at each instant; null keeps the offset of <paramref name="now"/>.</param>
    public static HudContent Content(
        ReadingState state,
        UserSettings settings,
        DateTimeOffset now,
        CultureInfo? culture = null,
        TimeZoneInfo? zone = null)
    {
        var lines = Lines(state, settings, now, culture, zone);

        if (lines.Count == 0)
        {
            // Every state that yields no lines has words for itself: no reading yet, a failure,
            // or a response naming no window the HUD draws. The floor below is unreachable
            // today and exists so that narrowing Shown() cannot silently produce a blank HUD —
            // the compiler demands a phrase here, which is the point of the two cases.
            return new HudContent.Empty(
                StatusMessage.Describe(state)?.Headline ?? StatusMessage.NoWindowToShow);
        }

        return new HudContent.Reading(lines, StatusMessage.MarkFor(state), state.Freshness == Freshness.Frozen);
    }

    /// <summary>
    /// The whole HUD: a section for each product switched on, Claude first (codex-quota AC-4,
    /// AC-7, AC-8).
    ///
    /// <para>
    /// A product switched on always has a section, even with nothing to show, because a missing
    /// line looks the same as Bingo failing to look (AC-7). Before the first-start question is
    /// answered only Claude is shown: the question appears before the HUD, so that is the moment
    /// between, or a failed save, and Claude alone is what Bingo has always shown.
    /// </para>
    /// </summary>
    /// <param name="claude">Claude's monitor state.</param>
    /// <param name="codex">Codex's monitor state.</param>
    /// <param name="settings">The user's display preferences, including which products are shown.</param>
    /// <param name="now">The moment of rendering. Its offset is used only when no zone is given.</param>
    /// <param name="culture">Whose clock conventions the reset phrase uses.</param>
    /// <param name="zone">The time zone times are shown in; null keeps the offset of <paramref name="now"/>.</param>
    public static Hud Compose(
        ReadingState claude,
        CodexState codex,
        UserSettings settings,
        DateTimeOffset now,
        CultureInfo? culture = null,
        TimeZoneInfo? zone = null)
    {
        var shown = settings.Products ?? new ProductSwitches(Claude: true, Codex: false);
        var sections = new List<HudSection>(2);

        if (shown.Claude)
        {
            sections.Add(new HudSection(Product.Claude, Content(claude, settings, now, culture, zone)));
        }

        if (shown.Codex)
        {
            sections.Add(new HudSection(Product.Codex, CodexContent(codex, settings, now, culture, zone)));
        }

        return new Hud(sections);
    }

    /// <summary>
    /// Codex's section: its lines and mark, or the phrase that stands in for them
    /// (codex-quota AC-3, AC-3a, AC-7).
    ///
    /// <para>
    /// A Codex reading is as old as the last Codex turn, so it always carries its age. When the
    /// logs read cleanly an old figure is still true, and keeps its colours: Codex has simply not
    /// been used since. When they could not be read, Bingo cannot tell whether Codex has been used
    /// since, so the reading is drawn the way a frozen Claude reading is, without colour, and the
    /// mark says why.
    /// </para>
    /// </summary>
    private static HudContent CodexContent(
        CodexState state,
        UserSettings settings,
        DateTimeOffset now,
        CultureInfo? culture,
        TimeZoneInfo? zone)
    {
        if (state.Last is not { } snapshot)
        {
            // deferred: placeholder words until the Codex states get their own in StatusMessage
            // (task 4.4), with headline and advice like every other state.
            return new HudContent.Empty(
                state.Presence == CodexPresence.NotFound ? "Codex not found" : "No Codex reading yet");
        }

        var failed = state.Problem is not null;
        var lines = LinesFor(
            Product.Codex, snapshot, frozen: failed, settings, now, culture, zone,
            ended: window => window.ResetsAt is { } resetsAt && resetsAt <= now);

        return lines.Count == 0
            ? new HudContent.Empty(StatusMessage.NoWindowToShow)
            : new HudContent.Reading(lines, StatusMessage.CodexMark(state), Frozen: failed);
    }

    /// <summary>What a Codex window whose reset has passed says in place of its figure.</summary>
    private const string EndedPhrase = "reset, no use since";

    /// <summary>
    /// The reset phrase, led by "limited" when the server is refusing work against the window.
    ///
    /// <para>
    /// AC-6 asks for a server refusal to be surfaced distinctly from a local threshold. Colour
    /// alone does not do it: critical and rate-limited are both bold with the accent bar, and red against
    /// magenta is the pair the most common colour-blindness confuses. The word does. It goes
    /// beside the reset rather than in place of "used", because the direction word is what AC-2b
    /// needs on every figure.
    /// </para>
    /// </summary>
    private static string? Reset(
        QuotaWindow window,
        Severity severity,
        ResetFormat format,
        DateTimeOffset now,
        CultureInfo? culture,
        TimeZoneInfo? zone)
    {
        var reset = format == ResetFormat.Countdown
            ? ResetFormatter.Countdown(window.ResetsAt, now, culture)
            : ResetFormatter.Describe(window.ResetsAt, now, culture, zone);

        if (severity != Severity.RateLimited)
        {
            return reset;
        }

        return reset is null ? RateLimitedWord : $"{RateLimitedWord}, {reset}";
    }

    private const string RateLimitedWord = "limited";

    /// <summary>
    /// The windows the HUD draws a line for, session first. Named here because
    /// <see cref="StatusMessage"/> has to ask the same question to tell a reading that named none
    /// of them from a reading that has not arrived.
    /// </summary>
    internal static readonly WindowKind[] HudKinds = [WindowKind.Session, WindowKind.WeeklyAll];

    /// <summary>
    /// Which windows get a line, session first (AC-7).
    ///
    /// <para>
    /// Both, unless the user has asked for collapse; then only the worst, unless both are in
    /// trouble. That last exception is the whole point of the setting: collapse buys quiet while
    /// nothing is wrong, and gives it back the moment two limits are closing at once, because a
    /// hidden window the user is about to hit is the one thing the HUD must never do.
    /// </para>
    /// <para>
    /// A window the server did not report is absent rather than normal, so a reading with one
    /// window collapses to that window rather than to nothing.
    /// </para>
    /// <para>
    /// A window that has ended (codex-quota AC-3) needs no attention whatever figure it last
    /// held, so it counts as normal and as the emptiest window, and collapse keeps the other.
    /// </para>
    /// </summary>
    private static IReadOnlyList<QuotaWindow> Shown(
        QuotaSnapshot snapshot, UserSettings settings, Func<QuotaWindow, bool> isEnded)
    {
        var windows = HudKinds
            .Select(kind => snapshot.Windows.FirstOrDefault(w => w.Kind == kind))
            .OfType<QuotaWindow>()
            .ToList();

        if (!settings.Collapse || windows.Count < 2)
        {
            return windows;
        }

        var severities = windows
            .Select(w => isEnded(w) ? Severity.Normal : SeverityPolicy.Evaluate(w, settings.Thresholds))
            .ToList();

        if (severities.All(s => s != Severity.Normal))
        {
            return windows;
        }

        var fullness = windows.Select(w => isEnded(w) ? -1 : w.UsedPercent).ToList();

        return [Worst(windows, severities, fullness)];
    }

    /// <summary>
    /// The one window a collapsed HUD names.
    ///
    /// <para>
    /// Severity first, so a window the server is refusing outranks a fuller one that is merely
    /// full. Percentage breaks a tie within a severity, because between two windows the HUD
    /// calls equally normal, the fuller one is the one about to stop being normal. An exact tie
    /// goes to the session window: it is the shorter of the two, so it is the one that stops
    /// work first.
    /// </para>
    /// </summary>
    private static QuotaWindow Worst(
        IReadOnlyList<QuotaWindow> windows, IReadOnlyList<Severity> severities, IReadOnlyList<double> fullness)
    {
        var worst = 0;

        for (var i = 1; i < windows.Count; i++)
        {
            var better = Rank(severities[i]) > Rank(severities[worst])
                || (Rank(severities[i]) == Rank(severities[worst])
                    && fullness[i] > fullness[worst]);

            if (better)
            {
                worst = i;
            }
        }

        return windows[worst];
    }

    /// <summary>
    /// How serious a severity is, as a number that can be compared.
    ///
    /// <para>
    /// <see cref="Severity.RateLimited"/> is the top of the order rather than a fourth step past
    /// critical; the enum orders it last already, but saying so here means the ranking does not
    /// silently depend on the order members happen to be declared in.
    /// </para>
    /// </summary>
    internal static int Rank(Severity severity) => severity switch
    {
        Severity.Normal => 0,
        Severity.Warning => 1,
        Severity.Critical => 2,
        Severity.RateLimited => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
    };

}
