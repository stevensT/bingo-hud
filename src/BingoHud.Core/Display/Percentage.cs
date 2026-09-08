using BingoHud.Core.Settings;

namespace BingoHud.Core.Display;

/// <summary>
/// Writes a stored percentage the way the user asked to read it (AC-2a, AC-2b).
///
/// <para>
/// Shared by the HUD and the detail panel deliberately. The two screens showing the same window
/// as "12% used" and "88% left" at the same moment would be the app contradicting itself, and
/// the direction a percentage reads in is the one error nobody can spot by looking at it.
/// </para>
/// </summary>
public static class Percentage
{
    /// <summary>
    /// The figure with the word that says which way it reads. The word travels with the number
    /// so the two can never be separated on screen.
    /// </summary>
    /// <param name="usedPercent">Utilization as stored: consumed, 0 to 100.</param>
    /// <param name="direction">Which way the user reads it.</param>
    public static string Describe(double usedPercent, DisplayDirection direction)
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
