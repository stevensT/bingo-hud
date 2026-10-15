namespace BingoHud.Core.Tests;

/// <summary>
/// Locates the recorded endpoint responses that the parser tests run against.
///
/// The fixtures live at <c>tests/fixtures/usage/</c> — outside any project — because they are
/// captured artefacts shared by the whole repository, not source belonging to the test project.
/// The test project copies them into its output directory at build time.
/// </summary>
internal static class Fixtures
{
    /// <summary>
    /// Full path to a named fixture in the test output directory.
    /// </summary>
    public static string Path(string fileName) =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", "usage", fileName);

    /// <summary>
    /// Contents of a named fixture.
    /// </summary>
    public static string Read(string fileName) => File.ReadAllText(Path(fileName));

    public const string Baseline = "2026-08-30-baseline.json";
    public const string AuthFailure = "2026-08-30-auth-failure.json";

    /// <summary>
    /// The baseline body with its <c>limits</c> array removed, and nothing else changed.
    ///
    /// Derived rather than captured: the live endpoint returns <c>limits[]</c> on every
    /// request, so the flat-key fallback cannot be observed from this account. See the
    /// fixture directory's README.
    /// </summary>
    public const string DerivedFlatOnly = "2026-08-30-derived-flat-only.json";

    /// <summary>
    /// Full path to a named Codex session log fixture in the test output directory. See
    /// <c>tests/fixtures/codex/README.md</c> for what each one is and what was cut from it.
    /// </summary>
    public static string CodexPath(string fileName) =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", "codex", fileName);

    /// <summary>
    /// The non-empty lines of a named Codex fixture, in file order.
    /// </summary>
    public static string[] CodexLines(string fileName) =>
        File.ReadAllLines(CodexPath(fileName)).Where(line => line.Length > 0).ToArray();

    public const string CodexSession = "2026-10-13-work-desktop-session.jsonl";
    public const string CodexTuiRecord = "2026-10-14-tui-record.jsonl";
    public const string CodexNullRecord = "2026-10-14-null-record.jsonl";
    public const string CodexUnknownWindow = "2026-10-14-made-unknown-window.jsonl";
    public const string CodexMissingField = "2026-10-14-made-missing-field.jsonl";
    public const string CodexWrongType = "2026-10-14-made-wrong-type.jsonl";
    public const string CodexReachedType = "2026-10-14-made-reached-type.jsonl";
    public const string CodexTruncatedTail = "2026-10-14-made-truncated-tail.jsonl";

    /// <summary>
    /// The prefix every invented conversation line in <see cref="CodexSession"/> carries.
    /// </summary>
    public const string DecoyMarker = "DECOY-MARKER-";
}
