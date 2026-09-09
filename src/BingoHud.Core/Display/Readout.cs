using System.Globalization;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Display;

/// <summary>
/// Decides every word on the HUD (AC-1 through AC-3), which windows get a line (AC-7), and how a
/// reading that is no longer current is marked (AC-8, AC-13). The WPF layer places the strings;
/// it does not compose them.
///
/// <para>
/// A pure function over the current reading state, the display direction, the moment of
/// rendering, and a culture. The shell passes no culture and gets the user's Windows setting,
/// which is the intent; tests pass one so the phrase is fixed. Being pure is what makes the one
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
    /// <param name="settings">The user's display preferences: direction, collapse, thresholds.</param>
    /// <param name="now">The moment of rendering, carrying the offset reset times are shown in.</param>
    /// <param name="culture">Whose clock conventions the reset phrase uses.</param>
    public static IReadOnlyList<ReadoutLine> Lines(
        ReadingState state,
        UserSettings settings,
        DateTimeOffset now,
        CultureInfo? culture = null)
    {
        if (state.Last is not { } snapshot)
        {
            return [];
        }

        var shown = Shown(snapshot, settings);
        var lines = new List<ReadoutLine>(shown.Count);

        foreach (var window in shown)
        {
            lines.Add(new ReadoutLine(
                WindowName.Short(window.Kind),
                Percentage.Describe(window.UsedPercent, settings.Direction),
                ResetFormatter.Describe(window.ResetsAt, now, culture)));
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
    /// <param name="settings">The user's display preferences: direction, collapse, thresholds.</param>
    /// <param name="now">The moment of rendering, carrying the offset reset times are shown in.</param>
    /// <param name="culture">Whose clock conventions the reset phrase uses.</param>
    public static HudContent Content(
        ReadingState state,
        UserSettings settings,
        DateTimeOffset now,
        CultureInfo? culture = null)
    {
        var lines = Lines(state, settings, now, culture);

        if (lines.Count == 0)
        {
            // Every state that yields no lines has words for itself: no reading yet, a failure,
            // or a response naming no window the HUD draws. The floor below is unreachable
            // today and exists so that narrowing Shown() cannot silently produce a blank HUD —
            // the compiler demands a phrase here, which is the point of the two cases.
            return new HudContent.Empty(
                StatusMessage.Describe(state)?.Headline ?? StatusMessage.NoWindowToShow);
        }

        return new HudContent.Reading(lines, StatusMessage.MarkFor(state));
    }

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
    /// </summary>
    private static IReadOnlyList<QuotaWindow> Shown(QuotaSnapshot snapshot, UserSettings settings)
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
            .Select(w => SeverityPolicy.Evaluate(w, settings.Thresholds))
            .ToList();

        if (severities.All(s => s != Severity.Normal))
        {
            return windows;
        }

        return [Worst(windows, severities)];
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
    private static QuotaWindow Worst(IReadOnlyList<QuotaWindow> windows, IReadOnlyList<Severity> severities)
    {
        var worst = 0;

        for (var i = 1; i < windows.Count; i++)
        {
            var better = Rank(severities[i]) > Rank(severities[worst])
                || (Rank(severities[i]) == Rank(severities[worst])
                    && windows[i].UsedPercent > windows[worst].UsedPercent);

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
    private static int Rank(Severity severity) => severity switch
    {
        Severity.Normal => 0,
        Severity.Warning => 1,
        Severity.Critical => 2,
        Severity.RateLimited => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
    };

}
