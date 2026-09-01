using BingoHud.Core.Time;

namespace BingoHud.Core.Polling;

/// <summary>
/// How long ago Claude Code last wrote a transcript, as the proxy for whether anything is
/// happening.
///
/// <para>
/// Utilization moves when Claude Code is working and at no other time, so this is the signal
/// that lets Bingo poll faster while there is something to see and back off to its ceiling when
/// there is not. It supplies <see cref="PollSignals.SinceLocalTranscriptActivity"/>, which is
/// otherwise always null — and while it is null, that row of the cadence table can never fire.
/// </para>
/// <para>
/// Only modification times are read. Transcripts are the user's conversations, and the one thing
/// needed from them is a timestamp the filesystem already holds; opening them would be a serious
/// overreach for that.
/// </para>
/// </summary>
public sealed class TranscriptActivity(string projectsPath, IClock clock)
{
    /// <summary>
    /// Transcripts only. The projects directory also holds markdown written by other things, and
    /// counting those would report Claude Code as working when it is not — which spends requests
    /// against a rate-limited endpoint for nothing.
    /// </summary>
    private const string TranscriptPattern = "*.jsonl";

    private static readonly EnumerationOptions Enumeration = new()
    {
        RecurseSubdirectories = true,

        // A folder being written as it is walked, or one the user cannot read, is not a reason
        // to fail. The answer is allowed to be approximate; it is a scheduling hint.
        IgnoreInaccessible = true,
    };

    /// <summary>Where Claude Code keeps its transcripts: one folder per project.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude",
        "projects");

    /// <summary>
    /// How long ago the newest transcript was written, or null when that cannot be established.
    ///
    /// <para>
    /// Null means "no evidence" rather than "nothing is happening", and the two must not be
    /// conflated: <see cref="PollPolicy"/> treats absence of evidence as a reason to poll slowly,
    /// which is the safe direction to be wrong in against someone else's undocumented endpoint.
    /// </para>
    /// </summary>
    public TimeSpan? SinceLastWrite()
    {
        DateTime newest;

        try
        {
            newest = Directory
                .EnumerateFiles(projectsPath, TranscriptPattern, Enumeration)
                .Select(File.GetLastWriteTimeUtc)
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException
            or NotSupportedException or ArgumentException)
        {
            return null;
        }

        if (newest == DateTime.MinValue)
        {
            return null;
        }

        var since = clock.Now.UtcDateTime - newest;

        // Clock skew and restored backups both put a file in the future. Left negative, the span
        // would satisfy every comparison in the cadence table and pin Bingo to its fastest
        // cadence indefinitely.
        return since < TimeSpan.Zero ? TimeSpan.Zero : since;
    }
}
