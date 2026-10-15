using BingoHud.Core.Settings;
using BingoHud.Core.Usage;

namespace BingoHud.Core.Alerts;

/// <summary>
/// Alerts and Mute for the products the user chose to see, and no others (codex-quota spec,
/// Decided, 2026-10-15). A notification about a product switched off is that product shown.
///
/// <para>
/// Crossings are still recorded as they happen whether or not their product is shown, so
/// switching a product back on does not announce a crossing from while it was off. Off means not
/// interrupted, not interrupted later.
/// </para>
/// </summary>
public static class ProductAlerts
{
    /// <summary>The alerts whose product is shown, in the order given.</summary>
    public static IReadOnlyList<Alert> Shown(IReadOnlyList<Alert> due, ProductSwitches shown) =>
        due.Where(alert => shown.IsOn(alert.Key.Product)).ToArray();

    /// <summary>
    /// The readings Mute acts on: each shown product's, where it has one. Empty means there is
    /// nothing to mute, so the menu item is greyed out.
    /// </summary>
    public static IReadOnlyList<(Product Product, QuotaSnapshot Snapshot)> Mutable(
        ProductSwitches shown, QuotaSnapshot? claude, QuotaSnapshot? codex)
    {
        var targets = new List<(Product, QuotaSnapshot)>();

        if (shown.Claude && claude is not null)
        {
            targets.Add((Product.Claude, claude));
        }

        if (shown.Codex && codex is not null)
        {
            targets.Add((Product.Codex, codex));
        }

        return targets;
    }
}
