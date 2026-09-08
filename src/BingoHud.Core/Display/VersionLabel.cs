namespace BingoHud.Core.Display;

/// <summary>
/// Names the running build for the detail panel (AC-24).
///
/// <para>
/// The panel shows a version so that "which build is misreading this payload" can be answered
/// from the screen, by a user who cannot be asked to run a command. Between releases a version
/// number alone answers it badly: every build since the last tag carries the same one. The SDK
/// stamps the commit into the informational version already, so it is shown alongside.
/// </para>
/// </summary>
public static class VersionLabel
{
    /// <summary>
    /// How much of a commit hash to show. Seven characters is what people quote and what the
    /// tooling around a repository abbreviates to, so it is the form a reader can act on.
    /// </summary>
    private const int ShortCommitLength = 7;

    /// <summary>
    /// The version as the panel shows it: the version proper, and the commit in parentheses when
    /// the build carries one.
    /// </summary>
    /// <param name="informationalVersion">
    /// The assembly's informational version, in the form the SDK produces:
    /// <c>1.0.0+cbf7c409...</c>, with everything after the plus sign being build metadata.
    /// </param>
    public static string Describe(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            // A blank beside the label reads as a panel that failed to load rather than as a
            // build that cannot name itself.
            return "unknown";
        }

        var version = informationalVersion.Trim();
        var plus = version.IndexOf('+');

        if (plus < 0)
        {
            return version;
        }

        // Everything before the plus is the version proper, pre-release tag included: that tag
        // says the build is not a release, and dropping it would have the panel claim otherwise.
        var released = version[..plus];
        var metadata = version[(plus + 1)..];

        // Build metadata is not required to be a commit hash. Anything shorter than the form we
        // would show is left off rather than displayed, because a short arbitrary string in
        // parentheses beside a version implies a precision that is not there.
        return metadata.Length < ShortCommitLength
            ? released
            : $"{released} ({metadata[..ShortCommitLength]})";
    }
}
