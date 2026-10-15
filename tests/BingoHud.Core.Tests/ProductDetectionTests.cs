using BingoHud.Core.Settings;

namespace BingoHud.Core.Tests;

/// <summary>
/// Which products the first-start question starts ticked (codex-quota AC-8b).
///
/// <para>
/// A folder test only. It answers "is it installed for this user" well enough to pre-tick a box
/// the user then confirms, and it never runs either program, which could be slow or ask the user
/// to sign in.
/// </para>
/// </summary>
public class ProductDetectionTests
{
    private static string Folder(TempDirectory directory, string name) =>
        Directory.CreateDirectory(directory.PathTo(name)).FullName;

    [Fact]
    public void BothFoundAreBothTicked()
    {
        using var profile = new TempDirectory();
        Folder(profile, ".claude");
        Folder(profile, ".codex");

        var found = ProductDetection.Detect(profile.Path, codexHome: null);

        Assert.Equal(new ProductDetection(ClaudeFound: true, CodexFound: true), found);
        Assert.Equal(new ProductSwitches(Claude: true, Codex: true), found.Suggested);
    }

    [Fact]
    public void OnlyClaudeFoundTicksOnlyClaude()
    {
        using var profile = new TempDirectory();
        Folder(profile, ".claude");

        var found = ProductDetection.Detect(profile.Path, codexHome: null);

        Assert.Equal(new ProductDetection(true, false), found);
        Assert.Equal(new ProductSwitches(true, false), found.Suggested);
    }

    [Fact]
    public void OnlyCodexFoundTicksOnlyCodex()
    {
        using var profile = new TempDirectory();
        Folder(profile, ".codex");

        Assert.Equal(new ProductSwitches(false, true), ProductDetection.Detect(profile.Path, null).Suggested);
    }

    [Fact]
    public void NothingFoundTicksNothing()
    {
        // A suggestion, not an answer: the user may still tick a product that is not installed
        // yet. The question will not continue with nothing ticked.
        using var profile = new TempDirectory();

        var found = ProductDetection.Detect(profile.Path, codexHome: null);

        Assert.Equal(new ProductDetection(false, false), found);
        Assert.False(found.Suggested.AnyOn);
    }

    [Fact]
    public void CodexIsFoundWhereCodexHomePoints()
    {
        using var profile = new TempDirectory();
        using var elsewhere = new TempDirectory();

        Assert.True(ProductDetection.Detect(profile.Path, codexHome: elsewhere.Path).CodexFound);
    }

    [Fact]
    public void WithCodexHomeSetTheProfileFolderDoesNotCount()
    {
        // Codex uses CODEX_HOME when it is set, so a .codex in the profile is not where it lives.
        using var profile = new TempDirectory();
        Folder(profile, ".codex");

        Assert.False(ProductDetection.Detect(profile.Path, codexHome: profile.PathTo("missing")).CodexFound);
    }

    [Fact]
    public void AFileNamedLikeTheFolderIsNotTheFolder()
    {
        using var profile = new TempDirectory();
        profile.WriteFile(".claude", "not a folder");

        Assert.False(ProductDetection.Detect(profile.Path, codexHome: null).ClaudeFound);
    }
}
