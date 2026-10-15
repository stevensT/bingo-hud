namespace BingoHud.Core.Settings;

/// <summary>
/// Which products the HUD shows, one switch each (codex-quota AC-8).
///
/// <para>
/// At least one is always on. A HUD showing no product has nothing to say, and looks the same as
/// one that has crashed, so turning off the last product that is on is refused here rather than
/// left to the tray menu to remember.
/// </para>
/// </summary>
/// <param name="Claude">Whether Claude's windows are shown.</param>
/// <param name="Codex">Whether Codex's windows are shown.</param>
public sealed record ProductSwitches(bool Claude, bool Codex)
{
    /// <summary>Whether a product is switched on.</summary>
    public bool IsOn(Product product) => product switch
    {
        Product.Claude => Claude,
        Product.Codex => Codex,
        _ => throw new ArgumentOutOfRangeException(nameof(product), product, null),
    };

    /// <summary>Whether a product may be switched off: not when it is the only one on.</summary>
    public bool CanTurnOff(Product product)
    {
        // Already off, or the other product stays on.
        var other = product == Product.Claude ? Product.Codex : Product.Claude;

        return !IsOn(product) || IsOn(other);
    }

    /// <summary>
    /// These switches with one product turned on or off, or these switches unchanged when that
    /// would leave nothing on.
    /// </summary>
    public ProductSwitches With(Product product, bool on)
    {
        var changed = product switch
        {
            Product.Claude => this with { Claude = on },
            Product.Codex => this with { Codex = on },
            _ => throw new ArgumentOutOfRangeException(nameof(product), product, null),
        };

        return changed.AnyOn ? changed : this;
    }

    /// <summary>Whether at least one product is on, which every saved answer must be.</summary>
    public bool AnyOn => Claude || Codex;
}
