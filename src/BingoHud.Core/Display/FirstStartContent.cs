using BingoHud.Core.Settings;

namespace BingoHud.Core.Display;

/// <summary>One product's tick box in the first-start question.</summary>
/// <param name="Product">Which product the box is for.</param>
/// <param name="Name">The product's name as shown beside the box.</param>
/// <param name="Ticked">Whether the box starts ticked: found products do (AC-8b).</param>
/// <param name="Note">Said beside a product that was not found, or null when it was.</param>
public sealed record FirstStartChoice(Product Product, string Name, bool Ticked, string? Note);

/// <summary>
/// The first-start question (codex-quota AC-8a, AC-8b).
///
/// <para>
/// Bingo's first question to the user, asked once, before the HUD appears, on a new install and
/// on the first start after upgrading from a version without it. Both products are always
/// offered. Each one found on this computer starts ticked; one not found starts unticked and says
/// so, but can still be ticked, since it may be installed later.
/// </para>
/// </summary>
/// <param name="Title">The window's title.</param>
/// <param name="Question">What is being asked.</param>
/// <param name="Choices">One tick box per product, Claude first.</param>
/// <param name="ChangeLater">Where the answer can be changed afterwards.</param>
/// <param name="Continue">The button that saves the answer.</param>
public sealed record FirstStartContent(
    string Title,
    string Question,
    IReadOnlyList<FirstStartChoice> Choices,
    string ChangeLater,
    string Continue)
{
    private const string NotFound = "not found on this computer";

    public static FirstStartContent For(ProductDetection found) => new(
        Title: "Bingo",
        Question: "Which usage windows should Bingo show?",
        Choices:
        [
            // "Claude Code" here, where the user is choosing an installed program; the HUD's
            // label is "Claude", because the windows are the Claude plan's.
            new FirstStartChoice(Product.Claude, "Claude Code", found.ClaudeFound, found.ClaudeFound ? null : NotFound),
            new FirstStartChoice(Product.Codex, "Codex", found.CodexFound, found.CodexFound ? null : NotFound),
        ],
        ChangeLater: "You can change this later from Bingo's tray menu.",
        Continue: "Continue");

    /// <summary>
    /// Whether the ticks shown can be saved: at least one product, because a HUD showing nothing
    /// looks the same as one that has crashed.
    /// </summary>
    public static bool CanContinue(ProductSwitches ticked) => ticked.AnyOn;

    /// <summary>
    /// What to show for this session when the question is closed without an answer: what was
    /// found, or Claude alone when nothing was. Nothing is saved, so the question is asked again
    /// at the next start.
    /// </summary>
    public static ProductSwitches WhenClosed(ProductDetection found) =>
        found.Suggested.AnyOn ? found.Suggested : new ProductSwitches(Claude: true, Codex: false);
}
