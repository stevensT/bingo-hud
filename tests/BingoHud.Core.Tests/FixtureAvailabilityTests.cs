using System.Text.Json;

namespace BingoHud.Core.Tests;

/// <summary>
/// Proves the recorded responses actually reach the test output directory before any parser
/// test depends on them.
///
/// Fixture files silently failing to be copied is a common and confusing build problem: the
/// parser tests would fail with a file-not-found deep inside a parse, which looks nothing like
/// the real cause. Catching it here means that failure mode has one obvious home.
/// </summary>
public class FixtureAvailabilityTests
{
    [Theory]
    [InlineData(Fixtures.Baseline)]
    [InlineData(Fixtures.AuthFailure)]
    public void FixtureIsCopiedToTheTestOutputDirectory(string fileName)
    {
        var path = Fixtures.Path(fileName);

        Assert.True(
            File.Exists(path),
            $"Fixture '{fileName}' was not found at '{path}'. The test project must copy "
                + "tests/fixtures/usage into its output directory.");
    }

    [Theory]
    [InlineData(Fixtures.Baseline)]
    [InlineData(Fixtures.AuthFailure)]
    public void FixtureParsesAsAJsonObject(string fileName)
    {
        using var document = JsonDocument.Parse(Fixtures.Read(fileName));

        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
    }
}
