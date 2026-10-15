namespace BingoHud.Core.Codex;

/// <summary>
/// Finds the newest Codex rate-limit record with figures in the session logs (codex-quota AC-1).
///
/// <para>
/// Codex writes one JSONL log per session under <c>sessions\</c>, and moves old ones to
/// <c>archived_sessions\</c>. Every log is checked and the newest record timestamp wins.
/// File modification times cannot order records: Windows may not update a live log's time until
/// Codex closes its writer. Logs holding only null records contribute no reading.
/// </para>
/// <para>
/// The logs are the user's conversations. Only lines containing the text <c>"rate_limits"</c>
/// are handed to <see cref="CodexRecord.Parse"/>, and only what it returns is kept. Nothing else
/// in the Codex folder is opened: it also holds Codex's settings and sign-in.
/// </para>
/// </summary>
/// <param name="root">The Codex folder: <c>CODEX_HOME</c>, or <c>.codex</c> in the user profile.</param>
/// <param name="readLog">
/// How one log is read; <see cref="ReadShared"/> unless a test needs a file to change between
/// being listed and being opened, which cannot otherwise be timed.
/// </param>
public sealed class CodexLogReader(string root, Func<string, string>? readLog = null)
{
    private readonly Func<string, string> _readLog = readLog ?? ReadShared;

    private const string LogPattern = "*.jsonl";

    // The two folders Codex keeps session logs in. Nothing outside them is read.
    private static readonly string[] LogFolders = ["sessions", "archived_sessions"];

    private static readonly EnumerationOptions Enumeration = new()
    {
        RecurseSubdirectories = true,
        // A subfolder Bingo may not read is not a reason to refuse every other log; the logs it
        // holds are simply not candidates.
        IgnoreInaccessible = true,
    };

    public string Root { get; } = root;

    /// <summary>
    /// The environment variable Codex reads to find its folder, read here the same way so Bingo
    /// looks where Codex writes. The one environment variable a shipped build honours: it moves
    /// where local logs are read from, and no credential or request goes anywhere because of it.
    /// </summary>
    public const string HomeVariable = "CODEX_HOME";

    /// <summary>
    /// The Codex folder: <paramref name="codexHome"/> when it is set, as Codex itself does, else
    /// <c>.codex</c> in the user profile.
    /// </summary>
    public static string RootFor(string userProfile, string? codexHome) =>
        string.IsNullOrWhiteSpace(codexHome) ? Path.Combine(userProfile, ".codex") : codexHome;

    public CodexScan Scan()
    {
        if (!Directory.Exists(Root))
        {
            return new CodexScan.NotFound();
        }

        List<FileInfo> logs;

        try
        {
            logs = LogFiles().ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Unreadable("The Codex log folders could not be listed.");
        }

        CodexRecordResult? newest = null;
        DateTimeOffset? newestAt = null;

        foreach (var log in logs)
        {
            string content;

            try
            {
                content = _readLog(log.FullName);
            }
            catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
            {
                // Moved or deleted since it was listed: Codex moves finished sessions to
                // archived_sessions. Not a log Bingo was refused, so the rest are still read.
                continue;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return Unreadable(
                    "A Codex session log could not be opened, possibly because another program is holding it.");
            }

            if (NewestRecordIn(content) is not { } found)
            {
                continue;
            }

            var observedAt = found switch
            {
                CodexRecordResult.Figures figures => figures.Snapshot.ObservedAt,
                CodexRecordResult.Unreadable unreadable => unreadable.ObservedAt,
                _ => null,
            };

            // Without a timestamp the broken record cannot safely be placed before a reading.
            if (observedAt is null)
            {
                return new CodexScan.Problem(new CodexProblem.UnknownShape(
                    ((CodexRecordResult.Unreadable)found).Reason));
            }

            if (newestAt is null || observedAt > newestAt)
            {
                newest = found;
                newestAt = observedAt;
            }
        }

        return newest switch
        {
            CodexRecordResult.Figures figures => new CodexScan.Reading(figures),
            CodexRecordResult.Unreadable unreadable =>
                new CodexScan.Problem(new CodexProblem.UnknownShape(unreadable.Reason)),
            _ => new CodexScan.NoReading(),
        };
    }

    private IEnumerable<FileInfo> LogFiles() => LogFolders
        .Select(folder => Path.Combine(Root, folder))
        .Where(Directory.Exists)
        .SelectMany(folder => new DirectoryInfo(folder).EnumerateFiles(LogPattern, Enumeration));

    /// <summary>
    /// The whole log, opened so that Codex can go on writing it while it is read. Codex appends
    /// to the log of a running session, and a reader that locked it out would break Codex.
    /// </summary>
    public static string ReadShared(string path)
    {
        using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    /// <summary>
    /// The newest record with figures in one log, a problem if the newest record cannot be read,
    /// or null when the log holds no record with figures.
    /// </summary>
    private static CodexRecordResult? NewestRecordIn(string content)
    {
        var lines = content.Split('\n');

        // Every finished line ends in a newline, so the text after the last one is a line Codex
        // is still writing, or nothing at all. It is never a record yet.
        for (var i = lines.Length - 2; i >= 0; i--)
        {
            if (!lines[i].Contains("\"rate_limits\"", StringComparison.Ordinal))
            {
                continue;
            }

            switch (CodexRecord.Parse(lines[i]))
            {
                case CodexRecordResult.Figures figures:
                    return figures;

                // The newest record says Codex has changed what it writes. An older figure must
                // not stand in for it as though it were current.
                case CodexRecordResult.Unreadable unreadable:
                    return unreadable;

                // A null record, a damaged line that cannot be dated, or a conversation line that
                // mentions rate limits: keep looking.
                default:
                    continue;
            }
        }

        return null;
    }

    private static CodexScan.Problem Unreadable(string reason) =>
        new(new CodexProblem.LogsUnreadable(reason));
}
