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
}
