namespace BingoHud.Core.Display;

/// <summary>
/// How full each segment of a window's bar is, for the shell to draw.
///
/// <para>
/// Ten segments, filled to the exact figure: 75% is seven full segments and half of the eighth.
/// The segments are tick marks rather than steps, so the bar says exactly what the percentage
/// beside it says and there is no rounding rule to disagree with it. Computed here rather than in
/// the shell because the shell has no tests, and this is the arithmetic that can be wrong.
/// </para>
/// </summary>
public static class Bar
{
    public const int SegmentCount = 10;

    /// <summary>
    /// Each segment's fill, from 0 (empty) to 1 (full), left to right.
    /// </summary>
    /// <param name="usedPercent">
    /// The window's utilization as the server sent it. Clamped to 0..100 for drawing only: above
    /// 100 fills the bar and stops (display options AC-13), and a negative or NaN figure, which the
    /// server is not expected to send, draws nothing rather than a width the shell cannot lay out.
    /// </param>
    public static IReadOnlyList<double> Segments(double usedPercent)
    {
        // NaN fails every comparison, so it is caught before Clamp, which would pass it through.
        var used = double.IsNaN(usedPercent) ? 0 : Math.Clamp(usedPercent, 0, 100);
        var perSegment = 100.0 / SegmentCount;

        return Enumerable.Range(0, SegmentCount)
            .Select(i => Math.Clamp(used / perSegment - i, 0, 1))
            .ToArray();
    }
}
