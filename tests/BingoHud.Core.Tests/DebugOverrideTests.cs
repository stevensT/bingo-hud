namespace BingoHud.Core.Tests;

/// <summary>
/// The shell reads its environment in Debug builds only.
///
/// <para>
/// Two environment variables point a Debug build at another credential file and another endpoint,
/// so the error states can be forced and seen on screen without touching the real token. In a
/// shipped build the same two variables would let anything that can set a user's environment
/// redirect the token-bearing request, so they must not exist there. A fence test, read from the
/// shell's source like <see cref="InteropSeamTests"/>, because a preprocessor branch leaves nothing
/// in the Debug assembly this project could reflect over to tell the two builds apart.
/// </para>
/// <para>
/// A line scanner, not a preprocessor. Only a bare <c>#if DEBUG</c> opens a Debug branch, and any
/// <c>#elif</c>, <c>#else</c> or <c>#endif</c> closes it, so a nested block inside one fails the
/// fence rather than slipping past it — the safe direction for a heuristic to be wrong in.
/// </para>
/// </summary>
public class DebugOverrideTests
{
    // Without the "Environment." prefix, so a `using static System.Environment` cannot hide a
    // read. Also matches GetEnvironmentVariables, which is wanted.
    private const string EnvironmentRead = "GetEnvironmentVariable";

    /// <summary>
    /// Every line of shell source that reads the environment, and whether it sits inside an
    /// <c>#if DEBUG</c> branch.
    /// </summary>
    private static IEnumerable<(string File, int Line, string Text, bool InDebug)> EnvironmentReads()
    {
        foreach (var file in InteropSeamTests.ShellSources())
        {
            var inDebug = false;
            var number = 0;

            foreach (var line in File.ReadLines(file))
            {
                number++;
                var trimmed = line.Trim();

                if (trimmed == "#if DEBUG")
                {
                    inDebug = true;
                }
                else if (trimmed.StartsWith("#elif", StringComparison.Ordinal)
                    || trimmed.StartsWith("#else", StringComparison.Ordinal)
                    || trimmed.StartsWith("#endif", StringComparison.Ordinal))
                {
                    inDebug = false;
                }
                else if (line.Contains(EnvironmentRead, StringComparison.Ordinal))
                {
                    yield return (Path.GetFileName(file), number, trimmed, inDebug);
                }
            }
        }
    }

    [Fact]
    public void NothingInTheShellReadsTheEnvironmentOutsideADebugBranch()
    {
        Assert.Empty(EnvironmentReads().Where(r => !r.InDebug).Select(r => $"{r.File}:{r.Line}"));
    }

    [Theory]
    [InlineData("DebugOverrides.CredentialsVariable")]
    [InlineData("DebugOverrides.EndpointVariable")]
    public void EachOverrideIsReadInsideADebugBranch(string variable)
    {
        // Without this, the fence above passes by finding nothing, and would go on passing if
        // the overrides were rewritten in a form it does not recognise.
        Assert.Contains(EnvironmentReads(), r => r.InDebug && r.Text.Contains(variable, StringComparison.Ordinal));
    }
}
