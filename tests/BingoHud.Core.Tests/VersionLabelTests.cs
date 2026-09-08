using BingoHud.Core.Display;

namespace BingoHud.Core.Tests;

/// <summary>
/// How the running build is named in the detail panel (AC-24).
///
/// <para>
/// The panel shows a version for one reason: when the payload drifts, "which build is misreading
/// it" has to be answerable from the screen by a user who cannot be asked to run a command. A
/// release number alone answers that badly between releases, because every build since the last
/// tag carries the same one. The SDK already stamps the commit into the informational version,
/// so the panel shows it, shortened to the length people actually quote.
/// </para>
/// </summary>
public class VersionLabelTests
{
    [Fact]
    public void AVersionStampedWithACommitShowsBoth()
    {
        Assert.Equal(
            "1.0.0 (cbf7c40)",
            VersionLabel.Describe("1.0.0+cbf7c409ae3081bb813e652fc77119487099a82b"));
    }

    [Fact]
    public void AVersionWithNoCommitIsShownAsItIs()
    {
        // What a build with no source control information produces, and what 7.3 will set.
        Assert.Equal("0.1.0", VersionLabel.Describe("0.1.0"));
    }

    [Fact]
    public void ABuildMetadataFieldTooShortToBeACommitIsLeftOff()
    {
        // Build metadata is not required to be a commit hash. Showing an arbitrary short string
        // in parentheses beside a version implies a precision that is not there.
        Assert.Equal("1.0.0", VersionLabel.Describe("1.0.0+wip"));
    }

    [Fact]
    public void APreReleaseSuffixSurvives()
    {
        // A pre-release tag is part of the version proper, not build metadata, and dropping it
        // would have the panel claim a released build.
        Assert.Equal("0.2.0-beta.1 (cbf7c40)", VersionLabel.Describe("0.2.0-beta.1+cbf7c409ae3081"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AMissingVersionSaysSoRatherThanShowingAnEmptyRow(string? version)
    {
        // A blank beside the "Version" label reads as a panel that failed to load rather than as
        // a build that could not name itself.
        Assert.Equal("unknown", VersionLabel.Describe(version));
    }
}
