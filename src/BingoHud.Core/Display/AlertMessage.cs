using System.Globalization;
using BingoHud.Core.Alerts;
using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Display;

/// <summary>
/// One desktop notification, as words (AC-14).
///
/// <para>
/// <see cref="Alert"/> carries a decision and no wording, so that deciding and announcing stay
/// separate jobs. This is the announcing half, and it lives in Core with every other string the
/// user sees.
/// </para>
/// <para>
/// A toast is the least forgiving surface in the app. It is read once, out of context, usually
/// while the user is doing something else, and it cannot be gone back to after it is dismissed.
/// So it says all three things at once: which window, where the account stands, and when that
/// changes on its own.
/// </para>
/// </summary>
/// <param name="Title">The line shown first, naming the window and how much trouble it is in.</param>
/// <param name="Body">The figure and when the window resets.</param>
public sealed record AlertMessage(string Title, string Body)
{
    /// <summary>
    /// One notification for everything that crossed on the same reading, or null when nothing
    /// did.
    ///
    /// <para>
    /// Deliberately one notification rather than several. Windows shows one at a time and
    /// silently drops any that arrive while it is up — observed, with both windows crossing on a
    /// single reading and only the first announced — while the engine records every crossing as
    /// fired whether or not anyone saw it. The dropped one is therefore gone until the window
    /// resets, which is precisely the failure AC-14 exists to prevent. Combining them is the
    /// only shape that cannot lose one.
    /// </para>
    /// </summary>
    /// <param name="due">The alerts the engine says are due, in the order it produced them.</param>
    /// <param name="direction">Which way the user reads a percentage (AC-2a).</param>
    /// <param name="now">The current instant, which reset phrases are measured from.</param>
    /// <param name="culture">Whose clock conventions the reset phrases use.</param>
    public static AlertMessage? Describe(
        IReadOnlyList<Alert> due,
        DisplayDirection direction,
        DateTimeOffset now,
        CultureInfo? culture = null)
    {
        if (due.Count == 0)
        {
            return null;
        }

        if (due.Count == 1)
        {
            return Describe(due[0], direction, now, culture);
        }

        // The worst of them names the batch. A title calling a critical crossing "running low"
        // would understate it, and the body below still says which window is which.
        var worst = due.Max(a => a.Severity);

        var lines = due.Select(a =>
            $"{WindowName.Short(a.Key.Kind)}: {Line(a, direction, now, culture)}");

        return new AlertMessage(
            $"{due.Count} windows {Trouble(worst)}",
            string.Join(Environment.NewLine, lines));
    }

    /// <summary>
    /// The notification for one alert.
    /// </summary>
    /// <param name="alert">The crossing to announce.</param>
    /// <param name="direction">Which way the user reads a percentage (AC-2a).</param>
    /// <param name="now">The current instant, which the reset phrase is measured from.</param>
    /// <param name="culture">Whose clock conventions the reset phrase uses.</param>
    public static AlertMessage Describe(
        Alert alert,
        DisplayDirection direction,
        DateTimeOffset now,
        CultureInfo? culture = null)
    {
        var window = WindowName.Short(alert.Key.Kind);

        return new AlertMessage(
            $"{window} window {Trouble(alert.Severity)}",
            Line(alert, direction, now, culture));
    }

    /// <summary>
    /// Where one window stands and when it resets, as one sentence.
    /// </summary>
    private static string Line(
        Alert alert,
        DisplayDirection direction,
        DateTimeOffset now,
        CultureInfo? culture)
    {
        // The reading, not the threshold that was crossed. The line was drawn at 25% remaining;
        // where the account actually stands is the number worth interrupting someone for.
        var percent = Percentage.Describe(alert.UsedPercent, direction);

        // The same phrasing the HUD uses, so a toast and the HUD behind it never describe one
        // reset two ways. An alert always has a reset time: a window without one gets no alert
        // key at all, because "at most once per occurrence" has nothing to mean without one.
        var reset = ResetFormatter.Describe(alert.Key.ResetsAt, now, culture);

        return $"{percent}, {reset}.";
    }

    /// <summary>
    /// How much trouble the window is in, in words.
    ///
    /// <para>
    /// The two must not read alike. A user who cannot tell a warning from a critical at a glance
    /// has three severity states on the HUD and one in their notifications, which makes the
    /// second crossing say nothing the first did not.
    /// </para>
    /// <para>
    /// Only these two are reachable: alerts are raised from the warning and critical thresholds,
    /// and nothing else produces one.
    /// </para>
    /// </summary>
    private static string Trouble(Severity severity) => severity switch
    {
        Severity.Warning => "running low",
        Severity.Critical => "nearly used up",
        _ => throw new ArgumentOutOfRangeException(
            nameof(severity), severity, "No threshold raises an alert at this severity."),
    };
}
