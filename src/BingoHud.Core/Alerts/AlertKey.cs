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
/// <see cref="Thresholds"/>. 25 and 10 are the defaults. Kept exactly as set, not rounded, so
/// two thresholds that round alike stay two alerts; 0.3.0 wrote whole numbers, which read back
/// equal.
/// </param>
/// <param name="ResetsAt">
/// When the window occurrence ends. Compared as an instant, not as a spelling, so state that
/// round-trips through a store in a different offset still matches.
/// </param>
/// <param name="Product">
/// Whose window it is (codex-quota AC-10). Last and defaulted to Claude because 0.4.0 wrote no
/// product, and every alert it recorded was Claude's.
/// </param>
public sealed record AlertKey(
    WindowKind Kind,
    double ThresholdPercent,
    DateTimeOffset ResetsAt,
    Product Product = Product.Claude)
{
    /// <summary>
    /// How far apart two reset times may be and still name one occurrence of a window. Codex
    /// reports one window's reset a few seconds differently from record to record; two
    /// occurrences of one window are at least five hours apart.
    /// </summary>
    public static TimeSpan ResetTolerance { get; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Whether this key and <paramref name="other"/> are the same alert: same product, window and
    /// threshold, with resets within <see cref="ResetTolerance"/> of each other.
    ///
    /// <para>
    /// Stores ask this rather than comparing keys for equality. Matched to the instant, a single
    /// Codex crossing would be a new alert on every record, because its reset moves.
    /// </para>
    /// </summary>
    public bool SameOccurrenceAs(AlertKey other) =>
        Product == other.Product
        && Kind == other.Kind
        && ThresholdPercent.Equals(other.ThresholdPercent)
        && (ResetsAt - other.ResetsAt).Duration() <= ResetTolerance;

    /// <summary>
    /// The key for a threshold on a window, or null when the window reports no reset time.
    ///
    /// <para>
    /// A window with no reset time has no occurrence boundary, so "at most once per occurrence"
    /// has nothing to mean. It gets no key, and therefore never alerts — the alternative is an
    /// alert that fires on every poll and can never be rearmed or silenced.
    /// </para>
    /// </summary>
    public static AlertKey? For(QuotaWindow window, double thresholdPercent, Product product = Product.Claude) =>
        window.ResetsAt is { } resetsAt
            ? new AlertKey(window.Kind, thresholdPercent, resetsAt, product)
            : null;
}
