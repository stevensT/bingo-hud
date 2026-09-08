using System.Globalization;
using BingoHud.Core.Monitoring;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Display;

/// <summary>
/// Decides every word on the HUD (AC-1 through AC-3), and whether any number may be shown at
/// all (AC-8, AC-10, AC-13). The WPF layer places the strings; it does not compose them.
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
    /// None while there is no reading, and none while the reading is not fresh. A frozen
    /// reading will never refresh and a stale one has missed a poll; either one shown as a
    /// plain percentage next to a countdown that keeps moving would be read as current, which
    /// is the lie principle 6 exists to prevent. The words for those states are 7.1's; until
    /// then the shell shows its one empty-state phrase for all of them.
    /// </para>
    /// <para>
    /// A window the server did not report has no line. It is not shown as zero, because a zero
    /// would be a number the server never sent. Model-scoped weekly caps belong to the detail
    /// panel (AC-23) and are left out here, so the HUD never grows a third line.
    /// </para>
    /// </summary>
    /// <param name="state">The monitor's current state.</param>
    /// <param name="direction">Which way the percentage reads (AC-2a).</param>
    /// <param name="now">The moment of rendering, carrying the offset reset times are shown in.</param>
    /// <param name="culture">Whose clock conventions the reset phrase uses.</param>
    public static IReadOnlyList<ReadoutLine> Lines(
        ReadingState state,
        DisplayDirection direction,
        DateTimeOffset now,
        CultureInfo? culture = null)
    {
        if (state.Last is not { } snapshot || state.Freshness != Freshness.Fresh)
        {
            return [];
        }

        var lines = new List<ReadoutLine>(2);

        foreach (var kind in new[] { WindowKind.Session, WindowKind.WeeklyAll })
        {
            var window = snapshot.Windows.FirstOrDefault(w => w.Kind == kind);

            if (window is null)
            {
                continue;
            }

            lines.Add(new ReadoutLine(
                Name(kind),
                Percent(window.UsedPercent, direction),
                ResetFormatter.Describe(window.ResetsAt, now, culture)));
        }

        return lines;
    }

    private static string Name(WindowKind kind) => kind switch
    {
        WindowKind.Session => "5h",
        WindowKind.WeeklyAll => "Week",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a HUD window."),
    };

    /// <summary>
    /// The figure with its direction beside it (AC-2b). Stored values are consumed; this is the
    /// one place the figure is inverted for display, and the word changes with the number.
    /// </summary>
    private static string Percent(double usedPercent, DisplayDirection direction)
    {
        // Whole numbers, as /usage shows them (AC-2). Rounded once, then inverted as a whole
        // number, so the two directions are always the same reading described two ways. Rounding
        // each direction separately would put 63 used beside 38 left at 62.5.
        var used = (int)Math.Round(usedPercent, MidpointRounding.AwayFromZero);

        return direction switch
        {
            DisplayDirection.Consumed => $"{used}% used",
            DisplayDirection.Remaining => $"{100 - used}% left",
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
        };
    }
}
