namespace BingoHud.Core.Settings;

/// <summary>
/// Which products are installed for this user, to pre-tick the first-start question
/// (codex-quota AC-8b).
/// </summary>
/// <param name="ClaudeFound">Whether Claude Code's folder is there.</param>
/// <param name="CodexFound">Whether Codex's folder is there.</param>
public sealed record ProductDetection(bool ClaudeFound, bool CodexFound)
{
    /// <summary>
    /// Looks for each product's folder in the user profile, and for Codex where
    /// <c>CODEX_HOME</c> points when it is set.
    /// </summary>
    public static ProductDetection Detect(string userProfile, string? codexHome) => new(
        ClaudeFound: Directory.Exists(Path.Combine(userProfile, ".claude")),
        // The same folder the Codex reader reads, so "found" here and "not found" on the HUD
        // cannot disagree.
        CodexFound: Directory.Exists(Codex.CodexLogReader.RootFor(userProfile, codexHome)));

    /// <summary>
    /// The ticks the question starts with: found products on, missing ones off. Not an answer, so
    /// it may have nothing on; the question will not continue until something is ticked.
    /// </summary>
    public ProductSwitches Suggested => new(Claude: ClaudeFound, Codex: CodexFound);
}
