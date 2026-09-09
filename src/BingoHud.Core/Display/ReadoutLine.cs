namespace BingoHud.Core.Display;

/// <summary>
/// One line of the HUD: which window, how much, and one fact after it.
/// </summary>
/// <param name="Window">The window's short name, as shown: "5h" or "Week".</param>
/// <param name="Percent">
/// The figure and the word that says which way it reads (AC-2b): "12% used" or "88% left".
/// The word travels with the number so the two can never be separated on screen.
/// </param>
/// <param name="Note">
/// What sits after the figure, or null when there is nothing to put there.
///
/// <para>
/// Two different facts share this slot, because the HUD has room for one. While the reading is
/// current it holds the reset phrase from <see cref="ResetFormatter"/>. While it is not, it
/// holds the reading's status from <see cref="StatusMessage"/> instead — an age, or the reason
/// nothing will refresh. The status displaces the countdown rather than joining it: a countdown
/// ticks every second whether or not anything is still being fetched, and that movement is what
/// makes a dead number look alive.
/// </para>
/// <para>
/// Null means show nothing, never a guessed time.
/// </para>
/// </param>
public sealed record ReadoutLine(string Window, string Percent, string? Note);
