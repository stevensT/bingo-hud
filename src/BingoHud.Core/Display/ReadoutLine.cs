namespace BingoHud.Core.Display;

/// <summary>
/// One line of the HUD: which window, how much, when it resets, and how close it is to its limit.
/// </summary>
/// <param name="Window">The window's short name, as shown: "5h" or "Week".</param>
/// <param name="Percent">
/// The figure and the word that says which way it reads (AC-2b): "12% used" or "88% left".
/// The word travels with the number so the two can never be separated on screen.
/// </param>
/// <param name="Reset">
/// The reset phrase from <see cref="ResetFormatter"/>, or null when the server gave no reset
/// time. Null means show nothing, never a guessed time. Led by "limited" when the server is
/// refusing work against the window (AC-6), so that state is not told apart by colour alone.
///
/// <para>
/// A line keeps its reset time whatever state the reading is in. How much the reading can still
/// be trusted is a fact about the reading rather than about one window, so it is said once by
/// <see cref="HudContent.Reading.Mark"/> instead of being stamped onto every line.
/// </para>
/// </param>
/// <param name="Severity">
/// How close this window is to its limit (AC-4, AC-6), which the shell draws as the figure's
/// colour. <see cref="Usage.Severity.Normal"/> for a frozen reading, which AC-13 excludes.
/// </param>
public sealed record ReadoutLine(string Window, string Percent, string? Reset, Usage.Severity Severity);
