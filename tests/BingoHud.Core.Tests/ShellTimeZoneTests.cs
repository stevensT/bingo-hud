namespace BingoHud.Core.Tests;

/// <summary>
/// The shell hands the machine's time zone to every screen that shows a reset time.
///
/// <para>
/// A fence test, like <see cref="InteropSeamTests"/>, and read from the shell's source for the
/// same reason. The composers take the zone as an optional argument, so that tests stay the same
/// on any machine; the cost is that a call which leaves it out still compiles, and quietly shows
/// reset times in today's offset again, an hour out across a daylight-saving change. Core's own
/// tests cannot see that. This can.
/// </para>
/// </summary>
public class ShellTimeZoneTests
{
    [Theory]
    [InlineData("Readout.Content(")]
    [InlineData("PanelReadout.Compose(")]
    [InlineData("AlertMessage.Describe(")]
    public void EveryShellCallPassesTheLocalZone(string call)
    {
        var calls = InteropSeamTests.ShellSources()
            .SelectMany(file => CallsIn(File.ReadAllText(file), call))
            .ToList();

        // A renamed composer would otherwise pass this by finding nothing to check.
        Assert.NotEmpty(calls);
        Assert.All(calls, text => Assert.Contains("TimeZoneInfo.Local", text));
    }

    /// <summary>
    /// Each call's text, from its name to the end of the statement it starts.
    /// </summary>
    private static IEnumerable<string> CallsIn(string source, string call)
    {
        for (var at = source.IndexOf(call, StringComparison.Ordinal);
             at >= 0;
             at = source.IndexOf(call, at + call.Length, StringComparison.Ordinal))
        {
            var end = source.IndexOf(';', at);
            yield return source[at..(end < 0 ? source.Length : end)];
        }
    }
}
