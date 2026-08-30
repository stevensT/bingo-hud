namespace BingoHud.Core.Tests;

/// <summary>
/// Proves the test runner actually runs, and actually fails when it should.
/// A green suite means nothing until a red one has been seen.
/// </summary>
public class ScaffoldTests
{
    // A deliberately failing test sat here during task 1.1. The runner reported it as
    // Failed: 1, Passed: 1 with a non-zero exit code, so the suite is known to be capable
    // of going red. It was then removed.
    [Fact]
    public void TestRunnerReportsAPassingTest()
    {
        Assert.True(true);
    }
}
