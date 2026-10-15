using BingoHud.Core.Usage;

namespace BingoHud.Core.Display;

/// <summary>
/// What each window is called on screen.
///
/// <para>
/// Shared by the HUD and the detail panel, because a user who sees "Week" on one and "Weekly" on
/// the other has to work out whether they are the same thing. There is one name per window and
/// this is where it lives.
/// </para>
/// </summary>
public static class WindowName
{
    /// <summary>
    /// The short name, sized for the HUD, which has the least room and therefore sets the
    /// length. Per-model caps have no name here: they are labelled with the server's scope
    /// string instead, and only the panel shows them.
    /// </summary>
    public static string Short(WindowKind kind) => kind switch
    {
        WindowKind.Session => "5h",
        WindowKind.WeeklyAll => "Week",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a named window."),
    };

    /// <summary>
    /// A product's name as the user sees it. Written out rather than taken from the enum, so a
    /// rename in code cannot change what is on screen.
    /// </summary>
    public static string Product(Product product) => product switch
    {
        Core.Product.Claude => "Claude",
        Core.Product.Codex => "Codex",
        _ => throw new ArgumentOutOfRangeException(nameof(product), product, null),
    };

    /// <summary>
    /// A HUD line's label: the product, then the window, as "Claude 5h" or "Codex Week". Every
    /// line carries one, so neither product is the unlabelled default (codex-quota AC-7a).
    /// </summary>
    public static string Label(Product product, WindowKind kind) => $"{Product(product)} {Short(kind)}";
}
