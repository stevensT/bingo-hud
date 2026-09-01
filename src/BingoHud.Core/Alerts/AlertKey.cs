using BingoHud.Core.Usage;

namespace BingoHud.Core.Alerts;

/// <summary>
/// The identity of one alert: a threshold, on one window, in one occurrence of that window.
///
/// <para>
/// The occurrence is <see cref="ResetsAt"/>. It is the only thing that distinguishes today's
/// session window from tomorrow's, so putting it in the identity is what makes rearming on
/// reset free — after a reset the key no longer matches anything the store has recorded, and
/// the alert is armed again with no separate mechanism to get wrong.
/// </para>
/// </summary>
/// <param name="Kind">Which window the alert is about.</param>
/// <param name="ThresholdPercent">
/// The threshold that was crossed, as percentage <em>remaining</em>, matching
/// <see cref="Thresholds"/>. 25 and 10 are the defaults.
/// </param>
/// <param name="ResetsAt">
/// When the window occurrence ends. Compared as an instant, not as a spelling, so state that
/// round-trips through a store in a different offset still matches.
/// </param>
public sealed record AlertKey(WindowKind Kind, int ThresholdPercent, DateTimeOffset ResetsAt)
{
    /// <summary>
    /// The key for a threshold on a window, or null when the window reports no reset time.
    ///
    /// <para>
    /// A window with no reset time has no occurrence boundary, so "at most once per occurrence"
    /// has nothing to mean. It gets no key, and therefore never alerts — the alternative is an
    /// alert that fires on every poll and can never be rearmed or silenced.
    /// </para>
    /// </summary>
    public static AlertKey? For(QuotaWindow window, int thresholdPercent) =>
        window.ResetsAt is { } resetsAt
            ? new AlertKey(window.Kind, thresholdPercent, resetsAt)
            : null;
}
