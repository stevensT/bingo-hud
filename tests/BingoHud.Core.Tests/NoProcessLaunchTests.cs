using System.Reflection;

namespace BingoHud.Core.Tests;

/// <summary>
/// Core never starts a process.
///
/// <para>
/// This is the fence around the decision recorded at task 3.1. The original plan refreshed the
/// token by shelling out to <c>claude -p .</c>, which spends real quota to read a quota number —
/// the same objection that made the Messages-API fallback a non-goal. The refresh was cut
/// entirely.
/// </para>
/// <para>
/// A cut feature leaves no trace, which is exactly the problem. Someone later will see a token
/// expire, reach for the obvious fix, and reintroduce the shellout along with the console-window
/// flash, the environment stripping, the subprocess timeout, and the hung poller. The absence is
/// asserted here so that reintroducing it is a deliberate act that breaks a test naming the
/// decision, rather than a plausible-looking three-line patch.
/// </para>
/// <para>
/// Verified by mutation: with a single <c>Process.Start</c> call added to Core, this test failed
/// naming <c>System.Diagnostics.Process</c>.
/// </para>
/// </summary>
public class NoProcessLaunchTests
{
    [Fact]
    public void CoreDoesNotReferenceTheProcessApi()
    {
        var referenced = Assembly.Load("BingoHud.Core")
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.DoesNotContain("System.Diagnostics.Process", referenced);
    }
}
